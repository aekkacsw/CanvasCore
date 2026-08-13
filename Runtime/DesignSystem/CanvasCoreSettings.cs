using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Single project-wide settings asset for the whole CanvasCore plugin, modeled on TextMeshPro's
    /// TMP_Settings: one well-known asset living under a Resources folder inside the plugin, loaded lazily
    /// via Resources.Load and cached. Whatever needs configuring across the plugin belongs here rather than
    /// as a hard-coded const scattered in some Editor script — the actual asset lives at
    /// Plugins/aexxa/CanvasCore/Resources/CanvasCoreSettings.asset.
    /// </summary>
    public sealed class CanvasCoreSettings : ScriptableObject
    {
        private const string ResourceName = "CanvasCoreSettings";

        private static CanvasCoreSettings _instance;

        public static CanvasCoreSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadPreferringAssetsCopy();
                }

                return _instance;
            }
        }

        /// <summary>
        /// A package installed via git URL can, before CanvasCoreImporter's "Import Resources Into
        /// Project" step runs, end up sitting alongside a same-named settings asset the consumer already
        /// imported into their own Assets/ — Resources.Load's single-result overload doesn't guarantee
        /// which one it picks when two Resources folders both contain "CanvasCoreSettings". The project's
        /// own Assets/ copy (writable, the one Inspector edits actually land on) always wins here.
        /// </summary>
        private static CanvasCoreSettings LoadPreferringAssetsCopy()
        {
            var candidates = Resources.LoadAll<CanvasCoreSettings>(ResourceName);

            if (candidates.Length == 0)
            {
                return null;
            }

#if UNITY_EDITOR
            foreach (var candidate in candidates)
            {
                if (UnityEditor.AssetDatabase.GetAssetPath(candidate).StartsWith("Assets/"))
                {
                    return candidate;
                }
            }
#endif

            return candidates[0];
        }

        [SerializeField]
        [Tooltip("Folder scanned for prefabs to expose under GameObject > Canvas Core > Create. Any prefab in this folder gets a menu item named after itself — it doesn't matter what the prefab is. Drag the folder itself in, same pattern as TMP Settings' style sheet/preset fields, instead of typing a path string.")]
        private Object prefabSourceFolder;

        /// <summary>The folder asset itself. Editor code resolves this to a path via AssetDatabase.GetAssetPath — kept as a plain Object reference here (not DefaultAsset) so this class stays compilable outside the Editor.</summary>
        public Object PrefabSourceFolder => prefabSourceFolder;
    }
}
