using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Virtualized list: instantiates a small number of cells sized to the viewport (not to the item count)
    /// and re-binds them by index as the user scrolls. Wraps a standard ScrollRect for drag, momentum, and
    /// scrollbar behaviour, and manages only its content's children.
    ///
    /// <para><b>v2</b> keeps every bit of v1's behaviour and API and moves the positioning maths out into a
    /// <see cref="ScrollLayout"/>: vertical or horizontal, uniform or per-item sizes, single column or grid,
    /// all through the same recycling code. See <see cref="ScrollLayoutMode"/> for what each costs.</para>
    ///
    /// <para>The content RectTransform is driven entirely by this component — its anchors, pivot, and size
    /// along the scroll axis. Do not put a LayoutGroup or ContentSizeFitter on it: both would fight this for
    /// control of the same values, and the symptom is cells that jitter or pile up at the origin.</para>
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class RecycledScrollView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;

        [Tooltip("Prefab's root component — must implement IRecycledScrollCell.")]
        [SerializeField] private MonoBehaviour cellPrefab;

        [Tooltip("Size of one cell along the scroll axis: its height in a vertical list, its width in a horizontal one. Ignored in Variable Size mode, where the size provider answers instead.")]
        [SerializeField, Min(0.01f)] private float cellSize = 64f;

        [Tooltip("Extra cells kept instantiated beyond the visible viewport so fast scrolling doesn't show blank cells for a frame.")]
        [SerializeField, Min(0)] private int overscanCells = 2;

        [Tooltip("Which way the list scrolls. Set the ScrollRect's own Horizontal/Vertical toggles to match — this drives the content, that drives the dragging.")]
        [SerializeField] private ScrollAxis axis = ScrollAxis.Vertical;

        [Tooltip("Fixed Size: every cell the same. Variable Size: sizes come from SetSizeProvider() — call it before SetItemCount(). Grid: fixed cells, several across.")]
        [SerializeField] private ScrollLayoutMode layoutMode = ScrollLayoutMode.FixedSize;

        [Tooltip("Grid mode only: how many cells fit across the viewport. They divide its width (or height, in a horizontal list) evenly.")]
        [SerializeField, Min(1)] private int crossAxisCount = 2;

        private readonly struct PooledCell
        {
            public readonly RectTransform Transform;
            public readonly IRecycledScrollCell Cell;

            public PooledCell(RectTransform transform, IRecycledScrollCell cell)
            {
                Transform = transform;
                Cell = cell;
            }
        }

        private ScrollRect _scrollRect;
        private RectTransform _viewport;
        private ScrollLayout _layout;
        private Func<int, float> _sizeProvider;
        private readonly List<PooledCell> _pool = new();
        private readonly Dictionary<int, PooledCell> _bound = new();
        private readonly List<int> _scratchReleaseList = new();
        private int _itemCount;
        private int _firstVisibleIndex = -1;
        private bool _initialized;
        private float _lastViewportMain;
        private float _lastViewportCross;

        /// <summary>Raised after the set of bound cells changes, with the first and last index now on screen. For anything that has to follow the visible window — a selection highlight, an analytics "seen" marker.</summary>
        public event Action<int, int> VisibleRangeChanged;

        /// <summary>How many items the list is currently showing.</summary>
        public int ItemCount => _itemCount;

        /// <summary>Which way the list runs. Read by anything that has to turn a direction press into an index step.</summary>
        public ScrollAxis Axis => axis;

        /// <summary>How many items sit across the viewport — 1 for any non-grid list.</summary>
        public int CrossAxisCount => layoutMode == ScrollLayoutMode.Grid ? Mathf.Max(1, crossAxisCount) : 1;

        /// <summary>Lowest index with a live cell, or -1 when nothing is bound.</summary>
        public int FirstVisibleIndex { get; private set; } = -1;

        /// <summary>Highest index with a live cell, or -1 when nothing is bound.</summary>
        public int LastVisibleIndex { get; private set; } = -1;

        private void Awake() => EnsureInitialized();

        // Idempotent and called from every public entry point too, not just Awake — a consumer's own
        // Start()/Awake() calling SetItemCount() isn't guaranteed to run after this component's Awake()
        // given Unity's unspecified script execution order.
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _scrollRect = GetComponent<ScrollRect>();
            _viewport = _scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)transform;

            if (!(cellPrefab is IRecycledScrollCell))
            {
                throw new InvalidOperationException(
                    $"RecycledScrollView on '{name}': cellPrefab '{cellPrefab?.GetType().Name}' does not implement IRecycledScrollCell.");
            }

            _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            }
        }

        private void OnScrollChanged(Vector2 _) => RefreshVisibleRange();

        /// <summary>
        /// Notices the viewport changing size and rebuilds against the new one. Two float comparisons per
        /// frame, which is the cheap half of the deal; the expensive half is what happens without it — a
        /// window resize, an orientation change, or a layout group settling one frame late all leave the list
        /// sized to a viewport that no longer exists, showing too few cells or a scroll range that stops
        /// short of the last item.
        /// </summary>
        private void LateUpdate()
        {
            if (!_initialized || _layout == null || _itemCount == 0)
            {
                return;
            }

            var main = ViewportMainSize;
            var cross = ViewportCrossSize;

            if (Mathf.Abs(main - _lastViewportMain) < 0.5f && Mathf.Abs(cross - _lastViewportCross) < 0.5f)
            {
                return;
            }

            RebuildForViewport(main, cross);
        }

        /// <summary>Re-lays-out for a new viewport size, keeping the scroll position rather than snapping back to the top — a player resizing a window has not asked to lose their place.</summary>
        private void RebuildForViewport(float main, float cross)
        {
            _lastViewportMain = main;
            _lastViewportCross = cross;

            _layout.Rebuild(_itemCount, main, cross);
            ApplyContentGeometry();
            EnsurePoolSize();

            // Everything is released and re-bound: a cell's size and cross position can both change (a grid's
            // columns divide the new width), so keeping the existing placements would leave them stale.
            foreach (var pooled in _bound.Values)
            {
                pooled.Transform.gameObject.SetActive(false);
            }

            _bound.Clear();
            _firstVisibleIndex = -1;
            SetScrollOffset(Mathf.Min(CurrentScrollOffset, Mathf.Max(0f, _layout.ContentMainSize - main)));
            RefreshVisibleRange();
        }

        /// <summary>
        /// Supplies per-item sizes for <see cref="ScrollLayoutMode.VariableSize"/>. Call it before
        /// SetItemCount — the layout sums every size at that moment (see VariableScrollLayout for why it has
        /// to) and a provider arriving afterwards would be summing a list that has already been laid out.
        /// </summary>
        public void SetSizeProvider(Func<int, float> sizeProvider)
        {
            _sizeProvider = sizeProvider;
            _layout = null;
        }

        /// <summary>Call whenever the item count changes (list loaded/filtered/sorted). Resizes content and rebinds every currently-visible cell from scratch.</summary>
        public void SetItemCount(int itemCount)
        {
            EnsureInitialized();

            // A freshly Instantiate()'d hierarchy (e.g. calling this from OnCreated(), right inside
            // UIElementPool's createFunc) hasn't been through a Canvas layout pass yet — _viewport.rect
            // would read a stale/near-zero size and undersize the pool. Force one now so it's accurate
            // regardless of how soon after instantiation this runs.
            Canvas.ForceUpdateCanvases();

            _itemCount = Mathf.Max(0, itemCount);

            EnsureLayout();
            _lastViewportMain = ViewportMainSize;
            _lastViewportCross = ViewportCrossSize;
            _layout.Rebuild(_itemCount, _lastViewportMain, _lastViewportCross);

            ApplyContentGeometry();
            EnsurePoolSize();

            foreach (var pooled in _bound.Values)
            {
                pooled.Transform.gameObject.SetActive(false);
            }

            _bound.Clear();
            _firstVisibleIndex = -1;
            FirstVisibleIndex = -1;
            LastVisibleIndex = -1;

            RefreshVisibleRange();
        }

        /// <summary>Call when the underlying data changed but the count/scroll position didn't (e.g. a value flipped elsewhere) — rebinds only the cells currently on screen.</summary>
        public void Refresh()
        {
            foreach (var kvp in _bound)
            {
                kvp.Value.Cell.Bind(kvp.Key);
            }
        }

        /// <summary>
        /// Scrolls the minimum distance that brings an item fully into view, and leaves the list alone if it
        /// is already visible. What gamepad navigation calls when the selection moves past the edge, and what
        /// "jump to result" should call rather than setting normalizedPosition and hoping.
        /// </summary>
        public void ScrollTo(int index)
        {
            EnsureInitialized();

            if (_itemCount == 0)
            {
                return;
            }

            EnsureLayout();

            var target = _layout.OffsetToReveal(index, CurrentScrollOffset);

            if (Mathf.Approximately(target, CurrentScrollOffset))
            {
                return;
            }

            // Velocity has to die with the jump: momentum from the flick that triggered this would carry the
            // list straight back off the item that was just revealed.
            _scrollRect.velocity = Vector2.zero;
            SetScrollOffset(target);
            RefreshVisibleRange();
        }

        /// <summary>The live cell bound to an index, or null when that index is not on screen. For reaching into a visible row — a selection highlight, a focus target — without keeping a parallel list of cells.</summary>
        public IRecycledScrollCell GetBoundCell(int index) =>
            _bound.TryGetValue(index, out var pooled) ? pooled.Cell : null;

        private float ViewportMainSize =>
            axis == ScrollAxis.Vertical ? _viewport.rect.height : _viewport.rect.width;

        private float ViewportCrossSize =>
            axis == ScrollAxis.Vertical ? _viewport.rect.width : _viewport.rect.height;

        /// <summary>
        /// How far the list is scrolled, as a distance from the start, always positive. Both axes are
        /// normalised into that one number here so nothing downstream has to remember that Unity's content
        /// moves down-positive on one axis and left-negative on the other.
        /// </summary>
        private float CurrentScrollOffset =>
            axis == ScrollAxis.Vertical
                ? Mathf.Max(0f, content.anchoredPosition.y)
                : Mathf.Max(0f, -content.anchoredPosition.x);

        private void SetScrollOffset(float offset)
        {
            var position = content.anchoredPosition;

            if (axis == ScrollAxis.Vertical)
            {
                position.y = offset;
            }
            else
            {
                position.x = -offset;
            }

            content.anchoredPosition = position;
        }

        private void EnsureLayout()
        {
            if (_layout != null)
            {
                return;
            }

            _layout = layoutMode switch
            {
                ScrollLayoutMode.Grid => new GridScrollLayout(cellSize, crossAxisCount),
                ScrollLayoutMode.VariableSize when _sizeProvider != null => new VariableScrollLayout(_sizeProvider),
                ScrollLayoutMode.VariableSize => FallbackForMissingProvider(),
                _ => new FixedScrollLayout(cellSize),
            };
        }

        /// <summary>Variable mode with nobody to ask is a setup mistake, not a runtime state to handle silently — but it degrades to a uniform list rather than showing an empty one, so the mistake is visible in the console instead of on screen.</summary>
        private ScrollLayout FallbackForMissingProvider()
        {
            Debug.LogError(
                $"RecycledScrollView on '{name}': layout mode is Variable Size but no size provider was set. " +
                "Call SetSizeProvider() before SetItemCount(). Falling back to a fixed size of " +
                $"{cellSize}.", this);

            return new FixedScrollLayout(cellSize);
        }

        /// <summary>
        /// Points the content RectTransform at the start of the axis and sizes it to the whole list. Anchors
        /// are forced rather than trusted: a content set up for a vertical list and then switched to
        /// horizontal would otherwise keep growing downward, which looks like the list simply not working.
        /// </summary>
        private void ApplyContentGeometry()
        {
            var size = content.sizeDelta;

            if (axis == ScrollAxis.Vertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                size.y = _layout.ContentMainSize;
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                size.x = _layout.ContentMainSize;
            }

            content.sizeDelta = size;
        }

        private void EnsurePoolSize()
        {
            var desiredPoolSize = Mathf.Min(_itemCount, _layout.MaxConcurrentCells + overscanCells * 2);

            while (_pool.Count < desiredPoolSize)
            {
                var instance = Instantiate(cellPrefab, content);
                var rt = (RectTransform)instance.transform;
                instance.gameObject.SetActive(false);
                _pool.Add(new PooledCell(rt, (IRecycledScrollCell)instance));
            }
        }

        private void RefreshVisibleRange()
        {
            if (_itemCount == 0 || _layout == null)
            {
                return;
            }

            var offset = CurrentScrollOffset;
            var firstVisible = Mathf.Max(0, _layout.FirstIndexAt(offset) - OverscanItems);
            var lastVisible = Mathf.Min(_itemCount - 1, LastIndexInView(offset) + OverscanItems);

            // The cheap early-out that makes scrolling free between cell boundaries: while the same item is
            // still the topmost one, nothing has entered or left the viewport and there is nothing to rebind.
            if (firstVisible == _firstVisibleIndex)
            {
                return;
            }

            _firstVisibleIndex = firstVisible;

            _scratchReleaseList.Clear();

            foreach (var index in _bound.Keys)
            {
                if (index < firstVisible || index > lastVisible)
                {
                    _scratchReleaseList.Add(index);
                }
            }

            foreach (var index in _scratchReleaseList)
            {
                _bound[index].Transform.gameObject.SetActive(false);
                _bound.Remove(index);
            }

            for (var index = firstVisible; index <= lastVisible; index++)
            {
                if (_bound.ContainsKey(index))
                {
                    continue;
                }

                var pooled = FindFreeCell();

                if (pooled.Transform == null)
                {
                    Debug.LogWarning($"RecycledScrollView on '{name}': pool exhausted (size {_pool.Count}) — increase overscanCells or check cellSize.", this);
                    continue;
                }

                Place(pooled.Transform, _layout.PlacementOf(index));
                pooled.Transform.gameObject.SetActive(true);
                pooled.Cell.Bind(index);
                _bound[index] = pooled;
            }

            FirstVisibleIndex = firstVisible;
            LastVisibleIndex = lastVisible;
            VisibleRangeChanged?.Invoke(firstVisible, lastVisible);
        }

        /// <summary>Overscan is expressed in items, but a grid's "row" is several items wide — two rows of overscan in a 4-column grid is eight cells.</summary>
        private int OverscanItems =>
            _layout is GridScrollLayout ? overscanCells * crossAxisCount : overscanCells;

        /// <summary>The last item overlapping the viewport. Found from the far edge rather than counted forward from the first, so it stays right when items are different sizes.</summary>
        private int LastIndexInView(float offset) =>
            _layout.FirstIndexAt(offset + _layout.ViewportMainSize) + (_layout is GridScrollLayout grid ? crossAxisCount - 1 : 0);

        /// <summary>
        /// Positions and sizes one cell. A placement with no cross size means "fill the viewport across",
        /// which is the plain-list case and keeps v1's behaviour of cells that resize with the window.
        /// </summary>
        private void Place(RectTransform cell, ScrollCellPlacement placement)
        {
            var stretchAcross = placement.CrossSize <= 0f;

            if (axis == ScrollAxis.Vertical)
            {
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = new Vector2(stretchAcross ? 1f : 0f, 1f);
                cell.pivot = new Vector2(stretchAcross ? 0.5f : 0f, 1f);
                cell.sizeDelta = new Vector2(stretchAcross ? 0f : placement.CrossSize, placement.MainSize);
                cell.anchoredPosition = new Vector2(stretchAcross ? 0f : placement.Cross, -placement.Main);
            }
            else
            {
                cell.anchorMin = new Vector2(0f, stretchAcross ? 0f : 1f);
                cell.anchorMax = new Vector2(0f, 1f);
                cell.pivot = new Vector2(0f, stretchAcross ? 0.5f : 1f);
                cell.sizeDelta = new Vector2(placement.MainSize, stretchAcross ? 0f : placement.CrossSize);
                cell.anchoredPosition = new Vector2(placement.Main, stretchAcross ? 0f : -placement.Cross);
            }
        }

        private PooledCell FindFreeCell()
        {
            foreach (var pooled in _pool)
            {
                if (!pooled.Transform.gameObject.activeSelf)
                {
                    return pooled;
                }
            }

            return default;
        }
    }
}
