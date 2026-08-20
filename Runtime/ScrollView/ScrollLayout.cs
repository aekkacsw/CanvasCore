using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>Which way the list runs. The cross axis is whatever is left over.</summary>
    public enum ScrollAxis
    {
        Vertical,
        Horizontal,
    }

    /// <summary>How a list decides where its items sit. See the concrete layouts for what each one costs.</summary>
    public enum ScrollLayoutMode
    {
        /// <summary>Every item the same size. Positions are arithmetic — no per-item state, nothing to rebuild when the count changes.</summary>
        FixedSize,

        /// <summary>Per-item sizes from a callback. Buys wrapping text and mixed row heights; costs a prefix-sum array and a size query per item (see VariableScrollLayout).</summary>
        VariableSize,

        /// <summary>Fixed-size items, several across. An inventory grid.</summary>
        Grid,
    }

    /// <summary>Where one cell goes, in content-local space. Cross size 0 means "stretch across the viewport", which is what a plain list wants and a grid never does.</summary>
    public readonly struct ScrollCellPlacement
    {
        public readonly float Main;
        public readonly float Cross;
        public readonly float MainSize;
        public readonly float CrossSize;

        public ScrollCellPlacement(float main, float cross, float mainSize, float crossSize)
        {
            Main = main;
            Cross = cross;
            MainSize = mainSize;
            CrossSize = crossSize;
        }
    }

    /// <summary>
    /// The part of a virtualized list that knows where things are: content size, which index sits at a scroll
    /// offset, and where any given index belongs.
    ///
    /// Splitting this out is what makes v2 one component instead of three. Vertical, horizontal, fixed,
    /// variable, and grid differ only in this arithmetic — the recycling, pooling, binding, and ScrollRect
    /// wiring around it are identical, and a copied class per variation would mean fixing every recycling bug
    /// three times.
    ///
    /// <para>Deliberately free of Unity objects: it takes numbers and returns numbers, so the index maths —
    /// which is where the bugs that show as "a blank row while scrolling fast" actually live — can be tested
    /// without a Canvas, a prefab, or a play mode session.</para>
    /// </summary>
    public abstract class ScrollLayout
    {
        public int ItemCount { get; private set; }

        /// <summary>Viewport size along the scroll axis.</summary>
        public float ViewportMainSize { get; private set; }

        /// <summary>Viewport size across it — what a grid divides into columns.</summary>
        public float ViewportCrossSize { get; private set; }

        /// <summary>Total size of the content along the scroll axis.</summary>
        public abstract float ContentMainSize { get; }

        /// <summary>
        /// How many cells can be on screen at once, worst case — the pool size the list has to reach before
        /// scrolling can outrun it. Worst case, not typical: a pool that is one cell short does not fail
        /// gracefully, it shows a hole exactly when the user is scrolling fastest.
        /// </summary>
        public abstract int MaxConcurrentCells { get; }

        /// <summary>The first item overlapping this scroll offset. Clamped to the item range, so a rubber-band overscroll past either end still asks for a real index.</summary>
        public abstract int FirstIndexAt(float mainOffset);

        /// <summary>Where this item sits in content-local space.</summary>
        public abstract ScrollCellPlacement PlacementOf(int index);

        /// <summary>Re-reads the count and the viewport. Called whenever either changes; concrete layouts do any per-item precomputation in <see cref="OnRebuild"/>.</summary>
        public void Rebuild(int itemCount, float viewportMainSize, float viewportCrossSize)
        {
            ItemCount = Mathf.Max(0, itemCount);

            // A viewport is never legitimately zero, but it reads as zero before the first layout pass. Left
            // as-is it would divide its way into an infinite or NaN cell count.
            ViewportMainSize = Mathf.Max(1f, viewportMainSize);
            ViewportCrossSize = Mathf.Max(1f, viewportCrossSize);

            OnRebuild();
        }

        protected virtual void OnRebuild()
        {
        }

        /// <summary>The scroll offset that brings an item fully into view, given where the list is now. Returns the current offset unchanged when the item is already visible — scrolling a list that did not need scrolling is a jump the player did not ask for.</summary>
        public float OffsetToReveal(int index, float currentOffset)
        {
            if (ItemCount == 0)
            {
                return currentOffset;
            }

            var placement = PlacementOf(Mathf.Clamp(index, 0, ItemCount - 1));
            var maxOffset = Mathf.Max(0f, ContentMainSize - ViewportMainSize);

            if (placement.Main < currentOffset)
            {
                return Mathf.Clamp(placement.Main, 0f, maxOffset);
            }

            var itemEnd = placement.Main + placement.MainSize;
            var viewEnd = currentOffset + ViewportMainSize;

            if (itemEnd > viewEnd)
            {
                return Mathf.Clamp(itemEnd - ViewportMainSize, 0f, maxOffset);
            }

            return currentOffset;
        }
    }
}
