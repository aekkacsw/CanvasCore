using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Finds where every localization key is actually used, and therefore which keys are used but not
    /// translated — and which are translated but used nowhere.
    ///
    /// Both halves cost something real. A key used but missing from the tables ships as <c>#some.key#</c> on
    /// screen, usually spotted first by a player. A key nobody uses is a line every translator was paid to
    /// translate, in every language, forever — the cheapest thing in the project to remove and the easiest to
    /// never notice.
    ///
    /// <para><b>What is scanned.</b> Keys reach the runtime by two quite different routes and both are
    /// followed:</para>
    /// <list type="bullet">
    /// <item><b>Code</b> — string literals passed to <c>Localization.Get/HasKey/TryGet</c>, to
    /// <c>SetKey</c>, or to a <c>new LocalizedString(...)</c>, read straight out of the .cs files.</item>
    /// <item><b>Serialized data</b> — the key typed into a <see cref="LocalizedText"/> field, and any
    /// <see cref="LocalizedString"/> in any component or ScriptableObject, across prefabs, scenes, and
    /// assets. This is the half a naive "grep for Get(" would miss entirely, and in a UI project it is
    /// usually the larger half.</item>
    /// </list>
    ///
    /// <para><b>What it cannot see</b>, and why that is stated rather than hidden: a key assembled at runtime
    /// (<c>Get("item." + id)</c>) is invisible to any static scan. So <c>Unused</c> means "no use was found",
    /// not "no use exists" — which is exactly why nothing here deletes anything. It reports; the decision to
    /// remove a key stays with the person who knows whether some string is built at runtime.</para>
    /// </summary>
    public static class LocalizationKeyUsage
    {
        /// <summary>
        /// Literals handed to a localization call. Deliberately narrow: it matches the call shapes CanvasCore
        /// actually offers rather than any string anywhere, so a UI label that merely looks like a key is not
        /// mistaken for one. Verbatim strings (<c>@"..."</c>) are not matched — a key with a backslash in it
        /// is not a thing.
        /// </summary>
        private static readonly Regex CallPattern = new(
            @"(?:Localization\s*\.\s*(?:Get|HasKey|TryGet)|\.\s*SetKey|new\s+LocalizedString)\s*\(\s*""((?:[^""\\]|\\.)*)""",
            RegexOptions.Compiled);

        /// <summary>Keys that describe a locale rather than translate anything — never "unused", whoever does or does not mention them.</summary>
        private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ExternalLocaleFiles.DisplayNameKey,
            ExternalLocaleFiles.RightToLeftKey,
            ExternalLocaleFiles.FontKey,
        };

        /// <summary>What a scan found. Sources are kept per key so a report can say <em>where</em>, which is the difference between a list to act on and a list to argue with.</summary>
        public sealed class Report
        {
            public readonly Dictionary<string, List<string>> Sources = new(StringComparer.Ordinal);
            public readonly List<string> UnusedKeys = new();
            public readonly List<string> MissingKeys = new();

            public int TableKeyCount;
            public int ScannedScripts;
            public int ScannedPrefabs;
            public int ScannedScenes;
            public int ScannedAssets;

            public int UsedKeyCount => Sources.Count;

            public void Add(string key, string source)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }

                if (!Sources.TryGetValue(key, out var sources))
                {
                    sources = new List<string>();
                    Sources[key] = sources;
                }

                if (!sources.Contains(source))
                {
                    sources.Add(source);
                }
            }
        }

        /// <summary>What a scan should look at. Scenes are the slow part — each one has to be loaded — so they can be turned off for a quick pass.</summary>
        [Flags]
        public enum Scope
        {
            Scripts = 1,
            Prefabs = 2,
            Scenes = 4,
            ScriptableObjects = 8,
            All = Scripts | Prefabs | Scenes | ScriptableObjects,
        }

        public static Report Scan(Scope scope = Scope.All)
        {
            var report = new Report();

            try
            {
                if (scope.HasFlag(Scope.Scripts))
                {
                    ScanScripts(report);
                }

                if (scope.HasFlag(Scope.Prefabs))
                {
                    ScanPrefabs(report);
                }

                if (scope.HasFlag(Scope.ScriptableObjects))
                {
                    ScanScriptableObjects(report);
                }

                if (scope.HasFlag(Scope.Scenes))
                {
                    ScanScenes(report);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Compare(report);
            return report;
        }

        private static void Compare(Report report)
        {
            var tableKeys = LocalizationEditorUtility.AllKeys();
            report.TableKeyCount = tableKeys.Count;

            var known = new HashSet<string>(tableKeys, StringComparer.Ordinal);

            foreach (var key in tableKeys)
            {
                if (!report.Sources.ContainsKey(key) && !ReservedKeys.Contains(key))
                {
                    report.UnusedKeys.Add(key);
                }
            }

            foreach (var key in report.Sources.Keys)
            {
                if (!known.Contains(key) && !ReservedKeys.Contains(key))
                {
                    report.MissingKeys.Add(key);
                }
            }

            report.UnusedKeys.Sort(StringComparer.Ordinal);
            report.MissingKeys.Sort(StringComparer.Ordinal);
        }

        private static void ScanScripts(Report report)
        {
            var files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            for (var i = 0; i < files.Length; i++)
            {
                if (ShowProgress("Scanning scripts", files[i], i, files.Length))
                {
                    return;
                }

                string text;

                try
                {
                    text = File.ReadAllText(files[i]);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Localization key usage: could not read '{files[i]}' — {e.Message}");
                    continue;
                }

                report.ScannedScripts++;
                var source = ToAssetPath(files[i]);

                foreach (var key in KeysInCode(text))
                {
                    report.Add(key, source);
                }
            }
        }

        /// <summary>
        /// Every key literal in one piece of C#. Public and pure so the pattern can be tested directly — it is
        /// the part of this tool most able to break quietly, since a regex that stops matching does not fail,
        /// it just reports fewer uses, which reads as "more keys are unused" rather than as an error.
        /// </summary>
        public static IEnumerable<string> KeysInCode(string sourceText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                yield break;
            }

            foreach (Match match in CallPattern.Matches(sourceText))
            {
                var key = match.Groups[1].Value;

                if (!string.IsNullOrEmpty(key))
                {
                    yield return key;
                }
            }
        }

        private static void ScanPrefabs(Report report)
        {
            var paths = AssetPathsOfType("t:Prefab");

            for (var i = 0; i < paths.Length; i++)
            {
                if (ShowProgress("Scanning prefabs", paths[i], i, paths.Length))
                {
                    return;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);

                if (prefab == null)
                {
                    continue;
                }

                report.ScannedPrefabs++;
                CollectFromHierarchy(prefab, paths[i], report);
            }
        }

        private static void ScanScriptableObjects(Report report)
        {
            var paths = AssetPathsOfType("t:ScriptableObject");

            for (var i = 0; i < paths.Length; i++)
            {
                if (ShowProgress("Scanning assets", paths[i], i, paths.Length))
                {
                    return;
                }

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]);

                // A locale table lists every key by definition — counting it as a use would make every key
                // look used and the whole report meaningless.
                if (asset == null || asset is LocaleTableSO)
                {
                    continue;
                }

                report.ScannedAssets++;
                CollectFrom(asset, paths[i], report);
            }
        }

        /// <summary>
        /// Scenes are read through a preview scene: it loads a scene's objects without disturbing whatever the
        /// author currently has open, which matters because a tool that quietly closes someone's unsaved work
        /// is not one they will run twice.
        /// </summary>
        private static void ScanScenes(Report report)
        {
            var paths = AssetPathsOfType("t:Scene");

            for (var i = 0; i < paths.Length; i++)
            {
                if (ShowProgress("Scanning scenes", paths[i], i, paths.Length))
                {
                    return;
                }

                var scene = default(UnityEngine.SceneManagement.Scene);

                try
                {
                    scene = EditorSceneManager.OpenPreviewScene(paths[i]);
                    report.ScannedScenes++;

                    foreach (var root in scene.GetRootGameObjects())
                    {
                        CollectFromHierarchy(root, paths[i], report);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Localization key usage: could not open scene '{paths[i]}' — {e.Message}");
                }
                finally
                {
                    if (scene.IsValid())
                    {
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }
            }
        }

        private static void CollectFromHierarchy(GameObject root, string source, Report report)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                // A missing script leaves a null in the array — a broken prefab should not abort the scan.
                if (component != null)
                {
                    CollectFrom(component, source, report);
                }
            }
        }

        /// <summary>
        /// Pulls keys out of one object's serialized data: the key field of a LocalizedText, and every
        /// LocalizedString anywhere inside it — including ones nested in lists or in your own [Serializable]
        /// classes, which is why this walks the SerializedObject rather than looking at known field names.
        /// </summary>
        private static void CollectFrom(Object target, string source, Report report)
        {
            if (target is LocalizedText localizedText)
            {
                report.Add(localizedText.Key, source);
            }

            using var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.Generic
                    || !string.Equals(property.type, nameof(LocalizedString), StringComparison.Ordinal))
                {
                    continue;
                }

                var key = property.FindPropertyRelative("key");

                if (key != null && key.propertyType == SerializedPropertyType.String)
                {
                    report.Add(key.stringValue, source);
                }
            }
        }

        private static string[] AssetPathsOfType(string filter) =>
            AssetDatabase.FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        private static string ToAssetPath(string absolutePath)
        {
            var normalized = absolutePath.Replace('\\', '/');
            var index = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
            return index < 0 ? normalized : normalized.Substring(index + 1);
        }

        /// <summary>Returns true when the author cancelled — every scan step checks it, so a scan of a large project is never something you have to sit through.</summary>
        private static bool ShowProgress(string title, string detail, int index, int total) =>
            EditorUtility.DisplayCancelableProgressBar(title, detail, total == 0 ? 1f : (float)index / total);
    }
}
