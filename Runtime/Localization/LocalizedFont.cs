using TMPro;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Swaps the font on TMP_Text components to whatever the active language asks for — see
    /// <see cref="Localization.CurrentFont"/> for where that comes from and why a per-language font is worth
    /// having alongside TMP's fallback list.
    ///
    /// Put one on a screen's root and every label under it follows the language; put one on a single label to
    /// switch only that. Nothing else in the scene needs to know: this is the same "set it in the Inspector
    /// and forget it" arrangement as <see cref="LocalizedText"/>, and it is pooling-safe for the same reason —
    /// it subscribes in OnEnable and drops the subscription in OnDisable, which is exactly the lifecycle a
    /// pooled UIView goes through.
    ///
    /// <para>A language with no font of its own is a normal case, not a failure: the labels are put back to
    /// the fonts their prefab was authored with. That is what makes this safe to add pre-emptively — adding
    /// the component changes nothing at all until some locale actually names a font.</para>
    /// </summary>
    [AddComponentMenu("Canvas Core/Localized Font")]
    [DisallowMultipleComponent]
    public sealed class LocalizedFont : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Leave empty to apply to every TMP_Text under this GameObject, including inactive ones. Set a target to switch only that one label.")]
        private TMP_Text target;

        [SerializeField]
        [Tooltip("Also apply the font to labels created or re-parented after this component woke up. Costs a GetComponentsInChildren per locale change; leave off unless the hierarchy actually changes.")]
        private bool rescanOnLocaleChange;

        private TMP_Text[] _texts;
        private TMP_FontAsset[] _originalFonts;

        private void Awake() => Collect();

        private void OnEnable()
        {
            Localization.LocaleChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Localization.LocaleChanged -= Apply;
        }

        /// <summary>Re-reads the language's font and writes it to every target. Call it yourself after building labels at runtime, or turn on Rescan On Locale Change.</summary>
        public void Apply()
        {
            if (_texts == null || rescanOnLocaleChange)
            {
                Collect();
            }

            var font = Localization.CurrentFont;

            for (var i = 0; i < _texts.Length; i++)
            {
                var text = _texts[i];

                if (text == null)
                {
                    continue;
                }

                // Null font means this language has no opinion — restore what the prefab was authored with
                // rather than leaving it wearing the previous language's font.
                text.font = font != null ? font : _originalFonts[i];
            }
        }

        /// <summary>
        /// Finds the labels and remembers the fonts they started with. The originals are captured once, before
        /// any switch has happened — re-capturing later would record whatever language was active at the time
        /// as the "original", and the prefab's own font would be lost for the rest of the session.
        /// </summary>
        private void Collect()
        {
            var previousTexts = _texts;
            var previousFonts = _originalFonts;

            _texts = target != null ? new[] { target } : GetComponentsInChildren<TMP_Text>(true);
            _originalFonts = new TMP_FontAsset[_texts.Length];

            for (var i = 0; i < _texts.Length; i++)
            {
                _originalFonts[i] = FindRemembered(previousTexts, previousFonts, _texts[i]) ?? _texts[i].font;
            }
        }

        private static TMP_FontAsset FindRemembered(TMP_Text[] texts, TMP_FontAsset[] fonts, TMP_Text text)
        {
            if (texts == null)
            {
                return null;
            }

            for (var i = 0; i < texts.Length; i++)
            {
                if (ReferenceEquals(texts[i], text))
                {
                    return fonts[i];
                }
            }

            return null;
        }
    }
}
