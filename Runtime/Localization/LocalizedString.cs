using System;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// A translation key stored in a serialized field, for the strings a view holds in data rather than on a
    /// text component — a popup's title picked per call site, a quest description on a ScriptableObject, the
    /// label of an inventory item. Reads as a string wherever a string is expected, resolving through
    /// Localization at the moment it is read (so it is always in the current locale, never a stale copy).
    ///
    /// Its Inspector drawer shows the key alongside a live preview of the translation, and flags a key that
    /// is in no table. Use LocalizedText instead for the ordinary case of a fixed label on a TMP_Text — that
    /// one also re-renders itself when the locale changes, which a plain field obviously cannot.
    /// </summary>
    [Serializable]
    public struct LocalizedString : IEquatable<LocalizedString>
    {
        [SerializeField] private string key;

        public LocalizedString(string key) => this.key = key;

        /// <summary>The raw key. Empty is legal and resolves to an empty string — a field nobody filled in should not shout on screen.</summary>
        public string Key => key;

        /// <summary>The translation in the current locale, via Localization.Get.</summary>
        public string Value => Localization.Get(key);

        /// <summary>Whether the key resolves in the current locale or the fallback — false means Value is showing the missing-key placeholder.</summary>
        public bool IsResolved => Localization.HasKey(key);

        /// <summary>The translation with string.Format arguments applied, via Localization.Get(key, args).</summary>
        public string Format(params object[] args) => Localization.Get(key, args);

        public override string ToString() => Value;

        public static implicit operator string(LocalizedString localized) => localized.Value;

        public bool Equals(LocalizedString other) => string.Equals(key, other.key, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LocalizedString other && Equals(other);

        public override int GetHashCode() => key == null ? 0 : key.GetHashCode();
    }
}
