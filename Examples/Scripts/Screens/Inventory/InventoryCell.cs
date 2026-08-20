using Aexxa.CanvasCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    public sealed class InventoryCell : MonoBehaviour, IRecycledScrollCell
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color evenColor = new(0.137f, 0.153f, 0.204f, 1f);
        [SerializeField] private Color oddColor = new(0.106f, 0.118f, 0.161f, 1f);

        /// <summary>
        /// A recycled cell is the one case where LocalizedText is the wrong tool: the string changes on every
        /// Bind as the cell is reused for a different row, so it is built here from a key with a {0}
        /// placeholder instead. Note what that buys — languages that put the number first, or write it
        /// differently, need no code change, only a different value in their table.
        /// </summary>
        public void Bind(int index)
        {
            label.text = Localization.Get("inventory.cell.item", index);
            background.color = index % 2 == 0 ? evenColor : oddColor;
        }
    }
}
