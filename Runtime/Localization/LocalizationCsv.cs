using System.Collections.Generic;
using System.Text;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// RFC 4180 CSV reading and writing, with no Unity types anywhere in it — the file format is the part
    /// most likely to meet hostile input (a translator's spreadsheet round-tripped through Excel, Google
    /// Sheets, or a text editor that "helpfully" reflows quotes), so it is kept separate from the asset
    /// plumbing in LocalizationCsvIO and covered directly by edit-mode tests.
    ///
    /// This lives in the runtime assembly rather than the editor one because the same file format is read in
    /// both places: the editor imports a translator's spreadsheet, and the built game reads external locale
    /// files shipped beside it. One parser means a file that round-trips in the editor cannot behave
    /// differently in the player.
    ///
    /// Quoted fields are what make this worth writing carefully rather than String.Split(','): translated UI
    /// strings routinely contain commas, and multi-line strings and literal quotation marks both show up in
    /// real games. All three survive a round trip here.
    /// </summary>
    public static class LocalizationCsv
    {
        /// <summary>Quotes a single field when it needs it, doubling any embedded quotation mark. A field that needs no quoting is returned unchanged, so a hand-written CSV stays readable.</summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            var needsQuotes = field.IndexOf(',') >= 0
                              || field.IndexOf('"') >= 0
                              || field.IndexOf('\n') >= 0
                              || field.IndexOf('\r') >= 0;

            if (!needsQuotes)
            {
                return field;
            }

            return string.Concat("\"", field.Replace("\"", "\"\""), "\"");
        }

        /// <summary>Joins rows into CSV text with CRLF line endings — what every spreadsheet application writes, and what every one of them reads back.</summary>
        public static string Build(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();

            foreach (var row in rows)
            {
                for (var i = 0; i < row.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(Escape(row[i]));
                }

                builder.Append("\r\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Parses CSV text into rows of fields. Handles quoted fields containing commas, doubled quotes, and
        /// newlines; accepts CRLF, LF, or CR line endings; strips a UTF-8 BOM; and skips a trailing empty
        /// line. Never throws on malformed input — an unterminated quote simply runs to the end of the text,
        /// which surfaces as an obviously wrong last row rather than an exception mid-import.
        /// </summary>
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();

            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            if (text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c != '"')
                    {
                        field.Append(c);
                        continue;
                    }

                    // A doubled quote inside a quoted field is one literal quote; a single one ends the field.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;

                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;

                    case '\r':
                    case '\n':
                        // Only a line break outside quotes ends the row. CRLF counts once.
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }

                        row.Add(field.ToString());
                        field.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;

                    default:
                        field.Append(c);
                        break;
                }
            }

            // Whatever is still buffered is the final row, unless the text ended cleanly on a line break.
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
}
