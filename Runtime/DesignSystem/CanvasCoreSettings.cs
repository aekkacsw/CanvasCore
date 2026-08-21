using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Single project-wide settings asset for the whole CanvasCore plugin, modeled on TextMeshPro's
    /// TMP_Settings: one well-known asset living under a Resources folder inside the plugin, loaded lazily
    /// via Resources.Load and cached. Whatever needs configuring across the plugin belongs here rather than
    /// as a hard-coded const scattered in some Editor script. The asset is not shipped as a loadable
    /// package asset — "Tools > CanvasCore > Import Essential Resources" puts the project's only copy
    /// at Assets/Plugins/aexxa/CanvasCore/Resources/CanvasCoreSettings.asset, so there is never a second
    /// one competing for the same Resources path.
    /// </summary>
    public sealed class CanvasCoreSettings : ScriptableObject
    {
        private const string ResourceName = "CanvasCoreSettings";

        private static CanvasCoreSettings _instance;
        private static bool _reportedMissing;

        public static CanvasCoreSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CanvasCoreResources.Load<CanvasCoreSettings>(ResourceName);
                    ReportIfMissing();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Says the one thing worth saying when there is no settings asset: the import step has not been run.
        /// The package deliberately ships no loadable copy of this asset — a package copy would be a second
        /// asset at the same Resources path, which is the ambiguity CanvasCoreResources exists to describe —
        /// so "not imported yet" and "no settings at all" are the same state, and every caller null-checks
        /// into some quiet fallback. Without this they would do it silently, and the project would look
        /// configured while nothing was reading anyone's configuration.
        /// </summary>
        private static void ReportIfMissing()
        {
            if (_instance != null || _reportedMissing)
            {
                return;
            }

            _reportedMissing = true;

            Debug.LogError(
                $"CanvasCore: no {ResourceName} asset found under any Resources folder. Run " +
                "'Tools > CanvasCore > Import Essential Resources' once to get your own copy of it, along " +
                "with UIRoot and UIBootstrap. Until then CanvasCore falls back to built-in defaults and no " +
                "language will load. 'Tools > CanvasCore > Import Examples' adds the Design System prefabs, " +
                "the en/th locale tables, and a scene that already runs.");
        }

        [SerializeField]
        [Tooltip("Folder scanned for prefabs to expose under GameObject > Canvas Core > Create. Any prefab in this folder gets a menu item named after itself — it doesn't matter what the prefab is. Drag the folder itself in, same pattern as TMP Settings' style sheet/preset fields, instead of typing a path string.")]
        private Object prefabSourceFolder;

        /// <summary>The folder asset itself. Editor code resolves this to a path via AssetDatabase.GetAssetPath — kept as a plain Object reference here (not DefaultAsset) so this class stays compilable outside the Editor.</summary>
        public Object PrefabSourceFolder => prefabSourceFolder;

        [SerializeField]
        [Tooltip("Every language the game ships with. Each row points at one LocaleTableSO by Resources path — deliberately not by object reference, so listing a language here costs nothing until it is actually the active one.")]
        private List<LocaleDescriptor> locales = new();

        [SerializeField]
        [Tooltip("Locale used on first run when auto-detection is off or the player's system language matches none of the rows above.")]
        private string defaultLocaleCode = "en";

        [SerializeField]
        [Tooltip("Locale consulted for any key missing from the active one. Usually the language the UI is authored in. Its table stays resident alongside the active one — leave empty to spend nothing on it.")]
        private string fallbackLocaleCode = "en";

        [SerializeField]
        [Tooltip("On first run, pick the locale whose System Language matches Application.systemLanguage. A player who has explicitly chosen a language always overrides this on later runs.")]
        private bool autoDetectSystemLanguage = true;

        [SerializeField]
        [Tooltip("Remember Localization.SetLocale in PlayerPrefs so the choice survives a restart. Turn this off if your own save system owns the language setting instead.")]
        private bool persistLocaleSelection = true;

        [SerializeField]
        [Tooltip("What Localization.Get returns for a key found in no table.")]
        private MissingKeyDisplay missingKeyDisplay = MissingKeyDisplay.MarkedKey;

        public IReadOnlyList<LocaleDescriptor> Locales => locales;

        public string DefaultLocaleCode => defaultLocaleCode;

        public string FallbackLocaleCode => fallbackLocaleCode;

        public bool AutoDetectSystemLanguage => autoDetectSystemLanguage;

        public bool PersistLocaleSelection => persistLocaleSelection;

        public MissingKeyDisplay MissingKeyDisplay => missingKeyDisplay;

        [SerializeField]
        [Tooltip("Read locale CSV files sitting outside the build, so translations can be fixed and whole languages added without rebuilding. Off by default: it costs a directory scan and a few file reads at startup.")]
        private bool loadExternalLocales;

        [SerializeField]
        [Tooltip("Which folders are searched. persistentDataPath is writable on every platform, so it is the one a player or translator can actually put a file into.")]
        private ExternalLocaleSource externalLocaleSource = ExternalLocaleSource.StreamingAssetsThenPersistent;

        [SerializeField]
        [Tooltip("Folder name looked for inside StreamingAssets and/or persistentDataPath — e.g. \"Localization\" means <persistentDataPath>/Localization/*.csv.")]
        private string externalLocaleFolderName = "Localization";

        public bool LoadExternalLocales => loadExternalLocales;

        public ExternalLocaleSource ExternalLocaleSource => externalLocaleSource;

        public string ExternalLocaleFolderName => externalLocaleFolderName;

        [SerializeField]
        [Tooltip("When CanvasCore is allowed to put a gamepad/keyboard highlight on screen. The default keeps out of the way until the player presses a direction, so a mouse session never shows a selection it did not ask for.")]
        private UIFocusMode focusMode = UIFocusMode.OnFirstNavigationInput;

        public UIFocusMode FocusMode => focusMode;
    }
}
