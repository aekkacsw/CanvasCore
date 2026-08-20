using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Mirrors TextMeshPro's "Import TMP Essential Resources": copies the non-script content a consumer is
    /// meant to own and edit — the Design System prefabs, the settings asset, the locale tables, and the
    /// Examples' prefabs, scene, and sample locale file — out of the package (read-only when installed via
    /// git URL) and into the project's own Assets/. Runtime and Editor scripts stay in the package, versioned
    /// normally; only this Assets/ copy is ever scanned by CanvasCoreSettings, UICatalogSO, and the Design
    /// System Create menu (see DesignSystemCreateMenu.DefaultBaseFolder, UICatalogSOEditor's Assets-only
    /// search, and CanvasCoreSettings.LoadPreferringAssetsCopy) — so there's never ambiguity between a
    /// package copy and a project copy.
    ///
    /// Examples/Scripts is deliberately NOT copied — an .asmdef's assembly name must be unique across the
    /// whole project, so a copy sitting in Assets/ alongside the package's own copy under Packages/ would
    /// collide ("Assembly with name 'Aexxa.CanvasCore.Examples' already exists"). The imported example
    /// prefabs still reference those component scripts fine across the Assets/Packages boundary - only the
    /// prefab/ScriptableObject *data* needs a writable per-project copy, not the compiled behaviour.
    ///
    /// Tests/ is likewise excluded — it's part of developing the package itself, not something a consumer
    /// needs a writable copy of.
    ///
    /// <para><b>Why references are rewritten afterwards.</b> Copying without .meta files is what gives the
    /// Assets/ copy its own fresh GUIDs (see CopyDirectory), but it also means a copied asset's references
    /// still hold the <i>package</i> GUIDs it was authored with — so the imported MainMenuScreen would keep
    /// nesting the package's Button prefab, and the imported UIBootstrap would keep pointing at the package's
    /// catalog. Everything would look right and edits would go nowhere. RemapReferences closes that: every
    /// reference from one copied asset to another is repointed at the copy.</para>
    /// </summary>
    internal static class CanvasCoreImporter
    {
        private const string DestinationRoot = "Assets/Plugins/aexxa/CanvasCore";

        /// <summary>
        /// What gets copied, and where to. Most of it lands under the plugin folder, but StreamingAssets has
        /// to go to the one path Unity recognises — <c>Assets/StreamingAssets</c> — since a folder of that
        /// name anywhere else is just a folder.
        /// </summary>
        private static readonly (string Source, string Destination)[] FoldersToImport =
        {
            ("Prefabs", DestinationRoot + "/Prefabs"),
            ("Resources", DestinationRoot + "/Resources"),
            ("Examples/Resources", DestinationRoot + "/Examples/Resources"),
            ("Examples/ScriptableObjects", DestinationRoot + "/Examples/ScriptableObjects"),
            ("Examples/Scenes", DestinationRoot + "/Examples/Scenes"),
            ("Examples/StreamingAssets", "Assets/StreamingAssets"),
        };

        /// <summary>Files whose contents can hold an asset reference. Copied files of any other kind are left alone.</summary>
        private static readonly HashSet<string> ReferenceBearingExtensions = new()
        {
            ".prefab", ".unity", ".asset", ".mat", ".controller", ".anim",
        };

        private static readonly Regex GuidPattern = new(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        [MenuItem("Tools/CanvasCore/Import Resources Into Project")]
        internal static void Import()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CanvasCoreImporter).Assembly);

            if (packageInfo == null)
            {
                EditorUtility.DisplayDialog(
                    "CanvasCore",
                    "This copy of CanvasCore is already sitting directly under Assets/ (not installed as a " +
                    "package), so there's nothing to import — it's already yours to edit.",
                    "OK");
                return;
            }

            var destinationAbsolute = ToAbsolutePath(DestinationRoot);

            if (Directory.Exists(destinationAbsolute) && Directory.GetFileSystemEntries(destinationAbsolute).Length > 0)
            {
                var overwrite = EditorUtility.DisplayDialog(
                    "CanvasCore",
                    $"'{DestinationRoot}' already has content. Importing again will overwrite Prefabs/, " +
                    "Resources/, and Examples/ with the package's originals — any of your own edits inside " +
                    "those specific folders would be lost. The sample locale file at " +
                    "'Assets/StreamingAssets/Localization/ja.csv' is overwritten too. Continue?",
                    "Overwrite", "Cancel");

                if (!overwrite)
                {
                    return;
                }
            }

            // Source path -> destination asset path, for both files and folders: the folders matter because
            // CanvasCoreSettings points at the Design System folder itself, not at a file inside it.
            var copied = new Dictionary<string, string>();

            foreach (var (relativeSource, destination) in FoldersToImport)
            {
                var source = Path.Combine(packageInfo.resolvedPath, relativeSource);

                if (Directory.Exists(source))
                {
                    CopyDirectory(source, ToAbsolutePath(destination), destination, copied);
                }
            }

            // Refresh first: the copies need to exist as assets before Unity has assigned them the GUIDs the
            // rewrite is about to point everything at.
            AssetDatabase.Refresh();

            var rewritten = RemapReferences(copied);

            AssetDatabase.Refresh();
            DesignSystemMenuGenerator.ScanAndGenerate();

            Debug.Log(
                $"CanvasCore: imported {copied.Count} item(s) into '{DestinationRoot}', repointing references in " +
                $"{rewritten} file(s) at the imported copies. CanvasCoreSettings, UICatalogSO, and the Design " +
                "System Create menu all read from this copy now. Open " +
                $"'{DestinationRoot}/Examples/Scenes/ExampleScene.unity' and press Play to see it running.");
        }

        /// <summary>
        /// Copies file content only — .meta files are deliberately skipped so Unity assigns fresh GUIDs to
        /// the Assets/ copy instead of duplicating the package's own GUIDs into a second, live asset (which
        /// would cause a same-project GUID collision, since the package's originals are still present).
        /// </summary>
        private static void CopyDirectory(
            string sourceDir,
            string destinationDir,
            string destinationAssetPath,
            Dictionary<string, string> copied)
        {
            Directory.CreateDirectory(destinationDir);
            copied[sourceDir] = destinationAssetPath;

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(destinationDir, name), destinationAssetPath + "/" + name, copied);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (Path.GetExtension(file) == ".meta")
                {
                    continue;
                }

                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destinationDir, name), true);
                copied[file] = destinationAssetPath + "/" + name;
            }
        }

        /// <summary>
        /// Repoints every reference between copied assets at the copies, and returns how many files that
        /// touched. References to anything that was <i>not</i> copied — scripts, TMP fonts, Unity's own
        /// built-ins — are left exactly as they are: those live in one place and are meant to be shared.
        /// </summary>
        private static int RemapReferences(Dictionary<string, string> copied)
        {
            var guidMap = new Dictionary<string, string>();

            foreach (var pair in copied)
            {
                var oldGuid = ReadGuid(pair.Key + ".meta");
                var newGuid = AssetDatabase.AssetPathToGUID(pair.Value);

                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid) && oldGuid != newGuid)
                {
                    guidMap[oldGuid] = newGuid;
                }
            }

            if (guidMap.Count == 0)
            {
                return 0;
            }

            var rewritten = 0;

            foreach (var destinationAssetPath in copied.Values)
            {
                if (!ReferenceBearingExtensions.Contains(Path.GetExtension(destinationAssetPath).ToLowerInvariant()))
                {
                    continue;
                }

                var absolute = ToAbsolutePath(destinationAssetPath);

                if (!File.Exists(absolute))
                {
                    continue;
                }

                var text = File.ReadAllText(absolute);
                var replaced = GuidPattern.Replace(
                    text,
                    match => guidMap.TryGetValue(match.Groups[1].Value, out var updated) ? "guid: " + updated : match.Value);

                if (replaced == text)
                {
                    continue;
                }

                File.WriteAllText(absolute, replaced);
                rewritten++;
            }

            return rewritten;
        }

        /// <summary>The GUID Unity assigned an asset in the package, read from the .meta beside it.</summary>
        private static string ReadGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                return null;
            }

            var match = GuidPattern.Match(File.ReadAllText(metaPath));
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ToAbsolutePath(string assetsRelativePath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetsRelativePath);
    }
}
