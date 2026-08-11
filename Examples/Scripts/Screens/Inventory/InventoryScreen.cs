using Aexxa.CanvasCore;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Demonstrates RecycledScrollView: SetItemCount(2000) below only ever instantiates as many cell
    /// GameObjects as fit the viewport (plus a small overscan buffer) — check the Hierarchy under
    /// ScrollArea/Viewport/Content while this is open, the child count stays flat no matter how far you scroll.
    /// </summary>
    public sealed class InventoryScreen : UIScreen
    {
        [SerializeField] private Button backButton;
        [SerializeField] private RecycledScrollView scrollView;

        public override void OnCreated()
        {
            backButton.onClick.AddListener(HandleBack);
            scrollView.SetItemCount(2000);
        }

        private void HandleBack()
        {
            UIManager.Instance.HandleBack();
        }
    }
}
