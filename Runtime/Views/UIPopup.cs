using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Modal view. Meant to be shown one-at-a-time per layer via UIManager.Show/Hide. Show&lt;T&gt;() queues
    /// automatically when another popup is already up (see UIManager.Show&lt;T&gt;()), so a popup's context
    /// (callbacks, object references) may sit unused for a while before it's actually shown — clear any
    /// references to external state in OnDespawn() (see IPoolable), same as you would for a normal close,
    /// so a queued-but-cancelled request (UIManager.ClearPopupQueue()/CancelQueued&lt;T&gt;()) can't later
    /// fire a callback against something that's since become invalid.
    /// </summary>
    public abstract class UIPopup : UIView
    {
        [SerializeField] private Button backdropButton;

        /// <summary>
        /// A popup is modal: the screen behind it keeps its pixels and loses its input. The backdrop already
        /// eats mouse clicks, but keyboard and gamepad navigation walks the whole hierarchy and would step
        /// straight past it — the player would be answering a dialog they cannot see.
        /// </summary>
        public override bool BlocksInteractionBelow => true;

        [Tooltip("Uncheck for popups that require an explicit choice (e.g. a confirm dialog) instead of dismissing on an outside click. Only takes effect if Backdrop Button is assigned.")]
        [SerializeField] private bool closeOnBackdropClick = true;

        public override void OnCreated()
        {
            if (backdropButton != null)
            {
                backdropButton.onClick.AddListener(HandleBackdropClick);
            }
        }

        private void HandleBackdropClick()
        {
            if (closeOnBackdropClick)
            {
                UIManager.Instance.Close(this);
            }
        }
    }
}
