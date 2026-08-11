using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    [RequireComponent(typeof(Canvas))]
    public sealed class UILayer : MonoBehaviour
    {
        [SerializeField] private UILayerId layerId;
        [SerializeField] private Canvas canvasComponent;
        [SerializeField] private RectTransform container;
        [Tooltip("When true, adding a new view hides the previous one, and removing the top view re-shows the one beneath it (back-stack behaviour). Leave off for layers that hold several independent views at once (Overlay, Toast).")]
        [SerializeField] private bool isStacked;

        private readonly List<UIView> _active = new();

        public UILayerId LayerId => layerId;
        public Transform Container => container != null ? (Transform)container : transform;
        public UIView Top => _active.Count > 0 ? _active[^1] : null;
        public bool IsStacked => isStacked;

        private void Reset()
        {
            canvasComponent = GetComponent<Canvas>();
            container = transform as RectTransform;
        }

        private void Awake()
        {
            if (canvasComponent != null)
            {
                canvasComponent.sortingOrder = (int)layerId;
            }
        }

        public void Add(UIView view)
        {
            if (isStacked && _active.Count > 0)
            {
                _active[^1].Hide();
            }

            view.transform.SetParent(Container, false);
            view.transform.SetAsLastSibling();
            _active.Add(view);
        }

        public void Remove(UIView view)
        {
            var index = _active.LastIndexOf(view);

            if (index < 0)
            {
                return;
            }

            _active.RemoveAt(index);
            view.Hide();

            if (isStacked && index == _active.Count && _active.Count > 0)
            {
                _active[^1].Show();
            }
        }
    }
}
