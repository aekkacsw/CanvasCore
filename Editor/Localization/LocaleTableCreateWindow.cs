using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Adds a language in one step: creates the LocaleTableSO under a Resources folder, registers it in
    /// CanvasCoreSettings with the right code and system language, and optionally seeds it with every key the
    /// existing tables already use.
    ///
    /// Doing this by hand means creating the asset, remembering it has to live under Resources, typing the
    /// same code into two places, and copying the key list over from another language — four steps, each of
    /// which fails silently at runtime when skipped. That is the kind of setup a tool should just do.
    /// </summary>
    public sealed class LocaleTableCreateWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/Plugins/aexxa/CanvasCore/Resources/Localization";

        private string _code = "en";
        private string _displayName = "English";
        private SystemLanguage _systemLanguage = SystemLanguage.English;
        private string _folder = DefaultFolder;
        private bool _seedWithExistingKeys = true;

        [MenuItem("Tools/CanvasCore/Localization/Create Locale Table...", priority = 90)]
        public static void Open()
        {
            var window = GetWindow<LocaleTableCreateWindow>(true, "Create Locale Table", true);
            window.minSize = new Vector2(420f, 250f);
            window.maxSize = new Vector2(700f, 260f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("New Language", EditorStyles.boldLabel);

            _code = EditorGUILayout.TextField(new GUIContent("Locale Code", "Used in code, in CanvasCoreSettings, and as the CSV column header — e.g. \"en\", \"th\"."), _code);
            _displayName = EditorGUILayout.TextField(new GUIContent("Display Name", "Shown in a language picker. Write it in its own language."), _displayName);
            _systemLanguage = (SystemLanguage)EditorGUILayout.EnumPopup(new GUIContent("System Language", "Which Application.systemLanguage auto-detects into this locale on first run."), _systemLanguage);

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField(new GUIContent("Folder", "Must be under a folder named \"Resources\" — that is the only place Localization can load a table from."), _folder);

                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.SaveFolderPanel("Locale Tables Folder", _folder, string.Empty);

                    if (!string.IsNullOrEmpty(picked))
                    {
                        _folder = ToProjectRelative(picked);
                    }
                }
            }

            _seedWithExistingKeys = EditorGUILayout.Toggle(
                new GUIContent("Seed With Existing Keys", "Copy every key the other locale tables use into this one, with empty values to translate."),
                _seedWithExistingKeys);

            EditorGUILayout.Space(6);

            var problem = Validate();

            if (problem != null)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button("Create", GUILayout.Height(28f)))
                {
                    Create();
                    Close();
                }
            }
        }

        private string Validate()
        {
            if (string.IsNullOrWhiteSpace(_code))
            {
                return "A locale code is required.";
            }

            if (!_folder.Replace('\\', '/').Contains(LocalizationEditorUtility.ResourcesMarker) && !_folder.EndsWith("/Resources"))
            {
                return "The folder must be inside one named \"Resources\", otherwise Localization can never load the table at runtime.";
            }

            if (LocalizationEditorUtility.FindTable(_code) != null)
            {
                return $"A locale table with code '{_code}' already exists.";
            }

            return null;
        }

        private void Create()
        {
            Directory.CreateDirectory(_folder);
            AssetDatabase.Refresh();

            var table = CreateInstance<LocaleTableSO>();
            table.EditorSetLocaleCode(_code);

            if (_seedWithExistingKeys)
            {
                foreach (var key in LocalizationEditorUtility.AllKeys())
                {
                    table.EditorSetValue(key, string.Empty);
                }
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{_folder.TrimEnd('/')}/{_code}.asset");
            AssetDatabase.CreateAsset(table, assetPath);
            AssetDatabase.SaveAssets();
            LocalizationEditorUtility.InvalidateCaches();

            LocalizationEditorUtility.RegisterInSettings(
                _code,
                _displayName,
                LocalizationEditorUtility.ToResourcesPath(assetPath),
                _systemLanguage);

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);

            Debug.Log($"CanvasCore Localization: created '{assetPath}' and registered locale '{_code}' in CanvasCoreSettings.", table);
        }

        private static string ToProjectRelative(string absolutePath)
        {
            var normalized = absolutePath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');

            return normalized.StartsWith(dataPath, System.StringComparison.Ordinal)
                ? "Assets" + normalized.Substring(dataPath.Length)
                : normalized;
        }
    }
}
