using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Mirrors TextMeshPro's two import commands — "Import TMP Essential Resources" and "Import TMP Examples
    /// &amp; Extras" — because the same two audiences exist here. A project that wants the framework and
    /// nothing else takes Essentials; a project that wants something to read and run takes Examples too.
    ///
    /// <para><b>Essentials</b> (<c>PackageResources~/</c>) is what CanvasCore cannot run without:
    /// <c>UIRoot.prefab</c>, <c>UIBootstrap.prefab</c>, and <c>CanvasCoreSettings</c>. The bootstrap prefab
    /// ships with its <c>Catalog</c> field empty on purpose — it belongs to whoever installs it, and pointing
    /// it at the example catalog would make Essentials depend on Examples.</para>
    ///
    /// <para><b>Examples</b> (<c>Samples~/Examples/</c>) is starter content: the Design System prefabs, the
    /// <c>en</c>/<c>th</c> locale tables, and the example screens, scene, and scripts. Deleting the imported
    /// <c>Examples/</c> folder leaves a working framework. It takes the shipped languages with it, which is
    /// the intent — <c>en</c> and <c>th</c> are sample data, not part of the tool.</para>
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
    /// screen nesting the Button prefab, the scene pointing at the bootstrap, the settings asset pointing at
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
        /// One folder to copy: source relative to the package root, destination relative to the destination
        /// root. <see cref="UnderDestinationRoot"/> is false only for StreamingAssets, which has to go to the
        /// single path Unity recognises — a folder of that name anywhere else is just a folder.
        /// </summary>
        private readonly struct Entry
        {
            public readonly string Source;
            public readonly string Destination;
            public readonly bool UnderDestinationRoot;

            public Entry(string source, string destination, bool underDestinationRoot = true)
            {
                Source = source;
                Destination = destination;
                UnderDestinationRoot = underDestinationRoot;
            }
        }

        /// <summary>What CanvasCore cannot run without.</summary>
        private static readonly Entry[] EssentialFolders =
        {
            new Entry(PackageResourcesFolder + "/Prefabs", "Prefabs"),
            new Entry(PackageResourcesFolder + "/Resources", "Resources"),
        };

        /// <summary>Starter content — safe to skip, and safe to delete afterwards.</summary>
        private static readonly Entry[] ExampleFolders =
        {
            new Entry(SamplesFolder + "/Examples/Prefabs", "Examples/Prefabs"),
            new Entry(SamplesFolder + "/Examples/Resources", "Examples/Resources"),
            new Entry(SamplesFolder + "/Examples/ScriptableObjects", "Examples/ScriptableObjects"),
            new Entry(SamplesFolder + "/Examples/Scenes", "Examples/Scenes"),
            new Entry(SamplesFolder + "/Examples/Scripts", "Examples/Scripts"),
            new Entry(SamplesFolder + "/Examples/StreamingAssets", "Assets/StreamingAssets", false),
        };

        private static readonly Entry[] ExampleFiles =
        {
            new Entry(SamplesFolder + "/Examples/README.md", "Examples/README.md"),
        };

        [MenuItem("Tools/CanvasCore/Import Essential Resources", priority = 1)]
        internal static void ImportEssentials()
        {
            if (!ResolveRoots(out var sourceRoot, out var destinationRoot) || !SourceIsIntact(sourceRoot))
            {
                return;
            }

            if (!ConfirmOverwrite(destinationRoot, EssentialFolders, "Essential Resources"))
            {
                return;
            }

            var files = Copy(sourceRoot, destinationRoot, EssentialFolders, null);
            AssetDatabase.Refresh();

            Debug.Log(
                $"CanvasCore: imported {files} essential file(s) into '{destinationRoot}' — UIRoot, UIBootstrap, " +
                "and CanvasCoreSettings. Drop UIBootstrap into your bootstrap scene and give it your own " +
                "UICatalogSO; its Catalog field ships empty by design. " +
                "'Tools > CanvasCore > Import Examples' adds the Design System prefabs, the en/th locale " +
                "tables, and a scene that already runs.");
        }

        [MenuItem("Tools/CanvasCore/Import Examples", priority = 2)]
        internal static void ImportExamples()
        {
            if (!ResolveRoots(out var sourceRoot, out var destinationRoot) || !SourceIsIntact(sourceRoot))
            {
                return;
            }

            // The example scene instantiates UIBootstrap, which is an Essentials asset. Importing Examples on
            // their own would open a scene with a missing prefab and no obvious reason why, so ask rather than
            // let that happen.
            var alsoEssentials = !HasImported(destinationRoot, EssentialFolders);

            if (alsoEssentials && !EditorUtility.DisplayDialog(
                    "CanvasCore",
                    "The examples build on UIRoot, UIBootstrap and CanvasCoreSettings, which have not been " +
                    "imported yet — the example scene would open with a missing prefab. Import Essential " +
                    "Resources along with them?",
                    "Import Both", "Cancel"))
            {
                return;
            }

            if (!ConfirmOverwrite(destinationRoot, ExampleFolders, "Examples"))
            {
                return;
            }

            var files = alsoEssentials ? Copy(sourceRoot, destinationRoot, EssentialFolders, null) : 0;
            files += Copy(sourceRoot, destinationRoot, ExampleFolders, ExampleFiles);

            AssetDatabase.Refresh();

            Debug.Log(
                $"CanvasCore: imported {files} file(s) into '{destinationRoot}'. Open " +
                $"'{destinationRoot}/Examples/Scenes/ExampleScene.unity' and press Play. To get the Design " +
                "System prefabs onto the GameObject > Canvas Core > Create menu, run " +
                "'Tools > CanvasCore > Scan Create Menu Prefabs' — it writes a script, so it is left for you " +
                "to trigger rather than fired off behind an import.");
        }

        private static int Copy(string sourceRoot, string destinationRoot, Entry[] folders, Entry[] files)
        {
            var copied = 0;

            foreach (var entry in folders)
            {
                var source = Path.Combine(sourceRoot, entry.Source);

                if (Directory.Exists(source))
                {
                    CopyTree(source, ToAbsolutePath(DestinationOf(destinationRoot, entry)), ref copied);
                }
            }

            foreach (var entry in files ?? System.Array.Empty<Entry>())
            {
                var source = Path.Combine(sourceRoot, entry.Source);

                if (File.Exists(source))
                {
                    var absolute = ToAbsolutePath(DestinationOf(destinationRoot, entry));
                    Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                    CopyFileWithMeta(source, absolute, ref copied);
                }
            }

            return copied;
        }

        private static string DestinationOf(string destinationRoot, Entry entry) =>
            entry.UnderDestinationRoot ? destinationRoot + "/" + entry.Destination : entry.Destination;

        private static bool SourceIsIntact(string sourceRoot)
        {
            if (Directory.Exists(Path.Combine(sourceRoot, PackageResourcesFolder)))
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "CanvasCore",
                $"Could not find '{PackageResourcesFolder}' inside '{sourceRoot}'. This copy of CanvasCore " +
                "looks incomplete — reinstalling the package should fix it.",
                "OK");
            return false;
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

        /// <summary>True when any of these folders already has content in the project.</summary>
        private static bool HasImported(string destinationRoot, Entry[] folders) =>
            folders
                .Where(entry => entry.UnderDestinationRoot)
                .Select(entry => ToAbsolutePath(DestinationOf(destinationRoot, entry)))
                .Any(path => Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length > 0);

        private static bool ConfirmOverwrite(string destinationRoot, Entry[] folders, string what)
        {
            if (!HasImported(destinationRoot, folders))
            {
                return true;
            }

            var names = string.Join(", ", folders.Select(entry => entry.Destination + "/"));

            return EditorUtility.DisplayDialog(
                "CanvasCore",
                $"'{destinationRoot}' already holds an imported copy of {what}. Importing again overwrites " +
                $"{names} with the package's originals — your own edits inside those specific folders would be " +
                "lost.\n\n" +
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
