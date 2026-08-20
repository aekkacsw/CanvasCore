using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    [CustomEditor(typeof(CanvasCoreSettings))]
    public sealed class CanvasCoreSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _prefabSourceFolderProp;
        private SerializedProperty _localesProp;
        private SerializedProperty _defaultLocaleCodeProp;
        private SerializedProperty _fallbackLocaleCodeProp;
        private SerializedProperty _autoDetectProp;
        private SerializedProperty _persistProp;
        private SerializedProperty _missingKeyDisplayProp;
        private SerializedProperty _loadExternalProp;
        private SerializedProperty _externalSourceProp;
        private SerializedProperty _externalFolderProp;

        private void OnEnable()
        {
            _prefabSourceFolderProp = serializedObject.FindProperty("prefabSourceFolder");
            _localesProp = serializedObject.FindProperty("locales");
            _defaultLocaleCodeProp = serializedObject.FindProperty("defaultLocaleCode");
            _fallbackLocaleCodeProp = serializedObject.FindProperty("fallbackLocaleCode");
            _autoDetectProp = serializedObject.FindProperty("autoDetectSystemLanguage");
            _persistProp = serializedObject.FindProperty("persistLocaleSelection");
            _missingKeyDisplayProp = serializedObject.FindProperty("missingKeyDisplay");
            _loadExternalProp = serializedObject.FindProperty("loadExternalLocales");
            _externalSourceProp = serializedObject.FindProperty("externalLocaleSource");
            _externalFolderProp = serializedObject.FindProperty("externalLocaleFolderName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Create Menu", EditorStyles.boldLabel);

            // DefaultAsset (folders) is an Editor-only type, so the constraint lives here rather than on
            // the serialized field itself (which is a plain Object so CanvasCoreSettings stays Runtime-safe).
            // Drag the folder in directly — same pattern as TMP Settings' style sheet / preset fields —
            // instead of typing/browsing for a path string.
            EditorGUILayout.ObjectField(_prefabSourceFolderProp, typeof(DefaultAsset), new GUIContent("Prefab Folder"));

            var resolvedPath = _prefabSourceFolderProp.objectReferenceValue != null
                ? AssetDatabase.GetAssetPath(_prefabSourceFolderProp.objectReferenceValue)
                : $"({DesignSystemCreateMenu.DefaultBaseFolder} — default, no folder set)";

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Path", resolvedPath);
            }

            EditorGUILayout.HelpBox(
                "Any prefab under this folder — doesn't matter what it is — gets a real " +
                "\"GameObject > Canvas Core > Create > {prefab name}\" menu item. Click below after adding " +
                "or removing one — this does not happen automatically, so it never triggers a surprise recompile.",
                MessageType.Info);

            if (GUILayout.Button("Scan && Generate Menu", GUILayout.Height(28)))
            {
                serializedObject.ApplyModifiedProperties();
                DesignSystemMenuGenerator.ScanAndGenerate();
            }

            EditorGUILayout.Space(10);
            DrawLocalizationSection();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The locale list drives everything Localization does at runtime, and every way it can be wrong is
        /// silent — a table that is not under a Resources folder, a Resource Path that does not resolve, a
        /// default locale code that matches no row. Each of those is checked and reported here, because the
        /// alternative is finding out from a screen full of #missing.key# placeholders.
        /// </summary>
        private void DrawLocalizationSection()
        {
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_localesProp, new GUIContent("Locales"), true);

            if (_localesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No languages configured — Localization.Get() returns its key for everything. Create a " +
                    "Locale Table (Assets > Create > Aexxa > CanvasCore > Locale Table) under a Resources " +
                    "folder, then press the button below to list it here.",
                    MessageType.Info);
            }

            EditorGUILayout.PropertyField(_defaultLocaleCodeProp, new GUIContent("Default Locale"));
            EditorGUILayout.PropertyField(_fallbackLocaleCodeProp, new GUIContent("Fallback Locale"));
            EditorGUILayout.PropertyField(_autoDetectProp, new GUIContent("Auto Detect System Language"));
            EditorGUILayout.PropertyField(_persistProp, new GUIContent("Persist Player Choice"));
            EditorGUILayout.PropertyField(_missingKeyDisplayProp, new GUIContent("Missing Key Display"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_loadExternalProp, new GUIContent("Load External Locale Files"));

            if (_loadExternalProp.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_externalSourceProp, new GUIContent("Search"));
                    EditorGUILayout.PropertyField(_externalFolderProp, new GUIContent("Folder Name"));

                    var folder = string.IsNullOrEmpty(_externalFolderProp.stringValue) ? "Localization" : _externalFolderProp.stringValue;

                    EditorGUILayout.HelpBox(
                        "Drop CSV files exported from here (same format: a \"key\" column then one column per " +
                        "locale code) into:\n" +
                        $"  <StreamingAssets>/{folder}/   — ships inside the build, read-only\n" +
                        $"  {System.IO.Path.Combine(Application.persistentDataPath, folder)}   — writable, wins over the above\n\n" +
                        "Files override the shipped tables key by key, and a locale column the build has never " +
                        "heard of becomes a new language in the picker. Android and WebGL cannot enumerate " +
                        "StreamingAssets as a directory, so only persistentDataPath is read there.",
                        MessageType.None);

                    if (GUILayout.Button("Open persistentDataPath Folder"))
                    {
                        var path = System.IO.Path.Combine(Application.persistentDataPath, folder);
                        System.IO.Directory.CreateDirectory(path);
                        EditorUtility.RevealInFinder(path);
                    }
                }
            }

            DrawLocaleProblems();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Find Locale Tables", "Add a row for every LocaleTableSO under Assets/ that is not listed yet.")))
                {
                    serializedObject.ApplyModifiedProperties();
                    RegisterUnlistedTables();
                    serializedObject.Update();
                }

                if (GUILayout.Button(new GUIContent("Export CSV...", "Write every locale table to one spreadsheet, one column per language.")))
                {
                    LocalizationCsvIO.ExportWithDialog();
                }

                if (GUILayout.Button(new GUIContent("Import CSV...", "Read a translated spreadsheet back into the locale tables.")))
                {
                    LocalizationCsvIO.ImportWithDialog();
                }
            }

            if (Application.isPlaying && GUILayout.Button("Reload Localization (play mode)"))
            {
                Localization.Reload();
            }
        }

        private void DrawLocaleProblems()
        {
            var codes = new System.Collections.Generic.List<string>();
            var problems = new System.Collections.Generic.List<string>();

            for (var i = 0; i < _localesProp.arraySize; i++)
            {
                var descriptor = _localesProp.GetArrayElementAtIndex(i);
                var code = descriptor.FindPropertyRelative("code").stringValue;
                var resourcePath = descriptor.FindPropertyRelative("resourcePath").stringValue;

                if (string.IsNullOrEmpty(code))
                {
                    problems.Add($"Row {i}: no locale code.");
                    continue;
                }

                if (codes.Contains(code))
                {
                    problems.Add($"'{code}' is listed more than once — only the first is ever used.");
                }

                codes.Add(code);

                if (string.IsNullOrEmpty(resourcePath))
                {
                    problems.Add($"'{code}': no Resource Path.");
                }
                else if (Resources.Load<LocaleTableSO>(resourcePath) == null)
                {
                    problems.Add($"'{code}': nothing loads from Resources path '{resourcePath}'. The table must sit under a folder named \"Resources\", and the path is written without a file extension.");
                }

                // A font path is optional, but a wrong one is silent until someone plays in that language.
                var fontPath = descriptor.FindPropertyRelative("fontResourcePath").stringValue;

                if (!string.IsNullOrEmpty(fontPath) && Resources.Load<TMPro.TMP_FontAsset>(fontPath) == null)
                {
                    problems.Add($"'{code}': no TMP font asset at Resources path '{fontPath}' — labels will keep their own font in this language.");
                }
            }

            var defaultCode = _defaultLocaleCodeProp.stringValue;

            if (_localesProp.arraySize > 0 && !string.IsNullOrEmpty(defaultCode) && !codes.Contains(defaultCode))
            {
                problems.Add($"Default Locale '{defaultCode}' matches no row above — the first locale will be used instead.");
            }

            var fallbackCode = _fallbackLocaleCodeProp.stringValue;

            if (!string.IsNullOrEmpty(fallbackCode) && _localesProp.arraySize > 0 && !codes.Contains(fallbackCode))
            {
                problems.Add($"Fallback Locale '{fallbackCode}' matches no row above — untranslated keys will show the missing-key placeholder instead of falling back.");
            }

            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            }
        }

        private static void RegisterUnlistedTables()
        {
            LocalizationEditorUtility.InvalidateCaches();
            var added = 0;

            foreach (var table in LocalizationEditorUtility.FindAllTables())
            {
                if (LocalizationEditorUtility.IsRegisteredInSettings(table))
                {
                    continue;
                }

                LocalizationEditorUtility.RegisterInSettings(table);
                added++;
            }

            Debug.Log(added == 0
                ? "CanvasCore Localization: every LocaleTableSO under Assets/ is already listed."
                : $"CanvasCore Localization: added {added} locale row(s) from the tables found under Assets/.");
        }
    }
}
