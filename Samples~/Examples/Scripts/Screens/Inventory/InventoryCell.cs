using Aexxa.CanvasCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    public sealed class InventoryCell : MonoBehaviour, IRecycledScrollCell, IRecycledScrollCellSelection
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color evenColor = new(0.137f, 0.153f, 0.204f, 1f);
        [SerializeField] private Color oddColor = new(0.106f, 0.118f, 0.161f, 1f);

        [SerializeField]
        [Tooltip("Row colour while the gamepad/keyboard highlight is on this item.")]
        private Color selectedColor = new(0.30f, 0.49f, 0.94f, 1f);

        private int _index;
        private bool _isSelected;

        /// <summary>
        /// A recycled cell is the one case where LocalizedText is the wrong tool: the string changes on every
        /// Bind as the cell is reused for a different row, so it is built here from a key with a {0}
        /// placeholder instead. Note what that buys — languages that put the number first, or write it
        /// differently, need no code change, only a different value in their table.
        /// </summary>
        public void Bind(int index)
        {
            _index = index;
            label.text = Localization.Get("inventory.cell.item", index);
            ApplyColour();
        }

        /// <summary>
        /// Draws the highlight, told by <see cref="RecycledScrollNavigator"/> rather than by a Selectable of
        /// this cell's own — see IRecycledScrollCellSelection for why a virtualized list cannot hold its
        /// selection on a GameObject.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyColour();
        }

        /// <summary>
        /// Both callers land here, and that is the point: a cell can be re-bound to a new row <i>while</i> it
        /// is the selected one (scrolling with the pad does exactly that), so neither "which row am I" nor
        /// "am I selected" may own the colour alone.
        /// </summary>
        private void ApplyColour()
        {
            background.color = _isSelected ? selectedColor : _index % 2 == 0 ? evenColor : oddColor;
        }
    }
}
