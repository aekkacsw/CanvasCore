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
    /// Shared helpers used by the generated per-prefab [MenuItem]s (see DesignSystemMenuGenerator /
    /// Generated/DesignSystemCreateMenuItems.generated.cs) — this class itself declares no menu items.
    /// Doesn't care what the prefab is: anything dropped in the configured folder gets a menu item.
    /// </summary>
    internal static class DesignSystemCreateMenu
    {
        /// <summary>
        /// Deliberately Assets/-only, never resolved against the package's own install location: the
        /// package (however it's installed - git URL, embedded, vendored) never feeds this menu directly.
        /// CanvasCoreImporter copies the real Prefabs/DesignSystem folder into this exact path on import,
        /// so this is the one and only place ever scanned.
        /// </summary>
        internal const string DefaultBaseFolder = "Assets/Plugins/aexxa/CanvasCore/Prefabs/DesignSystem";

        internal static string ConfiguredBaseFolder
        {
            get
            {
                var folderAsset = CanvasCoreSettings.Instance != null ? CanvasCoreSettings.Instance.PrefabSourceFolder : null;
                return folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : DefaultBaseFolder;
            }
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

        /// <summary>
        /// Entry point for the compiled per-prefab menu items in Generated/DesignSystemCreateMenuItems
        /// .generated.cs — resolves the prefab by name against the *live* ConfiguredBaseFolder rather than
        /// a path baked in at generation time, so the shipped generated file keeps working no matter where
        /// the package actually ends up installed (Assets/ vendor copy, Packages/ git-URL install, etc.).
        /// </summary>
        internal static void CreateByName(string prefabName, GameObject contextGo)
        {
            var baseFolder = ConfiguredBaseFolder;
            var match = FindPrefabsInFolder(baseFolder).FirstOrDefault(entry => entry.prefab.name == prefabName);

            if (match.prefab == null)
            {
                Debug.LogError(
                    $"CanvasCore Design System: no prefab named '{prefabName}' found under '{baseFolder}'. " +
                    "If you changed the Prefab Folder in CanvasCoreSettings, run Tools > CanvasCore > Scan Create Menu Prefabs again.");
                return;
            }

            CreateFromBase(match.path, contextGo);
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
