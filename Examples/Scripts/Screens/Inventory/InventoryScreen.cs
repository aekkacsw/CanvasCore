using Aexxa.CanvasCore;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Demonstrates RecycledScrollView: SetItemCount(2000) below only ever instantiates as many cell
    /// GameObjects as fit the viewport (plus a small overscan buffer) — check the Hierarchy under
    /// ScrollArea/Viewport/Content while this is open, the child count stays flat no matter how far you scroll.
    ///
    /// <para>It also demonstrates the awkward half of that trade. With no GameObject per item, a gamepad has
    /// nothing to navigate <i>to</i> — so the list is driven by <see cref="RecycledScrollNavigator"/>, which
    /// keeps the selection on an index and lets the cells draw it. Try it: open this screen with a pad or the
    /// arrow keys and hold down.</para>
    /// </summary>
    public sealed class InventoryScreen : UIScreen
    {
        [SerializeField] private Button backButton;
        [SerializeField] private RecycledScrollView scrollView;

        [SerializeField]
        [Tooltip("Optional. Present when the list should be navigable with a gamepad or keyboard.")]
        private RecycledScrollNavigator navigator;

        public override void OnCreated()
        {
            backButton.onClick.AddListener(HandleBack);

            if (navigator != null)
            {
                navigator.Submitted += HandleItemSubmitted;
            }

            scrollView.SetItemCount(2000);
        }

        /// <summary>
        /// Wired in OnCreated, not OnSpawn: this runs once per pooled instance, where OnSpawn runs every time
        /// the screen is shown — subscribing there would add a second handler on the second visit and fire the
        /// toast twice. Same rule as the button above.
        /// </summary>
        private void HandleItemSubmitted(int index)
        {
            UIManager.Instance.Toast<SimpleToast>(Localization.Get("inventory.cell.item", index));
        }

        private void HandleBack()
        {
            UIManager.Instance.HandleBack();
        }
    }
}
