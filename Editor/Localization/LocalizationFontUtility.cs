using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// The font side of localization, kept separate from the window that presents it — same split as
    /// LocalizationCsv / LocalizationCsvIO, and for the same reason: this is the part worth calling from a
    /// script or a test, while the window is just buttons.
    ///
    /// The question it answers is deliberately narrow. Not "does this font support Thai" — a font can support
    /// a script and still miss the one character a translator used — but "which of the characters my
    /// translations actually contain can this font not draw". That set comes from the locale tables
    /// themselves, so it stays correct as languages and strings are added.
    /// </summary>
    public static class LocalizationFontUtility
    {
        /// <summary>
        /// Every distinct printable character appearing in any translation — the exact set the project's
        /// fonts have to cover, no more and no less.
        ///
        /// External locale files count too, and that is not a detail: adding a language by dropping a CSV
        /// next to the game is exactly the moment a script the project's fonts have never seen arrives, and a
        /// coverage check that only read the .asset files would cheerfully report "all clear" for the one
        /// case it exists to catch.
        /// </summary>
        public static string RequiredCharacters()
        {
            var characters = new SortedSet<char>();

            foreach (var table in LocalizationEditorUtility.FindAllTables())
            {
                foreach (var entry in table.Entries)
                {
                    Collect(entry?.value, characters);
                }
            }

            var settings = CanvasCoreSettings.Instance;

            if (settings != null && settings.LoadExternalLocales)
            {
                foreach (var external in ExternalLocaleFiles.LoadAll(settings).Values)
                {
                    foreach (var pair in external.Entries)
                    {
                        Collect(pair.Value, characters);
                    }
                }
            }

            return new string(characters.ToArray());
        }

        private static void Collect(string value, SortedSet<char> characters)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var character in value)
            {
                // Control characters are never rendered, so a font missing them is not a problem.
                if (!char.IsControl(character))
                {
                    characters.Add(character);
                }
            }
        }

        /// <summary>
        /// Which of these characters the font cannot draw. The atlas mode decides where to look, and both
        /// halves of that are easy to get wrong:
        ///
        /// A <b>dynamic</b> font asset starts with an empty character table and rasterises glyphs on demand,
        /// so the obvious HasCharacter(char) check calls a brand-new font — including the Thai font just
        /// imported to fix exactly this problem — missing everything. The question to ask is of the font
        /// *file*, which is what FontEngine.TryGetGlyphIndex does: glyph index 0 is the "not in this font"
        /// answer. TryAddCharacters looks like the natural API here and is not: it *writes* to the atlas, so
        /// it can fail for reasons that have nothing to do with coverage (an atlas texture that is not
        /// readable after a reimport, for one) and then reports every character as missing.
        ///
        /// A <b>static</b> font asset is the opposite case: it can only ever render what was baked into its
        /// atlas, so its own character table is the honest answer even when the source .ttf has far more.
        /// </summary>
        public static string MissingCharacters(TMP_FontAsset font, string required)
        {
            if (font == null || string.IsNullOrEmpty(required))
            {
                return string.Empty;
            }

            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic
                && font.sourceFontFile != null
                && TryLoadFontFace(font.sourceFontFile))
            {
                var absentInFile = new StringBuilder();

                foreach (var character in required)
                {
                    if (!FontEngine.TryGetGlyphIndex(character, out var glyphIndex) || glyphIndex == 0)
                    {
                        absentInFile.Append(character);
                    }
                }

                return absentInFile.ToString();
            }

            var absent = new StringBuilder();

            foreach (var character in required)
            {
                if (!font.HasCharacter(character))
                {
                    absent.Append(character);
                }
            }

            return absent.ToString();
        }

        /// <summary>Point the font engine at a font file for glyph queries. The engine may not have been initialized yet in a fresh editor session, so that is retried once rather than assumed.</summary>
        private static bool TryLoadFontFace(Font source)
        {
            const int QueryPointSize = 90;

            if (FontEngine.LoadFontFace(source, QueryPointSize) == FontEngineError.Success)
            {
                return true;
            }

            FontEngine.InitializeFontEngine();
            return FontEngine.LoadFontFace(source, QueryPointSize) == FontEngineError.Success;
        }

        public static IEnumerable<TMP_FontAsset> FindFontAssets() =>
            AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .Where(font => font != null);

        public static bool IsProjectFallback(TMP_FontAsset font) =>
            font != null && TMP_Settings.fallbackFontAssets != null && TMP_Settings.fallbackFontAssets.Contains(font);

        /// <summary>
        /// Builds a TMP font asset beside the source .ttf, in dynamic atlas mode so glyphs are rasterised on
        /// demand rather than the atlas having to be pre-populated with a whole script's character set.
        /// Returns null (and logs why) if the font file cannot be read.
        /// </summary>
        public static TMP_FontAsset CreateFontAsset(Font source)
        {
            if (source == null)
            {
                return null;
            }

            var sourcePath = AssetDatabase.GetAssetPath(source);
            var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var targetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{source.name} SDF.asset");

            var fontAsset = TMP_FontAsset.CreateFontAsset(source);

            if (fontAsset == null)
            {
                Debug.LogError($"CanvasCore Localization: could not create a TMP font asset from '{source.name}'. The font file may need 'Include Font Data' enabled in its import settings.");
                return null;
            }

            fontAsset.name = source.name + " SDF";
            AssetDatabase.CreateAsset(fontAsset, targetPath);

            // The material and atlas texture are sub-assets of the font asset, not separate files.
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTextures != null)
            {
                foreach (var texture in fontAsset.atlasTextures)
                {
                    texture.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(targetPath);

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetPath);
        }

        /// <summary>
        /// Registers the font in TMP Settings' fallback list. Project-wide is the point: every TMP_Text can
        /// then borrow its glyphs, so adding a script needs no change to any prefab, material, or component.
        /// Written through SerializedObject because the list is exposed read-only at runtime.
        /// </summary>
        public static bool AddProjectFallback(TMP_FontAsset font)
        {
            if (font == null || IsProjectFallback(font))
            {
                return false;
            }

            var settingsGuid = AssetDatabase.FindAssets("t:TMP_Settings").FirstOrDefault();

            if (settingsGuid == null)
            {
                Debug.LogError("CanvasCore Localization: no TMP Settings asset found — run Window > TextMeshPro > Import TMP Essential Resources first.");
                return false;
            }

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(settingsGuid));
            var serialized = new SerializedObject(settings);
            var fallbacks = serialized.FindProperty("m_fallbackFontAssets");

            if (fallbacks == null)
            {
                Debug.LogError("CanvasCore Localization: this version of TextMeshPro does not expose 'm_fallbackFontAssets' — add the font under TMP Settings > Fallback Font Assets by hand.");
                return false;
            }

            var index = fallbacks.arraySize;
            fallbacks.InsertArrayElementAtIndex(index);
            fallbacks.GetArrayElementAtIndex(index).objectReferenceValue = font;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"CanvasCore Localization: '{font.name}' is now a project-wide TMP fallback font.", font);
            return true;
        }
    }
}
