using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Drop this on any TMP_Text (or legacy uGUI Text) to have it show a translated string and re-render
    /// itself whenever the locale changes. This is the component 95% of localized UI needs — no per-screen
    /// code at all: set the key in the Inspector and the label is correct in every language, forever.
    ///
    /// Pooling-safe by construction: it subscribes to Localization.LocaleChanged in OnEnable and drops the
    /// subscription in OnDisable, which is exactly the lifecycle a pooled UIView goes through on
    /// Show/Hide — so a view sitting idle in the pool holds no subscription, and one being shown re-reads its
    /// string on the way in even if the locale changed while it was pooled.
    /// </summary>
    [AddComponentMenu("Canvas Core/Localized Text")]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Key to look up in the active LocaleTableSO — e.g. \"menu.play\".")]
        private string key;

        [SerializeField]
        [Tooltip("TextMeshPro target. Auto-filled from this GameObject when the component is added.")]
        private TMP_Text tmpTarget;

        [SerializeField]
        [Tooltip("Legacy uGUI Text target, for projects not on TextMeshPro. Leave empty when a TMP target is set.")]
        private Text uguiTarget;

        private object[] _args;

        /// <summary>
        /// The key this label shows. Assigning re-renders immediately and clears any format arguments — a new
        /// key generally means a new string with different placeholders, so silently reusing the old arguments
        /// would be the wrong guess. Use SetKey(key, args) to set both at once.
        /// </summary>
        public string Key
        {
            get => key;
            set
            {
                key = value;
                _args = null;
                Refresh();
            }
        }

        private void Reset()
        {
            tmpTarget = GetComponent<TMP_Text>();
            uguiTarget = GetComponent<Text>();
        }

        private void OnEnable()
        {
            Localization.LocaleChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Localization.LocaleChanged -= Refresh;
        }

        /// <summary>Sets key and string.Format arguments together, then re-renders — for labels like "Level {0}".</summary>
        public void SetKey(string localizationKey, params object[] args)
        {
            key = localizationKey;
            _args = args;
            Refresh();
        }

        /// <summary>
        /// Replaces the format arguments for the current key and re-renders. The arguments are kept, so the
        /// label re-formats itself correctly on a later locale change too — which is the whole reason to pass
        /// them here rather than formatting a string yourself and assigning it to the text component.
        /// </summary>
        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        /// <summary>Re-reads the translation and writes it to the target. Called automatically on enable and on locale change; call it yourself only after changing the target component at runtime.</summary>
        public void Refresh()
        {
            var text = _args == null || _args.Length == 0
                ? Localization.Get(key)
                : Localization.Get(key, _args);

            if (tmpTarget != null)
            {
                tmpTarget.text = text;
            }

            if (uguiTarget != null)
            {
                uguiTarget.text = text;
            }
        }
    }
}
