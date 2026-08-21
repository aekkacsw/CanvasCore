using Aexxa.CanvasCore;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Demonstrates back-stack navigation: the Screen layer has isStacked = true (see UIRoot.prefab), so
    /// showing this on top of MainMenuScreen auto-hides it, and closing this via HandleBack() auto-shows it
    /// again — no manual bookkeeping needed on either screen's part.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        [SerializeField] private Button backButton;

        public override void OnCreated()
        {
            backButton.onClick.AddListener(HandleBack);
        }

        private void HandleBack()
        {
            UIManager.Instance.HandleBack();
        }
    }
}
