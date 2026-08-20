using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Exports every locale table to one spreadsheet and reads it back. This is the whole reason the strings
    /// live in per-locale assets rather than in prefabs: a translator gets a single file with one column per
    /// language, works in whatever tool they already use, and hands it back — no Unity licence, no scene
    /// merge conflicts, no re-typing anything into an Inspector.
    ///
    /// Layout is one header row (key, then one column per locale code) and one row per key:
    ///
    ///     key,en,th
    ///     menu.play,Play,เล่น
    ///     hud.score,"Score: {0}","คะแนน: {0}"
    ///
    /// Written as UTF-8 with a BOM, deliberately: without one, Excel on Windows still opens a UTF-8 file as
    /// the system codepage and turns every Thai character into mojibake — the single most common way a
    /// localization round trip gets corrupted.
    /// </summary>
    public static class LocalizationCsvIO
    {
        private const string KeyColumnHeader = "key";
        private const string LastPathPrefKey = "Aexxa.CanvasCore.LastLocalizationCsvPath";

        [MenuItem("Tools/CanvasCore/Localization/Export CSV...", priority = 100)]
        public static void ExportWithDialog()
        {
            var tables = LocalizationEditorUtility.FindAllTables();

            if (tables.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "CanvasCore Localization",
                    "No LocaleTableSO assets found under Assets/. Create one first: Assets > Create > Aexxa > CanvasCore > Locale Table.",
                    "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanel(
                "Export Localization CSV",
                LastDirectory(),
                "Localization.csv",
                "csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Export(tables, path);
            EditorPrefs.SetString(LastPathPrefKey, path);

            Debug.Log($"CanvasCore Localization: exported {LocalizationEditorUtility.AllKeys().Count} key(s) × {tables.Count} locale(s) to '{path}'.");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/CanvasCore/Localization/Import CSV...", priority = 101)]
        public static void ImportWithDialog()
        {
            var tables = LocalizationEditorUtility.FindAllTables();

            if (tables.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "CanvasCore Localization",
                    "No LocaleTableSO assets found under Assets/ to import into. Create a table per language first — the importer matches CSV columns to tables by their Locale Code.",
                    "OK");
                return;
            }

            var path = EditorUtility.OpenFilePanel("Import Localization CSV", LastDirectory(), "csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Merge is the safe default and what an incremental translation pass wants. Replace exists for the
            // case where the CSV is the source of truth and keys have been deliberately deleted from it —
            // without it, a removed key would linger in the asset forever with no way to notice.
            var choice = EditorUtility.DisplayDialogComplex(
                "CanvasCore Localization",
                $"Import '{Path.GetFileName(path)}' into {tables.Count} locale table(s)?\n\n" +
                "Merge — update the keys present in the CSV, leave any other keys already in the tables alone.\n\n" +
                "Replace — make the tables match the CSV exactly, deleting keys the CSV does not contain.",
                "Merge", "Cancel", "Replace");

            if (choice == 1)
            {
                return;
            }

            var text = File.ReadAllText(path, Encoding.UTF8);
            var report = Import(tables, text, replace: choice == 2);

            EditorPrefs.SetString(LastPathPrefKey, path);
            LocalizationEditorUtility.InvalidateCaches();

            Debug.Log($"CanvasCore Localization: imported '{path}' — {report}");
            EditorUtility.DisplayDialog("CanvasCore Localization", report, "OK");
        }

        /// <summary>Writes the tables to a CSV file at <paramref name="path"/>, one column per table, keys sorted so the file diffs cleanly against the last export.</summary>
        public static void Export(IReadOnlyList<LocaleTableSO> tables, string path)
        {
            File.WriteAllText(path, BuildCsv(tables), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        /// <summary>The CSV text for these tables. Split out from the file write so tests can exercise the shape of the output without touching the disk.</summary>
        public static string BuildCsv(IReadOnlyList<LocaleTableSO> tables)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var table in tables)
            {
                foreach (var entry in table.Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.key))
                    {
                        keys.Add(entry.key);
                    }
                }
            }

            var rows = new List<IReadOnlyList<string>>(keys.Count + 1)
            {
                new[] { KeyColumnHeader }.Concat(tables.Select(table => table.LocaleCode)).ToArray(),
            };

            foreach (var key in keys)
            {
                var row = new List<string>(tables.Count + 1) { key };

                foreach (var table in tables)
                {
                    row.Add(table.TryGet(key, out var value) ? value : string.Empty);
                }

                rows.Add(row);
            }

            return LocalizationCsv.Build(rows);
        }

        /// <summary>
        /// Applies CSV text to the given tables, matching columns to tables by locale code. Returns a
        /// human-readable summary — including anything skipped, which is the part worth showing the author:
        /// a column whose locale has no table is far more often a typo in the header than a language nobody
        /// set up yet.
        /// </summary>
        public static string Import(IReadOnlyList<LocaleTableSO> tables, string csvText, bool replace)
        {
            var rows = LocalizationCsv.Parse(csvText);

            if (rows.Count == 0)
            {
                return "The file is empty — nothing was changed.";
            }

            var header = rows[0];

            if (header.Count < 2 || !string.Equals(header[0].Trim(), KeyColumnHeader, StringComparison.OrdinalIgnoreCase))
            {
                return $"The first column of the header row must be \"{KeyColumnHeader}\", followed by one column per locale code. Nothing was changed.";
            }

            // Column index -> table it writes into. Left null for a column no table claims.
            var columnTables = new LocaleTableSO[header.Count];
            var unmatchedColumns = new List<string>();

            for (var column = 1; column < header.Count; column++)
            {
                var localeCode = header[column].Trim();
                var table = tables.FirstOrDefault(candidate => string.Equals(candidate.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase));

                columnTables[column] = table;

                if (table == null && !string.IsNullOrEmpty(localeCode))
                {
                    unmatchedColumns.Add(localeCode);
                }
            }

            var touchedTables = columnTables.Where(table => table != null).Distinct().ToList();

            if (touchedTables.Count == 0)
            {
                return $"No column header matched the Locale Code of any table. Headers found: {string.Join(", ", header.Skip(1))}. Nothing was changed.";
            }

            // Every table is recorded, not just the ones with a column: the key-sync pass below can add rows to
            // a language this CSV says nothing about, and an edit outside the Undo record is an edit the user
            // cannot take back.
            foreach (var table in tables)
            {
                Undo.RecordObject(table, "Import Localization CSV");
            }

            var csvKeys = new HashSet<string>(StringComparer.Ordinal);
            var writtenValues = 0;

            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var key = row.Count > 0 ? row[0].Trim() : string.Empty;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                csvKeys.Add(key);

                for (var column = 1; column < columnTables.Length && column < row.Count; column++)
                {
                    if (columnTables[column] == null)
                    {
                        continue;
                    }

                    columnTables[column].EditorSetValue(key, row[column]);
                    writtenValues++;
                }
            }

            var removedKeys = 0;

            if (replace)
            {
                foreach (var table in touchedTables)
                {
                    var stale = table.Entries
                        .Where(entry => entry != null && !string.IsNullOrEmpty(entry.key) && !csvKeys.Contains(entry.key))
                        .Select(entry => entry.key)
                        .ToList();

                    foreach (var key in stale)
                    {
                        table.EditorRemoveKey(key);
                        removedKeys++;
                    }
                }
            }

            // Hold the project-wide key invariant (see LocalizationEditorUtility.AddKeyToAllTables): a language
            // this CSV had no column for still gains the new keys, empty, instead of quietly falling behind and
            // surfacing as #missing.key# later. Only keys spread this way — values stay strictly per-column.
            var syncedKeys = 0;

            foreach (var table in tables)
            {
                foreach (var key in csvKeys)
                {
                    if (table.HasKey(key))
                    {
                        continue;
                    }

                    table.EditorSetValue(key, string.Empty);
                    syncedKeys++;
                }
            }

            foreach (var table in tables)
            {
                table.EditorSortByKey();
                EditorUtility.SetDirty(table);
            }

            AssetDatabase.SaveAssets();

            // A table read during play mode caches its lookup; the assets just changed underneath it.
            foreach (var table in tables)
            {
                table.InvalidateLookup();
            }

            if (Application.isPlaying)
            {
                Localization.Reload();
            }

            var summary = new StringBuilder();
            summary.Append($"{csvKeys.Count} key(s), {writtenValues} value(s) written across {touchedTables.Count} table(s): ");
            summary.Append(string.Join(", ", touchedTables.Select(table => table.LocaleCode)));

            if (syncedKeys > 0)
            {
                summary.Append($". {syncedKeys} empty row(s) added so every language has every key");
            }

            if (removedKeys > 0)
            {
                summary.Append($". {removedKeys} key(s) not in the CSV were removed (Replace)");
            }

            if (unmatchedColumns.Count > 0)
            {
                summary.Append($". Skipped column(s) with no matching table: {string.Join(", ", unmatchedColumns)}");
            }

            return summary.Append('.').ToString();
        }

        private static string LastDirectory()
        {
            var last = EditorPrefs.GetString(LastPathPrefKey, string.Empty);
            var directory = string.IsNullOrEmpty(last) ? string.Empty : Path.GetDirectoryName(last);

            return string.IsNullOrEmpty(directory) || !Directory.Exists(directory)
                ? Application.dataPath
                : directory;
        }
    }
}
