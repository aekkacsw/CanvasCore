using System;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Per-item sizes, supplied by a callback. This is what a chat log, a feed, or any list of wrapping text
    /// needs — and it is the reason v2 was not a small job.
    ///
    /// <para><b>Why a prefix sum.</b> With fixed sizes, "which item is at offset 4820?" is a division. With
    /// variable sizes it is a search, and doing it by walking from item 0 on every scroll frame turns a
    /// smooth list into an O(n) loop per frame — invisible at 50 items, a stutter at 5,000. Storing the
    /// running total up to each item makes the array sorted by construction, so the same question is a binary
    /// search: about 13 comparisons for 10,000 items, on any frame, at any scroll position.</para>
    ///
    /// <para><b>What it costs the caller.</b> Every size has to be known up front, at Rebuild, because the
    /// totals cannot be summed otherwise. That is a real constraint and not a hidden one: a list whose item
    /// heights depend on measuring rendered text has to measure it before showing the list, or estimate and
    /// live with the estimate. The alternative — measuring lazily as cells bind — makes the content size
    /// change while the user scrolls, which moves the scrollbar under their thumb.</para>
    /// </summary>
    public sealed class VariableScrollLayout : ScrollLayout
    {
        private readonly Func<int, float> _sizeProvider;

        /// <summary>Running totals: _offsets[i] is where item i starts, and _offsets[ItemCount] is the total size. One longer than the item count, which is what makes the last item's end available without a special case.</summary>
        private float[] _offsets = Array.Empty<float>();

        private float _smallestCell = 1f;

        public VariableScrollLayout(Func<int, float> sizeProvider) =>
            _sizeProvider = sizeProvider ?? throw new ArgumentNullException(nameof(sizeProvider));

        public override float ContentMainSize => _offsets.Length == 0 ? 0f : _offsets[ItemCount];

        /// <summary>Sized by the smallest item, since a viewport full of the shortest rows is the most cells that can ever be on screen at once.</summary>
        public override int MaxConcurrentCells => Mathf.CeilToInt(ViewportMainSize / _smallestCell) + 1;

        protected override void OnRebuild()
        {
            if (_offsets.Length < ItemCount + 1)
            {
                // Grown, never shrunk: a list that is filtered down and back up again would otherwise
                // reallocate on every change.
                _offsets = new float[Mathf.NextPowerOfTwo(ItemCount + 1)];
            }

            _offsets[0] = 0f;
            _smallestCell = float.MaxValue;

            for (var i = 0; i < ItemCount; i++)
            {
                // Clamped away from zero: a provider returning 0 (or a negative, or NaN from a division that
                // has not been guarded) would make two items share an offset, and the binary search below
                // would then have no single answer to give.
                var size = _sizeProvider(i);
                size = float.IsNaN(size) ? 1f : Mathf.Max(0.01f, size);

                _offsets[i + 1] = _offsets[i] + size;
                _smallestCell = Mathf.Min(_smallestCell, size);
            }

            if (ItemCount == 0)
            {
                _smallestCell = 1f;
            }
        }

        /// <summary>Binary search for the last item that starts at or before this offset.</summary>
        public override int FirstIndexAt(float mainOffset)
        {
            if (ItemCount == 0)
            {
                return 0;
            }

            var target = Mathf.Clamp(mainOffset, 0f, ContentMainSize);
            var low = 0;
            var high = ItemCount - 1;

            while (low < high)
            {
                // Biased upward so the loop always moves: with low = high - 1 a downward-biased midpoint
                // would keep picking low and never terminate.
                var mid = (low + high + 1) / 2;

                if (_offsets[mid] <= target)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        public override ScrollCellPlacement PlacementOf(int index)
        {
            if (ItemCount == 0)
            {
                return default;
            }

            var clamped = Mathf.Clamp(index, 0, ItemCount - 1);
            return new ScrollCellPlacement(_offsets[clamped], 0f, _offsets[clamped + 1] - _offsets[clamped], 0f);
        }
    }
}
