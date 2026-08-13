using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Builds "GameObject > Canvas Core > Create..." live from whatever prefabs sit in the configured
    /// folder — doesn't care what the prefab is, anything dropped there shows up.
    /// </summary>
    internal static class DesignSystemCreateMenu
    {
        /// <summary>
        /// Resolved via the installed package's own location (works whether it's a git/registry/local UPM
        /// package under Packages/, or vendored directly under Assets/) rather than a hardcoded literal —
        /// a fixed "Assets/Plugins/aexxa/CanvasCore/..." string only holds true for the original dev
        /// checkout and breaks for every consumer installing via "Add package from git URL", since that
        /// mounts the package under Packages/com.aexxa.canvascore/ instead.
        /// </summary>
        internal static string DefaultBaseFolder
        {
            get
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DesignSystemCreateMenu).Assembly);
                var packageRoot = packageInfo != null ? packageInfo.assetPath : "Assets/Plugins/aexxa/CanvasCore";
                return $"{packageRoot}/Prefabs/DesignSystem";
            }
        }

        internal static string ConfiguredBaseFolder
        {
            get
            {
                var folderAsset = CanvasCoreSettings.Instance != null ? CanvasCoreSettings.Instance.PrefabSourceFolder : null;
                return folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : DefaultBaseFolder;
            }
        }

        /// <summary>
        /// Single static menu entry that builds its item list live from whatever prefabs currently sit in
        /// <see cref="ConfiguredBaseFolder"/>. Replaces the old approach of writing a generated .cs file per
        /// prefab (see git history) — that baked absolute paths in at generation time and had to write back
        /// into the package's own folder, which silently breaks for any consumer whose package folder isn't
        /// at the exact same path (e.g. read-only git-URL installs under Packages/) or isn't writable at all.
        /// </summary>
        [MenuItem("GameObject/Canvas Core/Create...", false, 0)]
        private static void ShowCreateMenu(MenuCommand menuCommand)
        {
            var baseFolder = ConfiguredBaseFolder;
            var candidates = FindPrefabsInFolder(baseFolder);
            var contextGo = menuCommand.context as GameObject;

            var menu = new GenericMenu();
            if (candidates.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent($"No prefabs found in '{baseFolder}'"));
            }
            else
            {
                foreach (var (path, prefab) in candidates)
                {
                    menu.AddItem(new GUIContent(prefab.name), false, () => CreateFromBase(path, contextGo));
                }
            }

            menu.ShowAsContext();
        }

        internal static List<(string path, GameObject prefab)> FindPrefabsInFolder(string baseFolder)
        {
            var result = new List<(string, GameObject)>();

            if (string.IsNullOrEmpty(baseFolder) || !AssetDatabase.IsValidFolder(baseFolder))
            {
                return result;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { baseFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    result.Add((path, prefab));
                }
            }

            return result.OrderBy(entry => entry.Item2.name).ToList();
        }

        internal static void CreateFromBase(string prefabPath, GameObject contextGo)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"CanvasCore Design System: base prefab not found at '{prefabPath}'.");
                return;
            }

            var parent = contextGo != null && contextGo.transform is RectTransform
                ? contextGo.transform
                : FindOrCreateCanvas().transform;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObjectUtility.SetParentAndAlign(instance, parent.gameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Create " + prefab.name);
            Selection.activeGameObject = instance;
        }

        internal static Canvas FindOrCreateCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
                eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
            }

            return canvasGo.GetComponent<Canvas>();
        }
    }
}
