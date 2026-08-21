using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Mirrors TextMeshPro's "Import TMP Essential Resources": copies the content a consumer is meant to own
    /// and edit — the Design System prefabs, the settings asset, the locale tables, and the whole Examples
    /// folder — into the project's own Assets/.
    ///
    /// <para><b>Why the sources live in folders ending in "~".</b> Unity's AssetDatabase ignores any folder
    /// whose name ends in "~", at any depth, in a package or under Assets/. Nothing inside
    /// <c>PackageResources~/</c> or <c>Samples~/</c> is imported, compiled, given a GUID, reachable through
    /// <c>Resources.Load</c>, or included in a build — the files are just there on disk, versioned in git like
    /// any other source.</para>
    ///
    /// <para>That is the whole fix for a class of bug this package used to have. When the shipped copies were
    /// ordinary assets, a consumer who imported them ended up with <i>two</i> assets at the Resources path
    /// "Localization/en" — theirs and the package's — and Resources.Load does not define which of two
    /// same-path assets it returns. Editor tooling filtered to Assets/ and got theirs; the runtime did not and
    /// could get the package's, so a key picked in the Inspector resolved against the wrong table. Every
    /// "prefer the Assets copy" rule scattered around the codebase was a patch on that duplication. With the
    /// shipped copies invisible, the duplicate cannot exist in the first place — in the Editor or in a
    /// build, where there is no AssetDatabase to disambiguate with.</para>
    ///
    /// <para><b>Why .meta files are copied verbatim.</b> Earlier versions stripped them so the copy would be
    /// given fresh GUIDs (the package's originals were live assets, and two live assets cannot share a GUID),
    /// then had to rewrite every reference between the copied files afterwards. An ignored folder's GUIDs were
    /// never claimed by anything, so the copy can simply keep them: references between the imported assets — a
    /// screen nesting the Button prefab, the bootstrap pointing at the catalog, the settings asset pointing at
    /// the Design System folder — arrive intact, with no rewriting at all.</para>
    ///
    /// <para>Examples/Tests is not shipped, and neither is Tests/: those are part of developing the package,
    /// not something a consumer needs a copy of.</para>
    /// </summary>
    internal static class CanvasCoreImporter
    {
        /// <summary>Where the import lands when CanvasCore is installed as a package. A copy vendored directly under Assets/ imports into itself instead — see ResolveRoots.</summary>
        private const string PackageInstallDestination = "Assets/Plugins/aexxa/CanvasCore";

        private const string PackageResourcesFolder = "PackageResources~";
        private const string SamplesFolder = "Samples~";

        /// <summary>
        /// What gets copied, and where to: sources relative to the package root, destinations relative to the
        /// destination root. StreamingAssets is the one thing that does not land under the destination root —
        /// it has to go to the single path Unity recognises, since a folder of that name anywhere else is just
        /// a folder.
        /// </summary>
        private static readonly (string Source, string Destination, bool UnderDestinationRoot)[] FoldersToImport =
        {
            (PackageResourcesFolder + "/Prefabs", "Prefabs", true),
            (PackageResourcesFolder + "/Resources", "Resources", true),
            (SamplesFolder + "/Examples/Resources", "Examples/Resources", true),
            (SamplesFolder + "/Examples/ScriptableObjects", "Examples/ScriptableObjects", true),
            (SamplesFolder + "/Examples/Scenes", "Examples/Scenes", true),
            (SamplesFolder + "/Examples/Scripts", "Examples/Scripts", true),
            (SamplesFolder + "/Examples/StreamingAssets", "Assets/StreamingAssets", false),
        };

        private static readonly (string Source, string Destination)[] FilesToImport =
        {
            (SamplesFolder + "/Examples/README.md", "Examples/README.md"),
        };

        [MenuItem("Tools/CanvasCore/Import Resources Into Project")]
        internal static void Import()
        {
            if (!ResolveRoots(out var sourceRoot, out var destinationRoot))
            {
                return;
            }

            if (!Directory.Exists(Path.Combine(sourceRoot, PackageResourcesFolder)))
            {
                EditorUtility.DisplayDialog(
                    "CanvasCore",
                    $"Could not find '{PackageResourcesFolder}' inside '{sourceRoot}'. This copy of CanvasCore " +
                    "looks incomplete — reinstalling the package should fix it.",
                    "OK");
                return;
            }

            if (!ConfirmOverwrite(destinationRoot))
            {
                return;
            }

            var files = 0;

            foreach (var (relativeSource, destination, underRoot) in FoldersToImport)
            {
                var source = Path.Combine(sourceRoot, relativeSource);

                if (Directory.Exists(source))
                {
                    CopyTree(source, ToAbsolutePath(underRoot ? destinationRoot + "/" + destination : destination), ref files);
                }
            }

            foreach (var (relativeSource, destination) in FilesToImport)
            {
                var source = Path.Combine(sourceRoot, relativeSource);

                if (File.Exists(source))
                {
                    var absolute = ToAbsolutePath(destinationRoot + "/" + destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                    CopyFileWithMeta(source, absolute, ref files);
                }
            }

            AssetDatabase.Refresh();
            DesignSystemMenuGenerator.ScanAndGenerate();

            Debug.Log(
                $"CanvasCore: imported {files} file(s) into '{destinationRoot}'. These are the only copies in " +
                "the project — the package keeps its originals in folders Unity does not read, so nothing here " +
                "competes with a package asset for the same Resources path. Open " +
                $"'{destinationRoot}/Examples/Scenes/ExampleScene.unity' and press Play to see it running.");
        }

        /// <summary>
        /// Where to copy from, and where to. Installed as a package, the source is the resolved package folder
        /// and the destination is the conventional plugin path. Vendored straight under Assets/ — how this
        /// package's own dev project holds it, and how someone who unzipped it into their project would — both
        /// roots are that folder: the import materialises the ignored folders' content beside them. That is the
        /// same thing a consumer gets, so developing the package exercises the consumer's flow rather than a
        /// special case that only works here.
        /// </summary>
        private static bool ResolveRoots(out string sourceRoot, out string destinationRoot)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CanvasCoreImporter).Assembly);

            if (packageInfo != null)
            {
                sourceRoot = packageInfo.resolvedPath;
                destinationRoot = PackageInstallDestination;
                return true;
            }

            var vendoredRoot = FindVendoredRoot();

            if (vendoredRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "CanvasCore",
                    "Could not work out where CanvasCore is installed — it is not registered as a package, and " +
                    "there is no package.json in any folder above this script. Nothing was copied.",
                    "OK");
                sourceRoot = null;
                destinationRoot = null;
                return false;
            }

            sourceRoot = ToAbsolutePath(vendoredRoot);
            destinationRoot = vendoredRoot;
            return true;
        }

        /// <summary>The Assets-relative folder holding package.json, found by walking up from this script's own asset path.</summary>
        private static string FindVendoredRoot()
        {
            var guid = AssetDatabase.FindAssets($"{nameof(CanvasCoreImporter)} t:MonoScript").FirstOrDefault();

            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var folder = ParentFolder(AssetDatabase.GUIDToAssetPath(guid));

            while (!string.IsNullOrEmpty(folder) && folder != "Assets")
            {
                if (File.Exists(ToAbsolutePath(folder + "/package.json")))
                {
                    return folder;
                }

                folder = ParentFolder(folder);
            }

            return null;
        }

        private static string ParentFolder(string assetPath) =>
            Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

        private static bool ConfirmOverwrite(string destinationRoot)
        {
            var alreadyImported = FoldersToImport
                .Where(entry => entry.UnderDestinationRoot)
                .Select(entry => ToAbsolutePath(destinationRoot + "/" + entry.Destination))
                .Any(path => Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length > 0);

            if (!alreadyImported)
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "CanvasCore",
                $"'{destinationRoot}' already holds an imported copy. Importing again overwrites Prefabs/, " +
                "Resources/, and Examples/ with the package's originals — your own edits inside those specific " +
                "folders would be lost, and so would 'Assets/StreamingAssets/Localization/ja.csv'.\n\n" +
                "Coming from CanvasCore 0.3.0 or earlier: the incoming files carry the package's own asset IDs " +
                "rather than the fresh ones that older import generated, so anything of yours pointing at an " +
                "imported prefab needs repointing once.\n\n" +
                "Continue?",
                "Overwrite", "Cancel");
        }

        /// <summary>
        /// Copies a folder and everything under it, .meta files included, so the copy keeps the GUIDs the
        /// assets were authored with and every reference between them still resolves.
        ///
        /// <para>A folder's own .meta comes along only when this call is what creates the folder. An existing
        /// destination keeps the identity the project already gave it — which matters most for
        /// Assets/StreamingAssets, a folder most projects already have and that importing must not
        /// renumber.</para>
        /// </summary>
        private static void CopyTree(string sourceDir, string destinationDir, ref int files)
        {
            var creating = !Directory.Exists(destinationDir);
            Directory.CreateDirectory(destinationDir);

            if (creating && File.Exists(sourceDir + ".meta"))
            {
                File.Copy(sourceDir + ".meta", destinationDir + ".meta", true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyTree(dir, Path.Combine(destinationDir, Path.GetFileName(dir)), ref files);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (Path.GetExtension(file) != ".meta")
                {
                    CopyFileWithMeta(file, Path.Combine(destinationDir, Path.GetFileName(file)), ref files);
                }
            }
        }

        private static void CopyFileWithMeta(string source, string destination, ref int files)
        {
            File.Copy(source, destination, true);
            files++;

            if (File.Exists(source + ".meta"))
            {
                File.Copy(source + ".meta", destination + ".meta", true);
            }
        }

        private static string ToAbsolutePath(string assetsRelativePath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetsRelativePath);
    }
}
