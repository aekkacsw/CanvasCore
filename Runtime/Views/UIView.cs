using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public abstract class UIView : MonoBehaviour, IPoolable
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        [Tooltip("Control the highlight starts on when this view opens with a gamepad or keyboard. Leave empty and the first interactable control in the hierarchy is used — set it when that guess would be wrong (a Cancel button that happens to come first, a search field nobody wants focused).")]
        [SerializeField] private Selectable firstSelected;

        public UILayerId Layer { get; internal set; }
        public bool IsVisible { get; private set; }

        /// <summary>Where a gamepad or keyboard highlight should land when this view opens. Null means "pick the first interactable control".</summary>
        public Selectable FirstSelected => firstSelected;

        /// <summary>
        /// Whether this view wants the selection when it opens. True for the things a player interacts
        /// with — Screens, Popups, Blockers — and false for the things that merely appear: a toast stealing
        /// the highlight mid-sentence, or a background taking it and never giving it back, are both worse
        /// than no focus management at all.
        /// </summary>
        public virtual bool TakesFocus => true;

        /// <summary>
        /// Whether the view underneath should stop accepting input while this one is up. True for the modal
        /// cases only (see UIPopup, UIBlocker): a Screen opening over a Screen already hides the one below,
        /// so nothing extra is needed there.
        /// </summary>
        public virtual bool BlocksInteractionBelow => false;

        protected virtual void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void OnCreated()
        {
        }

        public virtual void OnSpawn(object context)
        {
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            IsVisible = true;
        }

        public virtual void Hide()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            IsVisible = false;
        }

        /// <summary>
        /// Turns input on or off without touching visibility — how a view stays readable behind a modal while
        /// being unreachable by both pointer and navigation. Deliberately separate from Show/Hide, which own
        /// alpha as well: a view dimmed by a popup must not come back at the wrong opacity when it is
        /// released.
        /// </summary>
        public void SetInteractable(bool value)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = value;
            }
        }

        public virtual void OnDespawn()
        {
        }
    }
}
