using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// One table per language — not one table holding every language side by side. The split is deliberate
    /// and follows the same reasoning as UICatalogEntry: Unity keeps whatever it loads resident, so a single
    /// all-languages asset would pull every translation of every string into memory even though a session
    /// only ever displays one language. With one asset per locale, Localization loads exactly the current
    /// locale's table (plus the fallback, when configured) through Resources.Load and unloads the previous
    /// one on switch.
    ///
    /// The asset must live under a folder literally named "Resources", and its LocaleDescriptor in
    /// CanvasCoreSettings points at it by Resources-relative path — never by direct object reference, which
    /// would defeat the whole arrangement.
    /// </summary>
    [CreateAssetMenu(menuName = "Canvas Core/Locale Table", fileName = "LocaleTable")]
    public sealed class LocaleTableSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable identifier used from code and from LocalizedText — e.g. \"menu.play\". Never displayed to the player.")]
            public string key;

            // Deliberately no [TextArea]: its drawer reserves a label line and then draws a scrollable box
            // *below* the rect it is given, so inside LocaleTableSOEditor's one-line-per-key rows the actual
            // text box falls outside the row and only its scrollbar shows. Multi-line values still work fine —
            // they are edited on one line here and survive the CSV round trip.
            [Tooltip("The translated string for this table's locale. May contain {0}, {1}, ... placeholders filled in by Localization.Get(key, args).")]
            public string value;
        }

        [SerializeField]
        [Tooltip("Must match the Code of the LocaleDescriptor in CanvasCoreSettings that points at this asset — that's how the CSV round-trip and the settings inspector pair a table with its locale.")]
        private string localeCode = "en";

        [SerializeField]
        [Tooltip("Right-to-left script (Arabic, Hebrew, ...). Exposed as Localization.IsRightToLeft so layout code can mirror itself; CanvasCore does not flip anything on its own.")]
        private bool isRightToLeft;

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, string> _lookup;

        public string LocaleCode => localeCode;

        public bool IsRightToLeft => isRightToLeft;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(string key, out string value)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(key, out value);
        }

        public bool HasKey(string key)
        {
            BuildLookupIfNeeded();
            return _lookup.ContainsKey(key);
        }

        /// <summary>
        /// Copies this asset's strings into a plain runtime table. Localization calls this and then unloads
        /// the asset immediately: once the dictionary exists, keeping the ScriptableObject — and the List of
        /// Entry objects behind it — resident buys nothing. This is also the seam that lets an external CSV
        /// override a shipped translation without the asset knowing anything about it.
        /// </summary>
        public LocaleTable CreateRuntimeTable()
        {
            var table = new LocaleTable(localeCode, isRightToLeft, entries.Count);

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                if (table.HasKey(entry.key))
                {
                    Debug.LogError($"LocaleTableSO '{name}': duplicate key '{entry.key}' — the first one wins.", this);
                    continue;
                }

                table.Set(entry.key, entry.value);
            }

            return table;
        }

        /// <summary>Editor-side writes (the CSV importer, the inspector) change the list behind the cached lookup — this drops it so the next read rebuilds.</summary>
        public void InvalidateLookup() => _lookup = null;

        private void OnValidate() => _lookup = null;

#if UNITY_EDITOR
        /// <summary>
        /// Bulk edits used by the CSV importer and the table inspector, where going through SerializedProperty
        /// for thousands of rows would be needlessly slow. Compiled out of player builds — and unlike an
        /// editor-only serialized *field*, a method leaves nothing behind in the saved asset, so this is not a
        /// repeat of the UICatalogEntry.editorPrefabRef mistake. Callers own Undo.RecordObject beforehand and
        /// EditorUtility.SetDirty afterwards.
        /// </summary>
        public void EditorSetValue(string entryKey, string entryValue)
        {
            foreach (var entry in entries)
            {
                if (entry != null && string.Equals(entry.key, entryKey, StringComparison.Ordinal))
                {
                    entry.value = entryValue;
                    _lookup = null;
                    return;
                }
            }

            entries.Add(new Entry { key = entryKey, value = entryValue });
            _lookup = null;
        }

        /// <summary>Removes the first entry with this key. Returns whether anything was removed.</summary>
        public bool EditorRemoveKey(string entryKey)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].key, entryKey, StringComparison.Ordinal))
                {
                    entries.RemoveAt(i);
                    _lookup = null;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Renames a key, keeping this table's translation and the entry's position. Refuses (returns false)
        /// when the table already has an entry under the new name rather than merging the two — one of the two
        /// translations would have to lose, and silently picking a winner is exactly the kind of edit nobody
        /// notices until the wrong string ships.
        /// </summary>
        public bool EditorRenameKey(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(newKey) || HasKey(newKey))
            {
                return false;
            }

            foreach (var entry in entries)
            {
                if (entry != null && string.Equals(entry.key, oldKey, StringComparison.Ordinal))
                {
                    entry.key = newKey;
                    _lookup = null;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Sorts entries by key so every locale table lists its strings in the same order — which makes diffs between two languages, and between two versions of one language, actually readable.</summary>
        public void EditorSortByKey()
        {
            entries.Sort((a, b) => string.CompareOrdinal(a?.key, b?.key));
            _lookup = null;
        }

        public void EditorSetLocaleCode(string code)
        {
            localeCode = code;
        }
#endif

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, string>(entries.Count, StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                if (!_lookup.TryAdd(entry.key, entry.value))
                {
                    Debug.LogError($"LocaleTableSO '{name}': duplicate key '{entry.key}' — the first one wins.", this);
                }
            }
        }
    }
}
