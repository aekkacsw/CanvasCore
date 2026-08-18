using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>Full-screen view. Meant to be shown one-at-a-time per layer via UIManager.Show/Hide.</summary>
    public abstract class UIScreen : UIView
    {
        [Tooltip("Check for the base of a navigation flow (e.g. a HUD or main menu with nothing beneath it). UIManager.HandleBack() will not close a root screen — it returns false instead, so callers can fall through to their own app-level \"exit confirmation\" handling rather than leaving a blank screen behind.")]
        [SerializeField] private bool isRootScreen;

        public bool IsRootScreen => isRootScreen;
    }
}
