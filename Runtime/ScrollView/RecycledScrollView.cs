using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Fixed-height, vertical-only virtualized list. Instantiates a small, fixed number of cells sized
    /// to the viewport (not the item count) and re-binds them by index as the user scrolls, instead of
    /// spawning one GameObject per item. Wraps a standard ScrollRect for drag/momentum/scrollbar behaviour
    /// and only manages its `content`'s children.
    ///
    /// `content`'s anchor/pivot is forced to top-stretch (0,1)-(1,1) and its height is driven entirely by
    /// SetItemCount() * cellSize — don't add a LayoutGroup or ContentSizeFitter to it, both would fight
    /// this component for control of the same RectTransform.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class RecycledScrollView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [Tooltip("Prefab's root component — must implement IRecycledScrollCell.")]
        [SerializeField] private MonoBehaviour cellPrefab;
        [SerializeField, Min(0.01f)] private float cellSize = 64f;
        [Tooltip("Extra cells kept instantiated beyond the visible viewport so fast scrolling doesn't show blank cells for a frame.")]
        [SerializeField, Min(0)] private int overscanCells = 2;

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
        private readonly List<PooledCell> _pool = new();
        private readonly Dictionary<int, PooledCell> _bound = new();
        private readonly List<int> _scratchReleaseList = new();
        private int _itemCount;
        private int _firstVisibleIndex = -1;
        private bool _initialized;

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

            content.anchorMin = new Vector2(content.anchorMin.x, 1f);
            content.anchorMax = new Vector2(content.anchorMax.x, 1f);
            content.pivot = new Vector2(content.pivot.x, 1f);
            content.sizeDelta = new Vector2(content.sizeDelta.x, _itemCount * cellSize);

            EnsurePoolSize();

            foreach (var pooled in _bound.Values)
            {
                pooled.Transform.gameObject.SetActive(false);
            }
            _bound.Clear();

            _firstVisibleIndex = -1;
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

        private void EnsurePoolSize()
        {
            var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
            var visibleCount = Mathf.CeilToInt(viewportHeight / cellSize) + 1;
            var desiredPoolSize = Mathf.Min(_itemCount, visibleCount + overscanCells * 2);

            while (_pool.Count < desiredPoolSize)
            {
                var instance = Instantiate(cellPrefab, content);
                var rt = (RectTransform)instance.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, cellSize);
                instance.gameObject.SetActive(false);
                _pool.Add(new PooledCell(rt, (IRecycledScrollCell)instance));
            }
        }

        private void RefreshVisibleRange()
        {
            if (_itemCount == 0)
            {
                return;
            }

            var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
            var scrollOffset = Mathf.Max(0f, content.anchoredPosition.y);
            var firstVisible = Mathf.Clamp(Mathf.FloorToInt(scrollOffset / cellSize) - overscanCells, 0, _itemCount - 1);
            var visibleCount = Mathf.CeilToInt(viewportHeight / cellSize) + overscanCells * 2;
            var lastVisible = Mathf.Min(_itemCount - 1, firstVisible + visibleCount - 1);

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

                pooled.Transform.anchoredPosition = new Vector2(pooled.Transform.anchoredPosition.x, -index * cellSize);
                pooled.Transform.gameObject.SetActive(true);
                pooled.Cell.Bind(index);
                _bound[index] = pooled;
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
