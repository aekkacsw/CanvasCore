using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Shared lookups for every localization editor surface — the table inspector, the LocalizedText key
    /// picker, the LocalizedString drawer, the CSV importer, and the Localization section of the settings
    /// inspector all need the same handful of answers: which tables exist, which keys exist, and what a key
    /// looks like in the authoring language.
    ///
    /// Tables are searched under Assets/ only, matching the convention the rest of CanvasCore already follows
    /// (see CanvasCoreImporter): a copy sitting read-only inside the package is never the one a project edits.
    ///
    /// Results are cached because the key list gets rebuilt on every repaint of every drawer showing a key
    /// field — an unfiltered AssetDatabase search per repaint is noticeable on a large project. The cache is
    /// dropped whenever an asset is imported, deleted, or moved (see the AssetPostprocessor at the bottom),
    /// so it cannot go stale behind the author's back.
    /// </summary>
    public static class LocalizationEditorUtility
    {
        public const string ResourcesMarker = "/Resources/";

        private static LocaleTableSO[] _tableCache;
        private static string[] _keyCache;

        /// <summary>Every LocaleTableSO under Assets/, ordered to match the locale list in CanvasCoreSettings where possible so the CSV columns come out in the order the project thinks in.</summary>
        public static IReadOnlyList<LocaleTableSO> FindAllTables()
        {
            if (_tableCache != null)
            {
                return _tableCache;
            }

            var tables = AssetDatabase.FindAssets("t:LocaleTableSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<LocaleTableSO>)
                .Where(table => table != null)
                .ToList();

            var settings = CanvasCoreSettings.Instance;

            if (settings != null)
            {
                var order = settings.Locales
                    .Where(descriptor => descriptor != null)
                    .Select(descriptor => descriptor.Code)
                    .ToList();

                tables = tables
                    .OrderBy(table =>
                    {
                        var index = order.FindIndex(code => string.Equals(code, table.LocaleCode, StringComparison.OrdinalIgnoreCase));
                        return index < 0 ? int.MaxValue : index;
                    })
                    .ThenBy(table => table.LocaleCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            _tableCache = tables.ToArray();
            return _tableCache;
        }

        /// <summary>The table whose Locale Code matches, or null. Case-insensitive, like every other locale-code comparison in CanvasCore.</summary>
        public static LocaleTableSO FindTable(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode))
            {
                return null;
            }

            foreach (var table in FindAllTables())
            {
                if (string.Equals(table.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }
            }

            return null;
        }

        /// <summary>The union of every key across every table, sorted — the source list for key pickers.</summary>
        public static IReadOnlyList<string> AllKeys()
        {
            if (_keyCache != null)
            {
                return _keyCache;
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var table in FindAllTables())
            {
                foreach (var entry in table.Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.key))
                    {
                        keys.Add(entry.key);
                    }
                }
            }

            _keyCache = keys.ToArray();
            return _keyCache;
        }

        /// <summary>Whether any table defines this key at all — what a key field uses to warn about a typo before it ships as a #missing.key# on screen.</summary>
        public static bool KeyExists(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            foreach (var table in FindAllTables())
            {
                if (table.HasKey(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What this key reads as in the authoring language — the default locale's table, falling back to the
        /// fallback locale's, then to whichever table happens to have the key. This is the string shown beside
        /// a key field in the Inspector, so an author picking keys sees words rather than identifiers.
        /// </summary>
        public static string PreviewValue(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var settings = CanvasCoreSettings.Instance;

            if (settings != null)
            {
                if (TryPreviewFrom(FindTable(settings.DefaultLocaleCode), key, out var fromDefault))
                {
                    return fromDefault;
                }

                if (TryPreviewFrom(FindTable(settings.FallbackLocaleCode), key, out var fromFallback))
                {
                    return fromFallback;
                }
            }

            foreach (var table in FindAllTables())
            {
                if (TryPreviewFrom(table, key, out var fromAny))
                {
                    return fromAny;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// The invariant every one of these operations exists to hold: <strong>a key belongs to the project,
        /// not to one language.</strong> Every table has an entry for every key, and a language that has not
        /// been translated yet says so with an empty value rather than by lacking the row.
        ///
        /// Letting key sets drift between tables is the failure mode this prevents: nothing complains at edit
        /// time, and the missing language only shows itself as a <c>#some.key#</c> on screen — usually in the
        /// language the person who added the key does not play in. So adding, renaming, and deleting a key all
        /// apply to every table at once; only the translated *value* is ever per-language.
        /// </summary>
        public static void AddKeyToAllTables(string key) => AddKeyToTables(FindAllTables(), key);

        /// <summary>Adds a key to the given tables. Existing values are never overwritten; tables that lack the key get an empty string, which the table inspector then flags as untranslated.</summary>
        public static void AddKeyToTables(IReadOnlyList<LocaleTableSO> tables, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            Apply(tables, "Add Localization Key", table =>
            {
                if (table.HasKey(key))
                {
                    return false;
                }

                table.EditorSetValue(key, string.Empty);
                return true;
            });
        }

        /// <summary>Renames a key across every table, so a key stays one thing project-wide instead of becoming two half-populated ones.</summary>
        public static void RenameKeyInAllTables(string oldKey, string newKey) => RenameKeyInTables(FindAllTables(), oldKey, newKey);

        /// <summary>Renames in the given tables. A table that does not have the old key gets the new one added empty, so the rename cannot leave a language behind.</summary>
        public static void RenameKeyInTables(IReadOnlyList<LocaleTableSO> tables, string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(newKey) || string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                return;
            }

            Apply(tables, "Rename Localization Key", table =>
            {
                if (table.EditorRenameKey(oldKey, newKey))
                {
                    return true;
                }

                if (table.HasKey(newKey))
                {
                    // Already under the new name (this is the table the rename was typed into, or one where
                    // both names existed) — drop the stale row rather than leaving a duplicate behind.
                    return table.EditorRemoveKey(oldKey);
                }

                table.EditorSetValue(newKey, string.Empty);
                return true;
            });
        }

        /// <summary>Deletes a key from every table.</summary>
        public static void RemoveKeyFromAllTables(string key) => RemoveKeyFromTables(FindAllTables(), key);

        /// <summary>Deletes a key from the given tables.</summary>
        public static void RemoveKeyFromTables(IReadOnlyList<LocaleTableSO> tables, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            Apply(tables, "Remove Localization Key", table => table.EditorRemoveKey(key));
        }

        /// <summary>Every table other than <paramref name="excluded"/> — for the inspector, which edits its own table through SerializedProperty (so the change is undoable with the rest of the Inspector) and only propagates to the others.</summary>
        public static IReadOnlyList<LocaleTableSO> TablesOtherThan(LocaleTableSO excluded)
        {
            var others = new List<LocaleTableSO>();

            foreach (var table in FindAllTables())
            {
                if (!ReferenceEquals(table, excluded))
                {
                    others.Add(table);
                }
            }

            return others;
        }

        /// <summary>Which of the given tables hold a non-empty translation for this key — what a delete is about to destroy, and therefore what is worth asking about before doing it.</summary>
        public static List<string> LocalesWithTranslation(IReadOnlyList<LocaleTableSO> tables, string key)
        {
            var locales = new List<string>();

            if (string.IsNullOrEmpty(key))
            {
                return locales;
            }

            foreach (var table in tables)
            {
                if (table != null && table.TryGet(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    locales.Add(table.LocaleCode);
                }
            }

            return locales;
        }

        /// <summary>Runs one mutation over a set of tables with the Undo/dirty/save/cache bookkeeping each of them needs, saving only if something actually changed.</summary>
        private static void Apply(IReadOnlyList<LocaleTableSO> tables, string undoLabel, Func<LocaleTableSO, bool> mutate)
        {
            var changed = false;

            foreach (var table in tables)
            {
                if (table == null)
                {
                    continue;
                }

                Undo.RecordObject(table, undoLabel);

                if (!mutate(table))
                {
                    continue;
                }

                EditorUtility.SetDirty(table);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            AssetDatabase.SaveAssets();
            InvalidateCaches();
        }

        /// <summary>
        /// The path Resources.Load wants for an asset — the part after the last "/Resources/", extension
        /// stripped — or an empty string when the asset is not under a Resources folder at all. Locale tables
        /// have to be loadable this way, so the settings inspector uses this to fill in a descriptor's path
        /// from a dragged asset, and to warn when a table is sitting somewhere it can never be loaded from.
        /// </summary>
        public static string ToResourcesPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            var markerIndex = assetPath.LastIndexOf(ResourcesMarker, StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                return string.Empty;
            }

            var relative = assetPath.Substring(markerIndex + ResourcesMarker.Length);
            var directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(relative);

            return string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}";
        }

        /// <summary>Whether CanvasCoreSettings has a locale row pointing at this table's locale code — a table nobody registered is invisible to Localization at runtime, however complete its translations are.</summary>
        public static bool IsRegisteredInSettings(LocaleTableSO table)
        {
            var settings = CanvasCoreSettings.Instance;

            if (settings == null || table == null)
            {
                return false;
            }

            foreach (var descriptor in settings.Locales)
            {
                if (descriptor != null && string.Equals(descriptor.Code, table.LocaleCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a locale row for this table to CanvasCoreSettings, filling in the code, a starting display
        /// name, and the Resources path derived from where the asset actually sits. Written through
        /// SerializedObject rather than the runtime fields so the change is undoable and saved like any other
        /// Inspector edit.
        /// </summary>
        public static void RegisterInSettings(LocaleTableSO table)
        {
            var settings = CanvasCoreSettings.Instance;

            if (settings == null)
            {
                Debug.LogError("CanvasCore: no CanvasCoreSettings asset found — run Tools > CanvasCore > Import Essential Resources first.");
                return;
            }

            if (table == null || IsRegisteredInSettings(table))
            {
                return;
            }

            RegisterInSettings(
                table.LocaleCode,
                table.LocaleCode,
                ToResourcesPath(AssetDatabase.GetAssetPath(table)),
                SystemLanguage.Unknown);
        }

        /// <summary>Adds a fully specified locale row. The system language is written through intValue rather than enumValueIndex — for an enum whose numbering ever stops matching its declaration order, the index is quietly the wrong number.</summary>
        public static void RegisterInSettings(string code, string displayName, string resourcePath, SystemLanguage systemLanguage)
        {
            var settings = CanvasCoreSettings.Instance;

            if (settings == null)
            {
                Debug.LogError("CanvasCore: no CanvasCoreSettings asset found — run Tools > CanvasCore > Import Essential Resources first.");
                return;
            }

            var serialized = new SerializedObject(settings);
            var locales = serialized.FindProperty("locales");
            var index = locales.arraySize;

            locales.InsertArrayElementAtIndex(index);

            var descriptor = locales.GetArrayElementAtIndex(index);
            descriptor.FindPropertyRelative("code").stringValue = code;
            descriptor.FindPropertyRelative("displayName").stringValue = string.IsNullOrEmpty(displayName) ? code : displayName;
            descriptor.FindPropertyRelative("resourcePath").stringValue = resourcePath;
            descriptor.FindPropertyRelative("systemLanguage").intValue = (int)systemLanguage;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Drops the cached table and key lists. Called automatically on any asset change; call it directly after editing a table through code in the same frame.</summary>
        public static void InvalidateCaches()
        {
            _tableCache = null;
            _keyCache = null;
        }

        private static bool TryPreviewFrom(LocaleTableSO table, string key, out string value)
        {
            value = null;
            return table != null && table.TryGet(key, out value) && !string.IsNullOrEmpty(value);
        }

        private sealed class CacheInvalidator : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                InvalidateCaches();
            }
        }
    }
}
