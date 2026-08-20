using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Fixed-size items, several across — an inventory grid, a level select. Rows scroll; columns divide the
    /// viewport's cross axis evenly.
    ///
    /// The whole grid is "fixed layout with an extra division": row = index / columns, column = index %
    /// columns. That it needs no more than that is the argument for the strategy split — a GridScrollView
    /// written as its own component would have carried a second full copy of the recycling logic for this.
    /// </summary>
    public sealed class GridScrollLayout : ScrollLayout
    {
        private readonly float _cellMainSize;
        private readonly int _crossCount;

        public GridScrollLayout(float cellMainSize, int crossCount)
        {
            _cellMainSize = Mathf.Max(0.01f, cellMainSize);
            _crossCount = Mathf.Max(1, crossCount);
        }

        /// <summary>Number of rows the items occupy — the last one usually being short.</summary>
        public int RowCount => ItemCount == 0 ? 0 : Mathf.CeilToInt(ItemCount / (float)_crossCount);

        /// <summary>Width of one column, dividing the viewport evenly.</summary>
        public float CellCrossSize => ViewportCrossSize / _crossCount;

        public override float ContentMainSize => RowCount * _cellMainSize;

        public override int MaxConcurrentCells => (Mathf.CeilToInt(ViewportMainSize / _cellMainSize) + 1) * _crossCount;

        /// <summary>The first item of the first visible row — the whole row is on screen together, so this is the row's first column.</summary>
        public override int FirstIndexAt(float mainOffset)
        {
            if (ItemCount == 0)
            {
                return 0;
            }

            var row = Mathf.Clamp(Mathf.FloorToInt(mainOffset / _cellMainSize), 0, Mathf.Max(0, RowCount - 1));
            return Mathf.Min(row * _crossCount, ItemCount - 1);
        }

        public override ScrollCellPlacement PlacementOf(int index)
        {
            var row = index / _crossCount;
            var column = index % _crossCount;

            return new ScrollCellPlacement(row * _cellMainSize, column * CellCrossSize, _cellMainSize, CellCrossSize);
        }
    }
}
