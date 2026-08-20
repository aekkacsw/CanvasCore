using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Makes a <see cref="RecycledScrollView"/> navigable with a gamepad or keyboard, by holding the
    /// selection itself instead of letting it live on the cells.
    ///
    /// <para><b>Why not just put Buttons in the cells.</b> Because a virtualized list has no GameObject per
    /// item — it has a handful of cells that change which item they show. Unity's selection points at a
    /// GameObject, so the moment that cell is recycled the highlight is on the wrong item; and the item the
    /// player is trying to move to usually has no GameObject at all yet, so Unity's navigation cannot even
    /// find it. Every "gamepad support" bug report about an infinite list is one of those two.</para>
    ///
    /// <para><b>What this does instead.</b> The list as a whole is one selectable thing. While it holds focus,
    /// direction presses move a <see cref="SelectedIndex"/>, the list scrolls just far enough to reveal it,
    /// and whichever cell is currently showing that index is told to look selected (see
    /// <see cref="IRecycledScrollCellSelection"/>). Submit reports the index. Pressing past either end does
    /// nothing here and falls through to Unity's own navigation, so the player can leave the list the usual
    /// way.</para>
    ///
    /// <para>Add it to the same GameObject as the RecycledScrollView. Cells need no Selectable of their own —
    /// and should not have one, or the two selection models will fight.</para>
    /// </summary>
    [RequireComponent(typeof(RecycledScrollView))]
    [AddComponentMenu("Canvas Core/Recycled Scroll Navigator")]
    public sealed class RecycledScrollNavigator : Selectable, IMoveHandler, ISubmitHandler
    {
        [SerializeField]
        [Tooltip("Item highlighted when the list first takes focus.")]
        private int startIndex;

        [SerializeField]
        [Tooltip("Wrap from the last item back to the first. Off by default: pressing down past the end then falls through to Unity's navigation and lets the player leave the list.")]
        private bool wrapAround;

        private RecycledScrollView _list;
        private int _selectedIndex = -1;

        /// <summary>Item the player is on, or -1 when the list does not hold focus. Setting it scrolls the item into view.</summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => Select(value);
        }

        /// <summary>Raised when the player submits (A / Enter) on an item, with its index.</summary>
        public event Action<int> Submitted;

        /// <summary>Raised whenever the highlighted item changes, with the new index (-1 when the list loses focus).</summary>
        public event Action<int> SelectionChanged;

        protected override void Awake()
        {
            base.Awake();
            _list = GetComponent<RecycledScrollView>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_list != null)
            {
                // Re-applied on every rebind: the cell showing the selected item is a different object after
                // any scroll, and the new one has no idea it is the selected row.
                _list.VisibleRangeChanged += OnVisibleRangeChanged;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_list != null)
            {
                _list.VisibleRangeChanged -= OnVisibleRangeChanged;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            // Coming back to a list the player already used should return them to where they were, not to the
            // top of it.
            Select(_selectedIndex >= 0 ? _selectedIndex : startIndex);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            ApplySelectionToCells(-1);
            SelectionChanged?.Invoke(-1);
        }

        /// <summary>Override rather than a new method: Selectable already handles moves, and hiding it would leave two implementations whose behaviour depended on which type the caller held a reference through.</summary>
        public override void OnMove(AxisEventData eventData)
        {
            var step = StepFor(eventData.moveDir);

            if (step == 0 || _list.ItemCount == 0)
            {
                // Not a direction this list consumes (moving sideways out of a single-column list) — hand it
                // back to Unity so the player can navigate to whatever is next to the list.
                base.OnMove(eventData);
                return;
            }

            var next = _selectedIndex + step;

            if (next < 0 || next >= _list.ItemCount)
            {
                if (!wrapAround)
                {
                    base.OnMove(eventData);
                    return;
                }

                next = next < 0 ? _list.ItemCount - 1 : 0;
            }

            Select(next);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _list.ItemCount)
            {
                Submitted?.Invoke(_selectedIndex);
            }
        }

        /// <summary>
        /// How far one direction press moves through the item list. A grid's rows are <c>columns</c> items
        /// apart, which is the whole difference between navigating a grid and navigating a list.
        /// </summary>
        private int StepFor(MoveDirection direction)
        {
            var alongAxis = _list.Axis == ScrollAxis.Vertical;
            var rowStep = _list.CrossAxisCount;

            return direction switch
            {
                MoveDirection.Up => alongAxis ? -rowStep : rowStep > 1 ? -1 : 0,
                MoveDirection.Down => alongAxis ? rowStep : rowStep > 1 ? 1 : 0,
                MoveDirection.Left => alongAxis ? (rowStep > 1 ? -1 : 0) : -rowStep,
                MoveDirection.Right => alongAxis ? (rowStep > 1 ? 1 : 0) : rowStep,
                _ => 0,
            };
        }

        private void Select(int index)
        {
            if (_list == null || _list.ItemCount == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp(index, 0, _list.ItemCount - 1);
            _selectedIndex = clamped;

            // Scroll first: the cell that should show as selected may not exist until the list has moved.
            _list.ScrollTo(clamped);
            ApplySelectionToCells(clamped);
            SelectionChanged?.Invoke(clamped);
        }

        private void OnVisibleRangeChanged(int first, int last) => ApplySelectionToCells(_selectedIndex);

        /// <summary>Tells every live cell whether it is the selected one. Cheap — "every live cell" is a viewport's worth, not the item count.</summary>
        private void ApplySelectionToCells(int selectedIndex)
        {
            if (_list == null || _list.FirstVisibleIndex < 0)
            {
                return;
            }

            for (var index = _list.FirstVisibleIndex; index <= _list.LastVisibleIndex; index++)
            {
                if (_list.GetBoundCell(index) is IRecycledScrollCellSelection selectable)
                {
                    selectable.SetSelected(index == selectedIndex);
                }
            }
        }
    }
}
