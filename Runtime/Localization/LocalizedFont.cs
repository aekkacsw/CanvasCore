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
    /// <para>It carries the language's font <i>size</i> and <i>line spacing</i> too — see
    /// <see cref="Localization.CurrentFontScale"/> and <see cref="Localization.CurrentLineSpacingAdjustment"/>.
    /// Both are per-language constants a layout cannot work out for itself: CJK needs more size than a design
    /// tuned on Latin gives it, and Thai stacks vowels and tone marks above and below the baseline, where tight
    /// line spacing makes them collide with the line above.</para>
    ///
    /// <para>A language with no opinion is a normal case, not a failure: the labels are put back to the font,
    /// size, and spacing their prefab was authored with. That is what makes this safe to add pre-emptively —
    /// adding the component changes nothing at all until some locale actually asks for something.</para>
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
        private float[] _originalFontSizes;
        private float[] _originalLineSpacings;

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
            var scale = Localization.CurrentFontScale;
            var lineSpacing = Localization.CurrentLineSpacingAdjustment;

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

                // Always applied to the authored values, never to the current ones: scaling or adding to what
                // has already been scaled compounds, and three language switches would leave the label
                // unreadable.
                text.fontSize = _originalFontSizes[i] * scale;
                text.lineSpacing = _originalLineSpacings[i] + lineSpacing;
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

            var previousSizes = _originalFontSizes;
            var previousLineSpacings = _originalLineSpacings;

            _texts = target != null ? new[] { target } : GetComponentsInChildren<TMP_Text>(true);
            _originalFonts = new TMP_FontAsset[_texts.Length];
            _originalFontSizes = new float[_texts.Length];
            _originalLineSpacings = new float[_texts.Length];

            for (var i = 0; i < _texts.Length; i++)
            {
                var index = IndexOf(previousTexts, _texts[i]);

                _originalFonts[i] = index >= 0 ? previousFonts[index] : _texts[i].font;
                _originalFontSizes[i] = index >= 0 ? previousSizes[index] : _texts[i].fontSize;
                _originalLineSpacings[i] = index >= 0 ? previousLineSpacings[index] : _texts[i].lineSpacing;
            }
        }

        /// <summary>Where this label sat in the previous scan, or -1 if it is new. Both the font and the size have to come from the same remembered slot, or a rescan would pair one label's font with another's size.</summary>
        private static int IndexOf(TMP_Text[] texts, TMP_Text text)
        {
            if (texts == null)
            {
                return -1;
            }

            for (var i = 0; i < texts.Length; i++)
            {
                if (ReferenceEquals(texts[i], text))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
