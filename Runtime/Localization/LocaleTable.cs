using System;
using System.Collections.Generic;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// The lookup Localization actually reads at runtime: one language's strings in a plain dictionary, with
    /// no Unity object behind it.
    ///
    /// Separating this from LocaleTableSO buys two things that the asset alone could not. <b>Memory:</b> the
    /// ScriptableObject can be unloaded the moment its contents have been copied in here, so a language costs
    /// one dictionary rather than a dictionary plus a live asset plus its List of entry objects.
    /// <b>Provenance:</b> a table can be built from something that is not an asset at all — a CSV file sitting
    /// next to the built game, which is what lets players and translators add languages after the build.
    /// </summary>
    public sealed class LocaleTable
    {
        private readonly Dictionary<string, string> _entries;

        public LocaleTable(string localeCode, bool isRightToLeft = false, int capacity = 0)
        {
            LocaleCode = localeCode;
            IsRightToLeft = isRightToLeft;
            _entries = new Dictionary<string, string>(capacity, StringComparer.Ordinal);
        }

        public string LocaleCode { get; }

        public bool IsRightToLeft { get; internal set; }

        /// <summary>Resources path of the font this language asks to be drawn with, or empty. Set from the descriptor for a shipped locale, or from the reserved <c>locale.font</c> row for one that arrived as a file.</summary>
        public string FontResourcePath { get; internal set; }

        public int Count => _entries.Count;

        public IReadOnlyDictionary<string, string> Entries => _entries;

        public bool TryGet(string key, out string value) => _entries.TryGetValue(key, out value);

        public bool HasKey(string key) => _entries.ContainsKey(key);

        /// <summary>Adds or replaces one string. Later writes win, which is what makes an external file able to override a shipped translation.</summary>
        public void Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _entries[key] = value;
            }
        }

        /// <summary>Copies every entry of <paramref name="other"/> over this table — the merge step behind "external files override built-in ones".</summary>
        public void OverrideWith(LocaleTable other)
        {
            if (other == null)
            {
                return;
            }

            foreach (var pair in other._entries)
            {
                _entries[pair.Key] = pair.Value;
            }
        }
    }
}
