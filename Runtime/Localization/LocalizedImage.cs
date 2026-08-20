using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// The <see cref="LocalizedText"/> of images: set the per-language sprites in the Inspector and the Image
    /// follows the active language with no per-screen code. For anything that is not a UI Image — an
    /// AudioClip, a VideoClip, a material — use <see cref="LocalizedAsset{T}"/> directly from your own script;
    /// there is no component for those because there is no single obvious moment to apply them.
    ///
    /// Pooling-safe by the same OnEnable/OnDisable subscription as LocalizedText. A language with no variant
    /// and no default path leaves the sprite exactly as the prefab authored it.
    /// </summary>
    [AddComponentMenu("Canvas Core/Localized Image")]
    [DisallowMultipleComponent]
    public sealed class LocalizedImage : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image to swap. Auto-filled from this GameObject when the component is added.")]
        private Image target;

        [SerializeField]
        private LocalizedAsset<Sprite> sprite = new();

        private Sprite _originalSprite;
        private bool _capturedOriginal;

        private void Reset() => target = GetComponent<Image>();

        private void OnEnable()
        {
            Localization.LocaleChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Localization.LocaleChanged -= Apply;
        }

        /// <summary>Re-reads the sprite for the active language and assigns it.</summary>
        public void Apply()
        {
            if (target == null)
            {
                return;
            }

            // Captured before the first switch, so a language with nothing to say can put back the authored
            // sprite rather than leaving the previous language's showing.
            if (!_capturedOriginal)
            {
                _capturedOriginal = true;
                _originalSprite = target.sprite;
            }

            target.sprite = sprite.Load() ?? _originalSprite;
        }
    }
}
