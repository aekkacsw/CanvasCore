using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Keeps the EventSystem's selection on the view the player is actually looking at, and puts it back
    /// where it was when that view closes.
    ///
    /// Without this a pad or keyboard is unusable the moment anything opens on top of anything: Unity's
    /// selection is a single global GameObject with no idea that a popup went up, so it stays on the button
    /// behind it — the player presses A and answers a dialog they cannot see. Everything here follows from
    /// that one problem.
    ///
    /// <para><b>A stack, because the UI is one.</b> Focus is pushed when a view that wants it is shown and
    /// popped when it closes, mirroring the back-stack UILayer already maintains. Popping restores both the
    /// view below <i>and the exact control that was selected in it</i>, so closing a settings popup puts the
    /// highlight back on the Settings button that opened it rather than on whatever happens to be first.</para>
    ///
    /// <para><b>Modality is enforced here too, not only visually.</b> A popup dims the screen behind it and
    /// its backdrop eats mouse clicks, but keyboard navigation walks the whole hierarchy and would happily
    /// step behind it. While a modal view holds focus, the view beneath is made non-interactable — which
    /// also, as a side effect worth having, stops a mouse click from reaching a button through a gap in a
    /// backdrop that does not cover the full screen.</para>
    ///
    /// <para>Owned by UIManager, driven by whoever calls <see cref="Tick"/> once a frame (UIBootstrap does).
    /// Every EventSystem call is guarded: a scene without one is a legitimate setup — a test fixture, or a
    /// project that has not added one yet — and must not throw.</para>
    /// </summary>
    public sealed class UIFocus
    {
        /// <summary>One level of the stack: a view that took focus, plus what was selected before it did.</summary>
        private readonly struct Entry
        {
            public readonly UIView View;
            public readonly GameObject PreviousSelection;

            public Entry(UIView view, GameObject previousSelection)
            {
                View = view;
                PreviousSelection = previousSelection;
            }
        }

        private readonly List<Entry> _stack = new();
        private readonly List<Selectable> _selectableBuffer = new();
        private bool _activated;
        private bool _warnedNoEventSystem;

        /// <summary>
        /// How this instance decides when a highlight is allowed on screen. Read from CanvasCoreSettings at
        /// construction; settable so a game can change it at runtime (a "gamepad mode" toggle in options).
        /// </summary>
        public UIFocusMode Mode { get; set; }

        /// <summary>
        /// Whether focus is currently being driven. Under <see cref="UIFocusMode.OnFirstNavigationInput"/>
        /// this stays false until the player presses a direction, so a mouse-only session never sees a
        /// highlight it did not ask for.
        /// </summary>
        public bool IsActive => Mode == UIFocusMode.Always || (Mode == UIFocusMode.OnFirstNavigationInput && _activated);

        /// <summary>The view currently holding focus, or null.</summary>
        public UIView Current => _stack.Count > 0 ? _stack[^1].View : null;

        public UIFocus(UIFocusMode mode) => Mode = mode;

        /// <summary>
        /// Takes focus for a view that has just been shown. Safe to call for every view — one that does not
        /// want focus (a toast, a background) is ignored rather than having to be filtered by the caller.
        /// </summary>
        public void Push(UIView view)
        {
            if (view == null || !view.TakesFocus || Mode == UIFocusMode.Never)
            {
                return;
            }

            _stack.Add(new Entry(view, CurrentSelection()));
            ApplyModality();

            if (IsActive)
            {
                SelectFirstIn(view);
            }
        }

        /// <summary>
        /// Releases focus held by a view that is closing. A view closed out of order — not the top of the
        /// stack — is simply removed from it: that is a legitimate thing for game code to do, and it must not
        /// hand focus to the wrong view or leave the one below permanently non-interactable.
        /// </summary>
        public void Pop(UIView view)
        {
            if (view == null)
            {
                return;
            }

            var index = LastIndexOf(view);

            if (index < 0)
            {
                return;
            }

            var wasTop = index == _stack.Count - 1;
            var entry = _stack[index];
            _stack.RemoveAt(index);

            // The view is leaving this stack, so nothing here may keep holding its input off. A view being
            // despawned has already been hidden by its layer and is skipped — reviving input on something
            // invisible would be tidy-looking and wrong.
            if (view.IsVisible)
            {
                view.SetInteractable(true);
            }

            ApplyModality();

            if (!wasTop || !IsActive)
            {
                return;
            }

            var restored = Current;

            // Prefer the exact control that was selected before this view opened; fall back to the restored
            // view's own starting point when that control is gone (its screen was rebuilt, its list scrolled).
            if (IsSelectable(entry.PreviousSelection))
            {
                Select(entry.PreviousSelection);
            }
            else if (restored != null)
            {
                SelectFirstIn(restored);
            }
            else
            {
                Select(null);
            }
        }

        /// <summary>
        /// Once a frame: notices the player reaching for a pad or keyboard, and repairs a selection that has
        /// gone missing.
        ///
        /// <para>The repair is the part that is easy to leave out and impossible to live without. Unity drops
        /// the selection to null whenever the selected object is deactivated or destroyed — a button hidden
        /// by a state change, a list cell recycled out from under the highlight — and a null selection means
        /// every further direction press does nothing at all. The pad simply stops working, with no error.</para>
        /// </summary>
        public void Tick()
        {
            if (Mode == UIFocusMode.Never)
            {
                return;
            }

            if (Mode == UIFocusMode.OnFirstNavigationInput)
            {
                if (!_activated && UINavigationInput.NavigatedThisFrame())
                {
                    _activated = true;
                }
                else if (_activated && UINavigationInput.PointerActivityThisFrame())
                {
                    // Handed back to the mouse. The highlight has to go with it: a frame left glowing around
                    // a button the player has stopped navigating to reads as a control that is stuck, and it
                    // competes with the hover state for "where am I".
                    _activated = false;
                    Select(null);
                    return;
                }
            }

            if (!IsActive)
            {
                return;
            }

            var focused = Current;

            if (focused == null || !focused.IsVisible)
            {
                return;
            }

            if (!IsSelectable(CurrentSelection()))
            {
                SelectFirstIn(focused);
            }
        }

        /// <summary>Moves the highlight to a specific control — for game code that wants to say "start here" at a moment of its own choosing.</summary>
        public void Select(GameObject target)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                WarnMissingEventSystem();
                return;
            }

            eventSystem.SetSelectedGameObject(target);
        }

        /// <summary>
        /// Said once, then never again. Without an EventSystem there is nothing to hold a selection, so every
        /// direction press does nothing — and the symptom is a game that simply ignores the gamepad, with no
        /// error to search for. CanvasCore's UIRoot deliberately does not ship one (a scene almost always has
        /// its own, and a second EventSystem is its own loud problem), so saying so here is the whole warning
        /// a developer gets.
        /// </summary>
        private void WarnMissingEventSystem()
        {
            if (_warnedNoEventSystem)
            {
                return;
            }

            _warnedNoEventSystem = true;

            Debug.LogWarning(
                "CanvasCore: no EventSystem in the scene, so gamepad and keyboard navigation cannot work — " +
                "nothing can hold a selection. Add one via GameObject > UI > Event System. If the UI has to " +
                "survive scene loads, mark it DontDestroyOnLoad alongside UIRoot, or set Focus Mode to Never " +
                "in CanvasCoreSettings to silence this.");
        }

        /// <summary>Forgets everything. For a scene teardown, or a test that must not inherit the previous one's stack.</summary>
        public void Clear()
        {
            _stack.Clear();
            _activated = false;
        }

        /// <summary>
        /// Recomputes, for the whole stack, who may accept input: everything below the topmost modal view is
        /// locked out, everything from it upward is live.
        ///
        /// Derived from the stack every time rather than toggled at each push and pop, because the toggling
        /// version only holds while views close in the order they opened. A view closed out from under
        /// another — legal, and something game code does during a state reset — left the one below it
        /// permanently non-interactable, which reads as a screen that has simply stopped responding.
        /// </summary>
        private void ApplyModality()
        {
            if (Mode == UIFocusMode.Never)
            {
                return;
            }

            var blocked = false;

            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var view = _stack[i].View;

                if (view == null)
                {
                    continue;
                }

                if (view.IsVisible)
                {
                    view.SetInteractable(!blocked);
                }

                blocked |= view.BlocksInteractionBelow;
            }
        }

        /// <summary>
        /// Selects the view's declared starting control, or the first interactable one it can find. The
        /// fallback is what lets an existing prefab work with no editing at all: a screen that never heard of
        /// this system still becomes navigable, and setting First Selected is an override, not a requirement.
        /// </summary>
        private void SelectFirstIn(UIView view)
        {
            var declared = view.FirstSelected;

            if (declared != null && declared.gameObject.activeInHierarchy && declared.IsInteractable())
            {
                Select(declared.gameObject);
                return;
            }

            view.GetComponentsInChildren(false, _selectableBuffer);

            foreach (var selectable in _selectableBuffer)
            {
                if (selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                {
                    Select(selectable.gameObject);
                    return;
                }
            }

            // Nothing to select is a normal state — a popup that is only text, a view mid-transition. Leaving
            // the old selection standing would point the highlight at a control behind this view.
            Select(null);
        }

        private int LastIndexOf(UIView view)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].View, view))
                {
                    return i;
                }
            }

            return -1;
        }

        private static GameObject CurrentSelection() =>
            EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        /// <summary>Whether this object can actually hold a selection right now — present, on screen, and not disabled.</summary>
        private static bool IsSelectable(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            var selectable = target.GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }
    }
}
