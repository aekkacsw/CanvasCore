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

        public void Bind(int index)
        {
            label.text = "Item #" + index;
            background.color = index % 2 == 0 ? evenColor : oddColor;
        }
    }
}
