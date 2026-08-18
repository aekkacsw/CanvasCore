using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    public sealed class UIManager
    {
        public static UIManager Instance { get; private set; }

        private readonly UICatalogSO _catalog;
        private readonly UIRootCanvas _root;
        private readonly UIPoolManager _pools;
        private readonly Dictionary<Type, UIView> _active = new();
        private readonly Dictionary<Type, int> _blockRefCounts = new();
        private readonly Queue<UIView> _popupQueue = new();
        private readonly int _maxQueuedPopups;
        private UIView _activePopup;

        /// <summary>
        /// Whether the UIRoot this instance drives is still alive. UIRootCanvas is a MonoBehaviour, so this
        /// reflects Unity's destroyed-object check (not just "is the C# reference non-null") — false if the
        /// root was destroyed by anything other than this UIManager's own normal operation. A bootstrapper
        /// holding a candidate UIManager.Instance should check this before treating it as the live one to
        /// defer to; a stale, no-longer-alive Instance should be replaced, not preserved.
        /// </summary>
        public bool IsAlive => _root != null;

        public UIManager(UICatalogSO catalog, UIRootCanvas root, int maxQueuedPopups = 20)
        {
            _catalog = catalog;
            _root = root;
            _maxQueuedPopups = maxQueuedPopups;
            _pools = new UIPoolManager(catalog, layerId => _root.GetLayer(layerId).Container);
            _pools.PrewarmBootEntries();
            Instance = this;
        }

        /// <summary>
        /// Singleton-per-type show, for Screens/Popups. Calling again while already shown just re-delivers
        /// context. For UIBlocker types this also reference-counts: N calls to Show&lt;T&gt;() require N
        /// matching Hide&lt;T&gt;() calls before it actually hides, so two independent systems (e.g. a
        /// network request and an asset load) can both block input without one finishing first and
        /// unblocking while the other still needs it.
        ///
        /// For UIPopup types this also queues: only one popup is ever on screen at a time. If a popup
        /// (any type, including this same one) is already showing, this call is queued instead of
        /// showing immediately or overwriting the visible popup's context — so a second, unrelated
        /// Show&lt;SomePopup&gt;() call from another system can't hijack a popup the user hasn't answered
        /// yet. Queued popups show automatically, in call order, as each one ahead of them closes.
        /// </summary>
        public T Show<T>(object context = null) where T : UIView
        {
            if (typeof(UIPopup).IsAssignableFrom(typeof(T)))
            {
                return (T)ShowPopup(typeof(T), context);
            }

            if (typeof(UIBlocker).IsAssignableFrom(typeof(T)))
            {
                _blockRefCounts.TryGetValue(typeof(T), out var count);
                _blockRefCounts[typeof(T)] = count + 1;
            }

            if (_active.TryGetValue(typeof(T), out var existing))
            {
                existing.OnSpawn(context);
                return (T)existing;
            }

            var view = Spawn<T>(context);
            _active[typeof(T)] = view;
            return view;
        }

        private UIView ShowPopup(Type popupType, object context)
        {
            if (_activePopup == null)
            {
                var view = PrepareView(popupType, context);
                _active[popupType] = view;
                _activePopup = view;
                ActivateView(view);
                return view;
            }

            // Something is already on screen — prepare (pool + configure via OnSpawn) this one now so
            // Show<T>() still has a real instance to hand back, but don't add it to its layer or make it
            // visible yet. It'll be activated in call order as popups ahead of it close (see Despawn()).
            var queued = PrepareView(popupType, context);

            if (_popupQueue.Count >= _maxQueuedPopups)
            {
                // Bail out rather than growing forever — most likely something is calling Show<T>() on a
                // popup repeatedly (a loop, an event firing more than expected) without the ones ahead of
                // it ever getting answered/closed. Still hand back a real, non-null instance (Show<T>()'s
                // contract never returns null) but it's already been through OnDespawn and will never be
                // shown, same as any other dropped/cancelled queued popup.
                Debug.LogWarning($"UIManager: popup queue is full ({_maxQueuedPopups} pending) — dropping Show<{popupType.Name}>() instead of queueing it. Check for something calling Show repeatedly on a popup that's never being closed.");
                Despawn(queued);
                return queued;
            }

            _popupQueue.Enqueue(queued);
            return queued;
        }

        /// <summary>
        /// Drops every not-yet-shown popup from the queue and releases each back to its pool — running its
        /// OnDespawn() along the way, exactly like a normal close, so any context it captured (callbacks,
        /// scene object references) gets cleared instead of firing later against something that may no
        /// longer be valid. The currently visible popup (if any) is untouched; use Hide&lt;T&gt;()/Close()
        /// for that. Call this from your own scene-transition/state-reset code wherever queued popups would
        /// no longer make sense — e.g. before loading a new level, so a reward popup queued in the level
        /// just left doesn't surface in the level just entered.
        /// </summary>
        public void ClearPopupQueue()
        {
            while (_popupQueue.Count > 0)
            {
                Despawn(_popupQueue.Dequeue());
            }
        }

        /// <summary>Like ClearPopupQueue(), but only for queued (not-yet-shown) instances of T — other queued types and the currently visible popup (if any) are left alone.</summary>
        public void CancelQueued<T>() where T : UIPopup
        {
            var remaining = _popupQueue.Count;

            for (var i = 0; i < remaining; i++)
            {
                var view = _popupQueue.Dequeue();

                if (view.GetType() == typeof(T))
                {
                    Despawn(view);
                }
                else
                {
                    _popupQueue.Enqueue(view);
                }
            }
        }

        /// <summary>
        /// Hides and pools back the singleton instance of T shown via Show&lt;T&gt;(). For UIBlocker types
        /// this only actually hides once every matching Show&lt;T&gt;() call has a corresponding Hide&lt;T&gt;()
        /// — see Show&lt;T&gt;() for why.
        /// </summary>
        public void Hide<T>() where T : UIView
        {
            if (typeof(UIBlocker).IsAssignableFrom(typeof(T)) && _blockRefCounts.TryGetValue(typeof(T), out var count) && count > 0)
            {
                if (count > 1)
                {
                    _blockRefCounts[typeof(T)] = count - 1;
                    return;
                }

                _blockRefCounts.Remove(typeof(T));
            }

            if (!_active.Remove(typeof(T), out var view))
            {
                return;
            }

            Despawn(view);
        }

        public bool IsActive<T>() where T : UIView => _active.ContainsKey(typeof(T));

        /// <summary>
        /// Closes the top-most view of the highest-priority stacked layer that currently has one showing
        /// (e.g. a Popup closes before the Screen beneath it). Intended for back-button/ESC input.
        /// Returns false if no stacked layer has anything to close — either nothing is showing, or the
        /// top-most view is a root UIScreen (UIScreen.IsRootScreen) — so callers can fall through to
        /// their own app-level "exit confirmation" handling.
        /// </summary>
        public bool HandleBack()
        {
            UILayer target = null;

            foreach (var layer in _root.Layers)
            {
                if (!layer.IsStacked || layer.Top == null)
                {
                    continue;
                }

                if (target == null || layer.LayerId > target.LayerId)
                {
                    target = layer;
                }
            }

            if (target == null || target.Top is UIScreen { IsRootScreen: true })
            {
                return false;
            }

            Close(target.Top);
            return true;
        }

        /// <summary>Removes a view instance from its layer and returns it to the pool, clearing it from the Show/Hide singleton slot if it currently occupies one. Use this (rather than Despawn) when closing a view you didn't obtain via Show/Hide's known Type, e.g. from within the view itself.</summary>
        public void Close(UIView view)
        {
            var type = view.GetType();

            if (_active.TryGetValue(type, out var active) && ReferenceEquals(active, view))
            {
                _active.Remove(type);
            }

            Despawn(view);
        }

        /// <summary>Spawns a toast that auto-despawns itself after <paramref name="duration"/> seconds.</summary>
        public T Toast<T>(string message, float duration = 2f) where T : UIToast => Spawn<T>(new UIToast.Context(message, duration));

        /// <summary>Repeatable spawn for Widgets — multiple concurrent instances of the same type are allowed.</summary>
        public T Spawn<T>(object context = null) where T : UIView => (T)Spawn(typeof(T), context);

        /// <summary>Type-erased counterpart to Spawn&lt;T&gt;() for callers that only know the Type at runtime (tooling, catalog validation).</summary>
        public UIView Spawn(Type viewType, object context = null)
        {
            var view = PrepareView(viewType, context);
            ActivateView(view);
            return view;
        }

        /// <summary>Gets a pooled instance and delivers context, without adding it to a layer or making it visible yet.</summary>
        private UIView PrepareView(Type viewType, object context)
        {
            var entry = _catalog.Get(viewType);
            var view = _pools.Get(viewType);

            view.Layer = entry.layer;
            view.OnSpawn(context);

            return view;
        }

        /// <summary>Adds an already-prepared view to its layer and shows it.</summary>
        private void ActivateView(UIView view)
        {
            var entry = _catalog.Get(view.GetType());
            _root.GetLayer(entry.layer).Add(view);
            view.Show();
        }

        /// <summary>Counterpart to Spawn&lt;T&gt;() — removes the given instance from its layer and returns it to the pool. If this was the on-screen popup, activates the next queued popup (if any) — see Show&lt;T&gt;().</summary>
        public void Despawn(UIView view)
        {
            var entry = _catalog.Get(view.GetType());
            _root.GetLayer(entry.layer).Remove(view);
            _pools.Release(view);

            if (!ReferenceEquals(view, _activePopup))
            {
                return;
            }

            _activePopup = null;

            if (_popupQueue.Count == 0)
            {
                return;
            }

            var next = _popupQueue.Dequeue();
            _active[next.GetType()] = next;
            _activePopup = next;
            ActivateView(next);
        }
    }
}
