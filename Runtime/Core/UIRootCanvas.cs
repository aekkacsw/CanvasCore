using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    public sealed class UIRootCanvas : MonoBehaviour
    {
        [SerializeField] private Camera uiCamera;
        [SerializeField] private List<UILayer> layers = new();

        public Camera UICamera => uiCamera;
        public IReadOnlyList<UILayer> Layers => layers;

        public UILayer GetLayer(UILayerId layerId)
        {
            foreach (var layer in layers)
            {
                if (layer.LayerId == layerId)
                {
                    return layer;
                }
            }

            Debug.LogError($"UIRootCanvas: no UILayer registered for '{layerId}'.", this);
            return null;
        }
    }
}
