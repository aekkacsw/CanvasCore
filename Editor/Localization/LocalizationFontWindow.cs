using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Answers the question that actually matters once a game has more than one language: <em>can the fonts
    /// in this project draw the translations that are in it?</em>
    ///
    /// This is the failure nobody catches by reading code. Localization does its job perfectly — the right
    /// string reaches the right label — and the screen still fills with □□□□ because the font asset has no
    /// glyph for the script. It is invisible in English, invisible in tests that compare strings, and only
    /// shows up in a screenshot of a language the developer often does not read.
    ///
    /// All the logic lives in LocalizationFontUtility; this is the presentation of it.
    /// </summary>
    public sealed class LocalizationFontWindow : EditorWindow
    {
        private Vector2 _scroll;
        private Font _sourceFont;
        private string _required = string.Empty;
        private Dictionary<TMP_FontAsset, string> _missingByFont;

        // No "&" in the menu path: Unity reads it as the Alt-shortcut marker in a MenuItem string, which is
        // not the same rule as a GUI label (where "&&" is the escape).
        [MenuItem("Tools/CanvasCore/Localization/Font Coverage...", priority = 110)]
        public static void Open()
        {
            var window = GetWindow<LocalizationFontWindow>(false, "Localization Fonts", true);
            window.minSize = new Vector2(460f, 320f);
            window.Refresh();
            window.Show();
        }

        private void OnFocus() => Refresh();

        private void Refresh()
        {
            _required = LocalizationFontUtility.RequiredCharacters();
            _missingByFont = LocalizationFontUtility.FindFontAssets()
                .ToDictionary(font => font, font => LocalizationFontUtility.MissingCharacters(font, _required));
        }

        private void OnGUI()
        {
            if (_missingByFont == null)
            {
                Refresh();
            }

            EditorGUILayout.HelpBox(
                $"Checking every font asset against the {_required.Length} distinct character(s) used across " +
                "your locale tables. A font listed as covered can render every translation you have written " +
                "so far — add new languages or new strings and check again. Characters from external locale " +
                "files count too. Dynamic font assets are checked against their source font file rather than " +
                "against the atlas they have built so far, so the answer does not depend on what has been " +
                "rendered yet, and nothing here writes to an atlas.",
                MessageType.Info);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                foreach (var pair in _missingByFont.OrderBy(p => p.Value.Length))
                {
                    DrawFontRow(pair.Key, pair.Value);
                }
            }

            EditorGUILayout.Space(6);
            DrawCreateSection();
        }

        private void DrawFontRow(TMP_FontAsset font, string missing)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        new GUIContent(missing.Length == 0 ? "✓ " + font.name : "✗ " + font.name),
                        EditorStyles.boldLabel);

                    if (LocalizationFontUtility.IsProjectFallback(font))
                    {
                        EditorGUILayout.LabelField("project fallback", EditorStyles.miniLabel, GUILayout.Width(100f));
                    }
                    else if (missing.Length == 0 && GUILayout.Button(
                                 new GUIContent("Use As Fallback", "Adds this font to TMP Settings' fallback list, so every TMP_Text in the project can borrow its glyphs without any prefab being touched."),
                                 GUILayout.Width(120f)))
                    {
                        LocalizationFontUtility.AddProjectFallback(font);
                        Refresh();
                    }
                }

                EditorGUILayout.LabelField(
                    missing.Length == 0
                        ? "covers every character in your locale tables"
                        : $"missing {missing.Length} character(s): {Truncate(missing, 40)}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawCreateSection()
        {
            EditorGUILayout.LabelField("Create A Font Asset", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drop a .ttf/.otf into the project (an SIL Open Font Licence family such as Noto Sans Thai or " +
                "Sarabun can be redistributed with a published tool — a system font from C:\\Windows\\Fonts " +
                "generally cannot), then pick it here to generate the TMP font asset beside it.",
                MessageType.None);

            _sourceFont = (Font)EditorGUILayout.ObjectField("Source Font", _sourceFont, typeof(Font), false);

            using (new EditorGUI.DisabledScope(_sourceFont == null))
            {
                if (GUILayout.Button("Create TMP Font Asset", GUILayout.Height(26f)))
                {
                    var created = LocalizationFontUtility.CreateFontAsset(_sourceFont);

                    if (created != null)
                    {
                        Selection.activeObject = created;
                        EditorGUIUtility.PingObject(created);
                        Refresh();
                    }
                }
            }
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
