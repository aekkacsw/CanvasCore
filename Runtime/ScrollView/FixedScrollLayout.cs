using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Every item the same size, one per row. The v1 behaviour, kept as its own strategy because it is the
    /// case that needs no per-item state at all: positions are multiplication, the index at an offset is a
    /// division, and a list of a million rows costs exactly as much to lay out as a list of ten.
    /// </summary>
    public sealed class FixedScrollLayout : ScrollLayout
    {
        private readonly float _cellSize;

        public FixedScrollLayout(float cellSize) => _cellSize = Mathf.Max(0.01f, cellSize);

        public override float ContentMainSize => ItemCount * _cellSize;

        // +1 because a cell is almost never aligned to the viewport edge: a viewport exactly two cells tall
        // shows three of them the moment it is scrolled by a single pixel.
        public override int MaxConcurrentCells => Mathf.CeilToInt(ViewportMainSize / _cellSize) + 1;

        public override int FirstIndexAt(float mainOffset) =>
            ItemCount == 0 ? 0 : Mathf.Clamp(Mathf.FloorToInt(mainOffset / _cellSize), 0, ItemCount - 1);

        public override ScrollCellPlacement PlacementOf(int index) =>
            new(index * _cellSize, 0f, _cellSize, 0f);
    }
}
