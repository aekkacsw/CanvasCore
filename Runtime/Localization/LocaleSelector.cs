using TMPro;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Turns a TMP_Dropdown into a language picker: fills it with every locale in CanvasCoreSettings, selects
    /// the active one, and switches the game's language when the player changes it. Drop it on the dropdown
    /// and there is nothing else to write — which matters because a settings screen is the one place every
    /// localized game needs this exact widget, and hand-rolling it means re-deriving the same
    /// "populate, sync, subscribe, don't feed back into yourself" dance every time.
    ///
    /// The options are rebuilt on enable rather than once at startup so a locale added to the settings while
    /// the editor is in play mode still shows up, and so the labels are correct even if a table finished
    /// loading after this object was created.
    /// </summary>
    [AddComponentMenu("Canvas Core/Locale Selector")]
    [RequireComponent(typeof(TMP_Dropdown))]
    [DisallowMultipleComponent]
    public sealed class LocaleSelector : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;

        /// <summary>Guards the round trip: setting the dropdown's value in code raises onValueChanged just like a click would, and acting on that would fight the locale we are mid-way through applying.</summary>
        private bool _syncing;

        private void Reset()
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        private void OnEnable()
        {
            if (dropdown == null)
            {
                dropdown = GetComponent<TMP_Dropdown>();
            }

            Rebuild();
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
            Localization.LocaleChanged += SyncSelection;
        }

        private void OnDisable()
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            Localization.LocaleChanged -= SyncSelection;
        }

        /// <summary>Refills the option list from the current locale list. Called on enable; call it yourself only if you add locales at runtime.</summary>
        public void Rebuild()
        {
            var locales = Localization.AvailableLocales;
            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>(locales.Count);

            foreach (var locale in locales)
            {
                if (locale != null)
                {
                    options.Add(new TMP_Dropdown.OptionData(locale.DisplayName));
                }
            }

            _syncing = true;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            _syncing = false;

            SyncSelection();
        }

        private void SyncSelection()
        {
            var locales = Localization.AvailableLocales;
            var current = Localization.CurrentLocaleCode;

            for (var i = 0; i < locales.Count; i++)
            {
                // Case-insensitive, like every other locale-code comparison in the system — a settings entry
                // written "EN" against a stored preference of "en" would otherwise leave the picker showing
                // the wrong row while the game reads the right language.
                if (locales[i] == null || !string.Equals(locales[i].Code, current, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _syncing = true;
                dropdown.SetValueWithoutNotify(i);
                dropdown.RefreshShownValue();
                _syncing = false;
                return;
            }
        }

        private void OnDropdownChanged(int index)
        {
            if (_syncing)
            {
                return;
            }

            var locales = Localization.AvailableLocales;

            if (index >= 0 && index < locales.Count && locales[index] != null)
            {
                Localization.SetLocale(locales[index].Code);
            }
        }
    }
}
