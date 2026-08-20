using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// The one entry point for translated strings. Static rather than an instance like UIManager, because a
    /// string lookup has no scene dependency — there is nothing for a caller to hold or wire up, and
    /// LocalizedText components on pooled prefabs need to reach it from OnEnable without a reference.
    ///
    /// Everything it needs comes from CanvasCoreSettings.Instance: the list of locales, which one is the
    /// default, which one to fall back to, and how a missing key should render. Initialization is lazy — the
    /// first Get/SetLocale call resolves the startup locale (persisted choice, then system language, then the
    /// configured default) and loads that one language.
    ///
    /// <para><b>Memory.</b> Only the current language and — when configured and different — the fallback are
    /// resident, as plain dictionaries. The LocaleTableSO each one was built from is unloaded the moment it
    /// has been copied (see LoadTable), so a language costs one Dictionary rather than a dictionary plus a
    /// live ScriptableObject plus its List of entry objects. Switching language drops the dictionary of the
    /// one being left behind.</para>
    ///
    /// <para><b>Where a string can come from.</b> The shipped LocaleTableSO first, then any external CSV
    /// found next to the build, which overrides it key by key — see ExternalLocaleFiles. A language that
    /// exists only as an external file is a first-class locale: it appears in AvailableLocales and in any
    /// picker built from it.</para>
    /// </summary>
    public static class Localization
    {
        /// <summary>Where the player's chosen locale is remembered between sessions, when Persist Locale Selection is on.</summary>
        public const string PlayerPrefsKey = "Aexxa.CanvasCore.Locale";

        private static readonly List<LocaleDescriptor> Descriptors = new();
        private static readonly Dictionary<string, LocaleTable> ExternalTables =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;
        private static LocaleDescriptor _currentDescriptor;
        private static LocaleTable _currentTable;
        private static LocaleTable _fallbackTable;
        private static bool _warnedNoLocales;
        private static TMP_FontAsset _currentFont;
        private static bool _fontResolved;

        /// <summary>
        /// Raised after the active locale changes, and after any Reload(). <b>Not</b> raised by the initial
        /// lazy load — nothing can have subscribed at that point, since the subscriber's own first Get is
        /// usually what triggers it. LocalizedText therefore refreshes in OnEnable as well as on this event;
        /// do the same for anything that formats a string once and keeps it.
        /// </summary>
        public static event Action LocaleChanged;

        /// <summary>The active locale's descriptor, or null when no locales are configured at all.</summary>
        public static LocaleDescriptor CurrentLocale
        {
            get
            {
                EnsureInitialized();
                return _currentDescriptor;
            }
        }

        /// <summary>The active locale's code ("en", "th", ...), or an empty string when no locales are configured.</summary>
        public static string CurrentLocaleCode => CurrentLocale?.Code ?? string.Empty;

        /// <summary>
        /// Every available language — the ones listed in CanvasCoreSettings first, in their configured order,
        /// followed by any discovered only as external files. The natural source for a language picker.
        /// </summary>
        public static IReadOnlyList<LocaleDescriptor> AvailableLocales
        {
            get
            {
                EnsureInitialized();
                return Descriptors;
            }
        }

        /// <summary>Whether the active language is written right-to-left. CanvasCore does not mirror any layout itself — this is here so your own layout code can.</summary>
        public static bool IsRightToLeft
        {
            get
            {
                EnsureInitialized();
                return _currentTable != null && _currentTable.IsRightToLeft;
            }
        }

        /// <summary>
        /// The font this language asks to be drawn with, or null for "no opinion, keep the font each label
        /// already has". Comes from the locale's Font Resource Path in CanvasCoreSettings, or from a
        /// <c>locale.font</c> row in an external file, which wins.
        ///
        /// <para>This exists because a TMP fallback font is the wrong tool for some scripts and the right one
        /// for others. A fallback is per-character: TMP draws what the primary font has and borrows the rest,
        /// which is ideal for Thai next to Latin. It is not ideal for Japanese, where borrowing means Latin
        /// text keeps the Western font's metrics while the kana come from elsewhere, and it cannot express
        /// "this language should simply look different". Switching the asset says that outright.</para>
        ///
        /// <para>Loaded once per locale switch and kept — unlike a locale table, a font asset is not something
        /// to unload, because every label using it is holding it.</para>
        /// </summary>
        public static TMP_FontAsset CurrentFont
        {
            get
            {
                EnsureInitialized();

                if (!_fontResolved)
                {
                    _fontResolved = true;
                    _currentFont = LoadFont(CurrentFontPath);
                }

                return _currentFont;
            }
        }

        /// <summary>
        /// Multiplier to apply to label font sizes for the active language, 1 when it has no opinion. Some
        /// scripts simply need more room than a design tuned on Latin gives them — CJK at the size English is
        /// comfortable at is not comfortable — and that is a per-language constant, not something a layout can
        /// work out for itself. Comes from the locale's Font Size Scale, or a <c>locale.fontscale</c> row,
        /// which wins.
        /// </summary>
        public static float CurrentFontScale
        {
            get
            {
                EnsureInitialized();

                if (_currentTable != null && _currentTable.FontSizeScale > 0f)
                {
                    return _currentTable.FontSizeScale;
                }

                return _currentDescriptor?.FontSizeScale ?? 1f;
            }
        }

        /// <summary>
        /// Extra line spacing for the active language, 0 when it has no opinion. Comes from the locale's Line
        /// Spacing Adjustment, or a <c>locale.linespacing</c> row, which wins.
        /// </summary>
        public static float CurrentLineSpacingAdjustment
        {
            get
            {
                EnsureInitialized();

                // NaN is "the file did not say", which has to be distinguishable from a file that said 0 —
                // a translator deliberately cancelling the settings value is a legitimate thing to express.
                if (_currentTable != null && !float.IsNaN(_currentTable.LineSpacingAdjustment))
                {
                    return _currentTable.LineSpacingAdjustment;
                }

                return _currentDescriptor?.LineSpacingAdjustment ?? 0f;
            }
        }

        /// <summary>Resources path of <see cref="CurrentFont"/> before it is loaded, or an empty string when this language does not ask for a font of its own.</summary>
        public static string CurrentFontPath
        {
            get
            {
                EnsureInitialized();

                if (_currentTable != null && !string.IsNullOrEmpty(_currentTable.FontResourcePath))
                {
                    return _currentTable.FontResourcePath;
                }

                return _currentDescriptor?.FontResourcePath ?? string.Empty;
            }
        }

        /// <summary>Translated string for the key, falling back to the fallback locale and then to the configured Missing Key Display.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            EnsureInitialized();

            if (_currentTable != null && _currentTable.TryGet(key, out var value))
            {
                return value;
            }

            if (_fallbackTable != null && _fallbackTable.TryGet(key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return FormatMissing(key);
        }

        /// <summary>
        /// Get(key) with one format argument — "Item #{0}", "Level {0}". The fixed-arity overloads exist to
        /// keep a per-frame caller out of the garbage collector: the params version allocates an object[] on
        /// every single call, which matters when a recycled scroll view formats a label for every visible row
        /// on every scroll frame.
        /// </summary>
        public static string Get(string key, object arg0) => Format(key, Get(key), arg0, null, null, 1);

        /// <summary>Get(key) with two format arguments, without allocating an argument array.</summary>
        public static string Get(string key, object arg0, object arg1) => Format(key, Get(key), arg0, arg1, null, 2);

        /// <summary>Get(key) with three format arguments, without allocating an argument array.</summary>
        public static string Get(string key, object arg0, object arg1, object arg2) => Format(key, Get(key), arg0, arg1, arg2, 3);

        /// <summary>
        /// Get(key) with any number of format arguments. A translation whose placeholders do not line up with
        /// the arguments given (a translator dropping a {1}, say) logs the offending key and returns the raw
        /// unformatted string rather than throwing, so a bad translation can never take a screen down.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            var format = Get(key);

            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException e)
            {
                LogFormatMismatch(key, args.Length, e);
                return format;
            }
        }

        /// <summary>Lookup in the current language only — no fallback, no missing-key decoration. For tooling, and for callers that need to branch on "is this translated in this language at all". Note that HasKey does consult the fallback and this does not.</summary>
        public static bool TryGet(string key, out string value)
        {
            EnsureInitialized();
            value = null;
            return !string.IsNullOrEmpty(key) && _currentTable != null && _currentTable.TryGet(key, out value);
        }

        /// <summary>Whether the key exists in the current language, or failing that in the fallback one.</summary>
        public static bool HasKey(string key)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return (_currentTable != null && _currentTable.HasKey(key))
                   || (_fallbackTable != null && _fallbackTable.HasKey(key));
        }

        /// <summary>
        /// Switches the active language and raises LocaleChanged. Choosing the language that is already
        /// active still records the choice — a player who opens the picker and selects the language they are
        /// already reading has expressed a preference, and it should survive the next launch instead of being
        /// re-decided by system-language detection.
        /// </summary>
        public static void SetLocale(string localeCode)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(localeCode))
            {
                return;
            }

            var descriptor = FindDescriptor(localeCode);

            if (descriptor == null)
            {
                Debug.LogError($"Localization: no locale '{localeCode}' is available — locale unchanged (still '{CurrentLocaleCode}'). Available: {string.Join(", ", CodesOf(Descriptors))}");
                return;
            }

            Persist(descriptor.Code);

            if (ReferenceEquals(descriptor, _currentDescriptor))
            {
                return;
            }

            if (!Activate(descriptor))
            {
                return;
            }

            LocaleChanged?.Invoke();
        }

        /// <summary>
        /// Drops everything loaded and re-resolves from scratch — including re-reading the external locale
        /// files, which is what makes "edit the CSV, press the button, see it in game" possible without a
        /// restart. Then raises LocaleChanged. Ordinary game code never needs this.
        /// </summary>
        public static void Reload()
        {
            _currentTable = null;
            _fallbackTable = null;
            _currentDescriptor = null;
            _currentFont = null;
            _fontResolved = false;
            _initialized = false;
            _warnedNoLocales = false;
            Descriptors.Clear();
            ExternalTables.Clear();

            EnsureInitialized();
            LocaleChanged?.Invoke();
        }

        /// <summary>Forces the lazy startup resolution to run now instead of on the first Get — for a bootstrapper that would rather pay the file reads and table build during a loading screen.</summary>
        public static void Initialize() => EnsureInitialized();

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var settings = CanvasCoreSettings.Instance;
            BuildDescriptorList(settings);

            if (Descriptors.Count == 0)
            {
                if (!_warnedNoLocales)
                {
                    _warnedNoLocales = true;
                    Debug.LogWarning("Localization: no locales available — every Get() will return its key. Add one under Localization in the CanvasCoreSettings inspector.");
                }

                return;
            }

            _fallbackTable = LoadTable(FindDescriptor(settings == null ? null : settings.FallbackLocaleCode));
            Activate(ResolveStartupDescriptor(settings));
        }

        /// <summary>Configured locales first, in their authored order, then any language found only in an external file.</summary>
        private static void BuildDescriptorList(CanvasCoreSettings settings)
        {
            Descriptors.Clear();
            ExternalTables.Clear();

            if (settings != null)
            {
                foreach (var descriptor in settings.Locales)
                {
                    if (descriptor != null && !string.IsNullOrEmpty(descriptor.Code))
                    {
                        Descriptors.Add(descriptor);
                    }
                }
            }

            if (settings == null || !settings.LoadExternalLocales)
            {
                return;
            }

            foreach (var pair in ExternalLocaleFiles.LoadAll(settings))
            {
                ExternalTables[pair.Key] = pair.Value;

                if (FindDescriptor(pair.Key) != null)
                {
                    continue;
                }

                var displayName = ExternalLocaleFiles.ReadDisplayName(pair.Value) ?? pair.Key;
                Descriptors.Add(new LocaleDescriptor(pair.Key, displayName));
            }
        }

        /// <summary>Persisted choice first (the player asked for it once, that stands), then the system language, then the configured default, then whatever is listed first.</summary>
        private static LocaleDescriptor ResolveStartupDescriptor(CanvasCoreSettings settings)
        {
            if ((settings == null || settings.PersistLocaleSelection) && PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                var persisted = FindDescriptor(PlayerPrefs.GetString(PlayerPrefsKey));

                if (persisted != null)
                {
                    return persisted;
                }
            }

            if (settings == null || settings.AutoDetectSystemLanguage)
            {
                foreach (var descriptor in Descriptors)
                {
                    if (descriptor.SystemLanguage != SystemLanguage.Unknown
                        && descriptor.SystemLanguage == Application.systemLanguage)
                    {
                        return descriptor;
                    }
                }
            }

            return FindDescriptor(settings == null ? null : settings.DefaultLocaleCode) ?? Descriptors[0];
        }

        /// <summary>
        /// Makes a language current. Returns false and changes nothing if its table cannot be built — a
        /// failed switch must leave the player reading the language they had, not drop them into a screen of
        /// missing-key placeholders because a path was wrong in one build.
        /// </summary>
        private static bool Activate(LocaleDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return false;
            }

            var table = LoadTable(descriptor);

            if (table == null)
            {
                Debug.LogError($"Localization: could not load locale '{descriptor.Code}' — keeping '{CurrentLocaleCode}'.");
                return false;
            }

            _currentDescriptor = descriptor;
            _currentTable = table;

            // Resolved lazily on the next read rather than here: a locale switch that nothing asks a font of
            // should not pay for loading one.
            _fontResolved = false;
            _currentFont = null;
            return true;
        }

        private static TMP_FontAsset LoadFont(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            var font = Resources.Load<TMP_FontAsset>(resourcePath);

            if (font == null)
            {
                Debug.LogError($"Localization: locale '{CurrentLocaleCode}' asks for the font at Resources path '{resourcePath}', which does not exist — labels keep their current font. The font asset must sit under a folder literally named \"Resources\", and the path carries no file extension.");
            }

            return font;
        }

        /// <summary>
        /// Builds one language's lookup: the shipped asset first, then external files layered over it. The
        /// ScriptableObject is unloaded as soon as its contents have been copied — nothing outside this class
        /// ever holds one, which is what makes that safe.
        /// </summary>
        private static LocaleTable LoadTable(LocaleDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            LocaleTable table = null;

            if (!string.IsNullOrEmpty(descriptor.ResourcePath))
            {
                var asset = Resources.Load<LocaleTableSO>(descriptor.ResourcePath);

                if (asset == null)
                {
                    Debug.LogError($"Localization: no LocaleTableSO at Resources path '{descriptor.ResourcePath}' for locale '{descriptor.Code}'. The asset must sit under a folder literally named \"Resources\", and the path is written without a file extension.");
                }
                else
                {
                    table = asset.CreateRuntimeTable();
                    Resources.UnloadAsset(asset);
                }
            }

            if (ExternalTables.TryGetValue(descriptor.Code, out var external))
            {
                if (table == null)
                {
                    table = new LocaleTable(descriptor.Code, external.IsRightToLeft, external.Count);
                }
                else
                {
                    table.IsRightToLeft = external.IsRightToLeft || table.IsRightToLeft;
                }

                table.OverrideWith(external);

                // A file that names a font overrides the one configured in settings — same layering rule as
                // the strings, so "this language needs a different font" is answerable without a rebuild.
                if (!string.IsNullOrEmpty(external.FontResourcePath))
                {
                    table.FontResourcePath = external.FontResourcePath;
                }

                if (external.FontSizeScale > 0f)
                {
                    table.FontSizeScale = external.FontSizeScale;
                }

                if (!float.IsNaN(external.LineSpacingAdjustment))
                {
                    table.LineSpacingAdjustment = external.LineSpacingAdjustment;
                }
            }

            if (table == null)
            {
                Debug.LogError($"Localization: locale '{descriptor.Code}' has neither a Resource Path nor an external file — nothing to load.");
            }

            return table;
        }

        private static void Persist(string localeCode)
        {
            var settings = CanvasCoreSettings.Instance;

            if (settings != null && !settings.PersistLocaleSelection)
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, localeCode);
            PlayerPrefs.Save();
        }

        private static LocaleDescriptor FindDescriptor(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode))
            {
                return null;
            }

            foreach (var descriptor in Descriptors)
            {
                if (string.Equals(descriptor.Code, localeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return descriptor;
                }
            }

            return null;
        }

        private static string Format(string key, string format, object arg0, object arg1, object arg2, int count)
        {
            try
            {
                return count switch
                {
                    1 => string.Format(format, arg0),
                    2 => string.Format(format, arg0, arg1),
                    _ => string.Format(format, arg0, arg1, arg2),
                };
            }
            catch (FormatException e)
            {
                LogFormatMismatch(key, count, e);
                return format;
            }
        }

        private static void LogFormatMismatch(string key, int argumentCount, FormatException e) =>
            Debug.LogError($"Localization: key '{key}' in locale '{CurrentLocaleCode}' has placeholders that do not match the {argumentCount} argument(s) passed — {e.Message}. Showing the unformatted string.");

        private static string FormatMissing(string key)
        {
            var settings = CanvasCoreSettings.Instance;
            var mode = settings == null ? MissingKeyDisplay.MarkedKey : settings.MissingKeyDisplay;

            return mode switch
            {
                MissingKeyDisplay.Empty => string.Empty,
                MissingKeyDisplay.Key => key,
                _ => $"#{key}#",
            };
        }

        private static IEnumerable<string> CodesOf(IEnumerable<LocaleDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                yield return descriptor.Code;
            }
        }
    }
}
