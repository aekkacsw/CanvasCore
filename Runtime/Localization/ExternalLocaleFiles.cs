using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Reads locale CSV files that live outside the build's own assets, so translations can be corrected —
    /// and whole languages added — without rebuilding the game.
    ///
    /// The file format is deliberately <b>identical to the editor's Export CSV</b>: one header row of
    /// <c>key</c> plus one column per locale code, one row per key. Export from the editor, hand the file to
    /// a translator, drop it back next to the game, done. A file may carry a locale the build has never heard
    /// of; that language simply appears in the picker.
    ///
    /// <para>Two locations are searched, and the difference between them matters:</para>
    /// <list type="bullet">
    /// <item><b>StreamingAssets</b> ships inside the build and is read-only in practice — the right place for
    /// the translations you author.</item>
    /// <item><b>persistentDataPath</b> is writable and is where a player or modder can actually put a file.
    /// It is read last, so it wins.</item>
    /// </list>
    ///
    /// <para><b>Desktop only, by design.</b> On Android StreamingAssets lives inside the compressed APK and on
    /// WebGL it is a URL, so neither has a directory to enumerate — reaching them would mean UnityWebRequest,
    /// an async path through the whole of Localization, and a manifest file listing what to fetch, all to
    /// serve a feature whose entire audience is people editing text files next to a game they installed.
    /// Those platforms are therefore skipped rather than pretended at, and that is a decision, not a gap: on
    /// them the shipped LocaleTableSO assets are the whole story.</para>
    ///
    /// Precedence overall: built-in asset → StreamingAssets → persistentDataPath. Each layer overrides only
    /// the keys it mentions, so a one-line file fixing a single typo is a legitimate thing to ship.
    /// </summary>
    public static class ExternalLocaleFiles
    {
        /// <summary>Reserved keys a file can use to describe a locale it introduces, rather than to translate anything.</summary>
        public const string DisplayNameKey = "locale.displayname";

        public const string RightToLeftKey = "locale.righttoleft";

        /// <summary>
        /// Resources path of the font to draw this locale with. The font itself has to be in the build — a CSV
        /// can point at a font, not supply one — which makes this most useful for a language the game already
        /// ships a font for, or one whose script an existing font covers.
        /// </summary>
        public const string FontKey = "locale.font";

        /// <summary>Multiplier applied to every label's font size while this locale is active — for a script that needs more room than the design assumed.</summary>
        public const string FontScaleKey = "locale.fontscale";

        /// <summary>Added to every label's line spacing while this locale is active. Added rather than scaled because TMP's line spacing starts at 0 — see LocaleDescriptor.LineSpacingAdjustment.</summary>
        public const string LineSpacingKey = "locale.linespacing";

        private const string KeyColumnHeader = "key";

        /// <summary>Every folder that will be searched, in precedence order (later wins). Folders that do not exist are skipped silently — an absent override file is the normal case, not an error.</summary>
        public static IEnumerable<string> SearchFolders(CanvasCoreSettings settings)
        {
            if (settings == null || !settings.LoadExternalLocales)
            {
                yield break;
            }

            var folderName = string.IsNullOrEmpty(settings.ExternalLocaleFolderName)
                ? "Localization"
                : settings.ExternalLocaleFolderName;

            if (settings.ExternalLocaleSource != ExternalLocaleSource.PersistentDataPathOnly)
            {
                // Application.streamingAssetsPath is a jar:// or http:// URL on Android and WebGL, where
                // Directory.Exists is false and File.ReadAllText cannot work at all. Skipping is honest;
                // pretending would mean silently reading nothing on exactly those platforms.
                var streaming = Path.Combine(Application.streamingAssetsPath, folderName);

                if (Directory.Exists(streaming))
                {
                    yield return streaming;
                }
            }

            if (settings.ExternalLocaleSource != ExternalLocaleSource.StreamingAssetsOnly)
            {
                var persistent = Path.Combine(Application.persistentDataPath, folderName);

                if (Directory.Exists(persistent))
                {
                    yield return persistent;
                }
            }
        }

        /// <summary>
        /// Reads every *.csv in the search folders and returns one merged table per locale code found.
        /// Never throws: a locked, half-written, or malformed file logs a warning and is skipped, because the
        /// alternative is a game that refuses to start because a player edited a text file badly.
        /// </summary>
        public static Dictionary<string, LocaleTable> LoadAll(CanvasCoreSettings settings)
        {
            var tables = new Dictionary<string, LocaleTable>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in SearchFolders(settings))
            {
                string[] files;

                try
                {
                    files = Directory.GetFiles(folder, "*.csv", SearchOption.TopDirectoryOnly);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Localization: could not list external locale files in '{folder}' — {e.Message}");
                    continue;
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    ReadInto(file, tables);
                }
            }

            return tables;
        }

        private static void ReadInto(string file, Dictionary<string, LocaleTable> tables)
        {
            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Localization: could not read external locale file '{file}' — {e.Message}");
                return;
            }

            var rows = LocalizationCsv.Parse(text);

            if (rows.Count < 2)
            {
                Debug.LogWarning($"Localization: external locale file '{file}' has no data rows — ignored.");
                return;
            }

            var header = rows[0];

            if (header.Count < 2 || !string.Equals(header[0].Trim(), KeyColumnHeader, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Localization: external locale file '{file}' must start with a '{KeyColumnHeader}' column followed by one column per locale code — ignored.");
                return;
            }

            // Column index -> the table it feeds. Built once per file rather than per row.
            var columnTables = new LocaleTable[header.Count];

            for (var column = 1; column < header.Count; column++)
            {
                var code = header[column].Trim();

                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (!tables.TryGetValue(code, out var table))
                {
                    table = new LocaleTable(code);
                    tables[code] = table;
                }

                columnTables[column] = table;
            }

            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var key = row.Count > 0 ? row[0].Trim() : string.Empty;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                for (var column = 1; column < columnTables.Length && column < row.Count; column++)
                {
                    var table = columnTables[column];

                    if (table == null)
                    {
                        continue;
                    }

                    // An empty cell means "nothing to say here", not "override with blank" — otherwise a
                    // translator filling in one language would wipe every other language's text for that key.
                    if (string.IsNullOrEmpty(row[column]))
                    {
                        continue;
                    }

                    if (string.Equals(key, RightToLeftKey, StringComparison.OrdinalIgnoreCase))
                    {
                        table.IsRightToLeft = ParseBool(row[column]);
                        continue;
                    }

                    if (string.Equals(key, FontKey, StringComparison.OrdinalIgnoreCase))
                    {
                        table.FontResourcePath = row[column].Trim();
                        continue;
                    }

                    if (string.Equals(key, LineSpacingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        table.LineSpacingAdjustment = float.TryParse(row[column].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var spacing)
                            ? spacing
                            : float.NaN;
                        continue;
                    }

                    if (string.Equals(key, FontScaleKey, StringComparison.OrdinalIgnoreCase))
                    {
                        // Invariant culture: a file written on a machine with a comma decimal separator must
                        // still mean 1.2 here, not 12.
                        table.FontSizeScale = float.TryParse(row[column].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0f
                            ? scale
                            : 0f;
                        continue;
                    }

                    table.Set(key, row[column]);
                }
            }
        }

        /// <summary>The display name a file declares for a locale via the reserved <see cref="DisplayNameKey"/> row, or null.</summary>
        public static string ReadDisplayName(LocaleTable table) =>
            table != null && table.TryGet(DisplayNameKey, out var name) && !string.IsNullOrEmpty(name) ? name : null;

        private static bool ParseBool(string value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
