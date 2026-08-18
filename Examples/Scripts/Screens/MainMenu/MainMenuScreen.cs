using Aexxa.CanvasCore;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    public sealed class MainMenuScreen : UIScreen
    {
        [SerializeField] private Button openSettingsButton;
        [SerializeField] private Button showToastButton;
        [SerializeField] private Button openInventoryButton;

        // Is Root Screen is checked on this prefab — base of navigation, nothing shows beneath it,
        // so back/ESC here should exit-confirm (or no-op) at the app level instead of closing this screen.
        public override void OnCreated()
        {
            openSettingsButton.onClick.AddListener(HandleOpenSettings);
            showToastButton.onClick.AddListener(HandleShowToast);
            openInventoryButton.onClick.AddListener(HandleOpenInventory);
        }

        private void HandleOpenSettings()
        {
            UIManager.Instance.Show<SettingsScreen>();
        }

        private void HandleShowToast()
        {
            UIManager.Instance.Toast<SimpleToast>("Hello from MainMenuScreen!");
        }

        private void HandleOpenInventory()
        {
            UIManager.Instance.Show<InventoryScreen>();
        }
    }
}
