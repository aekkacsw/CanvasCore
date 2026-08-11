using UnityEngine;

namespace Aexxa.CanvasCore
{
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public abstract class UIView : MonoBehaviour, IPoolable
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        public UILayerId Layer { get; internal set; }
        public bool IsVisible { get; private set; }

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

        public virtual void OnDespawn()
        {
        }
    }
}
