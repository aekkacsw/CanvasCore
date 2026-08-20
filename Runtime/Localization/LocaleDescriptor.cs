using System;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// One row of the locale list in CanvasCoreSettings: what a language is called, which system language
    /// auto-detection should map onto it, and where its LocaleTableSO lives.
    ///
    /// Like UICatalogEntry, this holds no direct LocaleTableSO reference. A settings asset is loaded at
    /// startup (CanvasCoreSettings.Instance) and Unity eager-loads everything reachable from it, so a direct
    /// reference here would pull every language's full translation table into memory on boot — exactly what
    /// the per-locale table split exists to avoid. The path string keeps loading lazy and switchable.
    /// </summary>
    [Serializable]
    public sealed class LocaleDescriptor
    {
        [SerializeField]
        [Tooltip("Identifier used in code and in the CSV column header — e.g. \"en\", \"th\", \"pt-BR\". Must match the Locale Code on the table asset itself.")]
        private string code = "en";

        [SerializeField]
        [Tooltip("Shown to the player in a language picker — write it in its own language (\"ไทย\", not \"Thai\").")]
        private string displayName = "English";

        [SerializeField]
        [Tooltip("Path passed to Resources.Load<LocaleTableSO>(...) — relative to a folder literally named \"Resources\", with no file extension. e.g. \"Localization/en\".")]
        private string resourcePath = "Localization/en";

        [SerializeField]
        [Tooltip("Which Application.systemLanguage picks this locale on first run, when Auto Detect System Language is on. Set several locales to Unknown to opt them out of detection.")]
        private SystemLanguage systemLanguage = SystemLanguage.English;

        [SerializeField]
        [Tooltip("Optional. Resources path of a TMP_FontAsset to switch to while this language is active — e.g. \"Localization/Fonts/NotoSansJP SDF\". Leave empty to keep whatever font each label already uses.")]
        private string fontResourcePath = string.Empty;

        [SerializeField]
        [Tooltip("Multiplies every label's font size while this language is active. 1 leaves sizes alone. Useful where a script needs more room to stay readable — CJK at the size Latin is comfortable at usually is not.")]
        [Min(0.1f)]
        private float fontSizeScale = 1f;

        [SerializeField]
        [Tooltip("Added to every label's TMP Line Spacing while this language is active. 0 leaves it alone. Positive values open the lines up — Thai stacks vowels and tone marks above and below the baseline, and at tight spacing they collide with the line above.")]
        private float lineSpacingAdjustment;

        /// <summary>Serialization needs the parameterless form; Unity fills the fields itself.</summary>
        public LocaleDescriptor()
        {
        }

        /// <summary>
        /// Builds a descriptor at runtime, for a language that exists only as an external file — one a player
        /// or translator dropped in after the game shipped, which by definition has no entry in the settings
        /// asset. Marked <see cref="IsExternal"/> so tooling and error messages can tell the two apart.
        /// </summary>
        public LocaleDescriptor(string code, string displayName, SystemLanguage systemLanguage = SystemLanguage.Unknown)
        {
            this.code = code;
            this.displayName = displayName;
            this.systemLanguage = systemLanguage;
            resourcePath = string.Empty;
            IsExternal = true;
        }

        /// <summary>True when this language came from an external file rather than from the settings asset. Never serialized — an external locale exists only for the session that discovered it.</summary>
        public bool IsExternal { get; private set; }

        public string Code => code;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? code : displayName;

        public string ResourcePath => resourcePath;

        public SystemLanguage SystemLanguage => systemLanguage;

        /// <summary>
        /// Resources path of the font this language should be drawn with, or empty for "leave the labels
        /// alone". A path rather than a TMP_FontAsset reference for the same reason the table is: a direct
        /// reference here would drag every language's font atlas into memory at boot, and a CJK atlas is far
        /// heavier than the strings it draws. See <see cref="Localization.CurrentFont"/>.
        /// </summary>
        public string FontResourcePath => fontResourcePath;

        /// <summary>How much to multiply label font sizes by while this language is active. 1 means "leave them alone", which is the answer for most languages.</summary>
        public float FontSizeScale => fontSizeScale <= 0f ? 1f : fontSizeScale;

        /// <summary>
        /// Added to each label's TMP line spacing while this language is active; 0 leaves it as authored.
        ///
        /// <para><b>Added, not multiplied</b> — and that is not a style choice. TMP's lineSpacing is an
        /// <i>extra</i> gap on top of the font's own metrics and it defaults to <c>0</c>, so a multiplier
        /// would scale zero and do nothing at all on the very labels that need it most.</para>
        /// </summary>
        public float LineSpacingAdjustment => lineSpacingAdjustment;
    }
}
