using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Mirrors TextMeshPro's "Import TMP Essential Resources": copies the non-script content a consumer is
    /// meant to own and edit — the Design System prefabs, the settings asset, and the Examples — out of the
    /// package (read-only when installed via git URL) and into the project's own Assets/, at
    /// Assets/Plugins/aexxa/CanvasCore/. Runtime and Editor scripts stay in the package, versioned normally;
    /// only this Assets/ copy is ever scanned by CanvasCoreSettings, UICatalogSO, and the Design System
    /// Create menu (see DesignSystemCreateMenu.DefaultBaseFolder, UICatalogSOEditor's Assets-only search,
    /// and CanvasCoreSettings.LoadPreferringAssetsCopy) — so there's never ambiguity between a package copy
    /// and a project copy.
    ///
    /// Tests/ is deliberately excluded — it's part of developing the package itself, not something a
    /// consumer needs a writable copy of.
    /// </summary>
    internal static class CanvasCoreImporter
    {
        private const string DestinationRoot = "Assets/Plugins/aexxa/CanvasCore";
        private static readonly string[] FoldersToImport = { "Prefabs", "Resources", "Examples" };
        private const string ExcludedExamplesSubfolder = "Tests";

        [MenuItem("Tools/CanvasCore/Import Resources Into Project")]
        internal static void Import()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(CanvasCoreImporter).Assembly);

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
                    "those specific folders would be lost. Continue?",
                    "Overwrite", "Cancel");

                if (!overwrite)
                {
                    return;
                }
            }

            var importedCount = 0;

            foreach (var folder in FoldersToImport)
            {
                var source = Path.Combine(packageInfo.resolvedPath, folder);

                if (!Directory.Exists(source))
                {
                    continue;
                }

                var destination = Path.Combine(destinationAbsolute, folder);
                var excluded = folder == "Examples" ? ExcludedExamplesSubfolder : null;
                importedCount += CopyDirectory(source, destination, excluded);
            }

            AssetDatabase.Refresh();
            DesignSystemMenuGenerator.ScanAndGenerate();

            Debug.Log(
                $"CanvasCore: imported {importedCount} file(s) into '{DestinationRoot}'. " +
                "CanvasCoreSettings, UICatalogSO, and the Design System Create menu all read from this copy now.");
        }

        /// <summary>
        /// Copies file content only — .meta files are deliberately skipped so Unity assigns fresh GUIDs to
        /// the Assets/ copy instead of duplicating the package's own GUIDs into a second, live asset (which
        /// would cause a same-project GUID collision, since the package's originals are still present).
        /// </summary>
        private static int CopyDirectory(string sourceDir, string destinationDir, string excludedSubfolderName)
        {
            Directory.CreateDirectory(destinationDir);
            var count = 0;

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);

                if (dirName == excludedSubfolderName)
                {
                    continue;
                }

                count += CopyDirectory(dir, Path.Combine(destinationDir, dirName), null);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (Path.GetExtension(file) == ".meta")
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
                count++;
            }

            return count;
        }

        private static string ToAbsolutePath(string assetsRelativePath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetsRelativePath);
    }
}
