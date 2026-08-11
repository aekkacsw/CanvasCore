using System;
using System.Collections.Generic;

namespace Aexxa.CanvasCore
{
    public sealed class UIManager
    {
        public static UIManager Instance { get; private set; }

        private readonly UICatalogSO _catalog;
        private readonly UIRootCanvas _root;
        private readonly UIPoolManager _pools;
        private readonly Dictionary<Type, UIView> _active = new();

        public UIManager(UICatalogSO catalog, UIRootCanvas root)
        {
            _catalog = catalog;
            _root = root;
            _pools = new UIPoolManager(catalog, layerId => _root.GetLayer(layerId).Container);
            _pools.PrewarmBootEntries();
            Instance = this;
        }

        /// <summary>Singleton-per-type show, for Screens/Popups. Calling again while already shown just re-delivers context.</summary>
        public T Show<T>(object context = null) where T : UIView
        {
            if (_active.TryGetValue(typeof(T), out var existing))
            {
                existing.OnSpawn(context);
                return (T)existing;
            }

            var view = Spawn<T>(context);
            _active[typeof(T)] = view;
            return view;
        }

        /// <summary>Hides and pools back the singleton instance of T shown via Show&lt;T&gt;().</summary>
        public void Hide<T>() where T : UIView
        {
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
        /// Returns false if no stacked layer has anything to close, so callers can fall through to their
        /// own app-level "exit confirmation" handling.
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

            if (target == null)
            {
                return false;
            }

            CloseInstance(target.Top);
            return true;
        }

        private void CloseInstance(UIView view)
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
            var entry = _catalog.Get(viewType);
            var view = _pools.Get(viewType);

            view.Layer = entry.layer;
            view.OnSpawn(context);

            _root.GetLayer(entry.layer).Add(view);
            view.Show();

            return view;
        }

        /// <summary>Counterpart to Spawn&lt;T&gt;() — removes the given instance from its layer and returns it to the pool.</summary>
        public void Despawn(UIView view)
        {
            var entry = _catalog.Get(view.GetType());
            _root.GetLayer(entry.layer).Remove(view);
            _pools.Release(view);
        }
    }
}
