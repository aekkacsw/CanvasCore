using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Draws one <see cref="LocalizedAssetVariant"/> as a locale picker plus a drop target, instead of two raw
    /// text fields.
    ///
    /// Both fields fail the same way when typed by hand: silently, and only in the language nobody on the team
    /// plays in. A locale code with a typo simply never matches, and a Resources path with a typo — or a
    /// perfectly correct path to an asset that is not under a Resources folder — resolves to null the first
    /// time that language is selected. Neither shows up until then. Picking from the locales the project
    /// actually has, and deriving the path from the asset itself, removes both.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizedAssetVariant))]
    public sealed class LocalizedAssetVariantDrawer : PropertyDrawer
    {
        private const float LocaleWidth = 90f;
        private const float Gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            return NeedsWarning(property) ? line * 2f + 4f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var codeProp = property.FindPropertyRelative("localeCode");
            var pathProp = property.FindPropertyRelative("resourcePath");

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var localeRect = new Rect(row.x, row.y, LocaleWidth, row.height);
            var pathRect = new Rect(localeRect.xMax + Gap, row.y, row.width - LocaleWidth - Gap, row.height);

            DrawLocalePopup(localeRect, codeProp);
            DrawPathField(pathRect, pathProp);

            if (NeedsWarning(property))
            {
                var warning = new Rect(position.x, row.yMax + 2f, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(warning, WarningFor(property), WarningStyle);
            }
        }

        /// <summary>
        /// The locale codes configured in the project, plus whatever this variant already holds — an unknown
        /// code stays visible and selected rather than being silently replaced by the first entry in the list,
        /// which would be an edit nobody made.
        /// </summary>
        private static void DrawLocalePopup(Rect rect, SerializedProperty codeProp)
        {
            var codes = ConfiguredCodes();
            var current = codeProp.stringValue ?? string.Empty;

            if (!string.IsNullOrEmpty(current) && !codes.Contains(current))
            {
                codes.Insert(0, current);
            }

            if (codes.Count == 0)
            {
                codeProp.stringValue = EditorGUI.TextField(rect, current);
                return;
            }

            var index = Mathf.Max(0, codes.IndexOf(current));

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.Popup(rect, index, codes.ToArray());

            if (EditorGUI.EndChangeCheck())
            {
                codeProp.stringValue = codes[picked];
            }
        }

        /// <summary>
        /// The path, editable as text, with an object field beside it that converts a dropped asset into the
        /// Resources path Resources.Load actually wants. The text stays editable because a path can legitimately
        /// point at an asset that is not in the project yet — an asset a mod or a later build will provide.
        /// </summary>
        private static void DrawPathField(Rect rect, SerializedProperty pathProp)
        {
            const float PickerWidth = 22f;

            var textRect = new Rect(rect.x, rect.y, rect.width - PickerWidth - Gap, rect.height);
            var pickerRect = new Rect(textRect.xMax + Gap, rect.y, PickerWidth, rect.height);

            EditorGUI.BeginChangeCheck();
            var edited = EditorGUI.TextField(textRect, pathProp.stringValue);

            if (EditorGUI.EndChangeCheck())
            {
                pathProp.stringValue = edited;
            }

            EditorGUI.BeginChangeCheck();
            var dropped = EditorGUI.ObjectField(pickerRect, null, typeof(Object), false);

            if (!EditorGUI.EndChangeCheck() || dropped == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(dropped);
            var resourcesPath = LocalizationEditorUtility.ToResourcesPath(assetPath);

            if (string.IsNullOrEmpty(resourcesPath))
            {
                // Not a failure to swallow: the asset is real, the reference would look fine, and it would
                // resolve to null at runtime with no clue why.
                Debug.LogError(
                    $"LocalizedAsset: '{assetPath}' is not inside a folder named \"Resources\", so " +
                    "Resources.Load can never find it. Move it under one, then drop it here again.", dropped);
                return;
            }

            pathProp.stringValue = resourcesPath;
        }

        private static List<string> ConfiguredCodes()
        {
            var settings = CanvasCoreSettings.Instance;

            if (settings == null)
            {
                return new List<string>();
            }

            return settings.Locales
                .Where(descriptor => descriptor != null && !string.IsNullOrEmpty(descriptor.Code))
                .Select(descriptor => descriptor.Code)
                .Distinct()
                .ToList();
        }

        private static bool NeedsWarning(SerializedProperty property) => !string.IsNullOrEmpty(WarningFor(property));

        /// <summary>Checked live rather than at load: a path that was right when it was typed goes wrong the moment the asset is moved out of Resources, and nothing else in the project would notice.</summary>
        private static string WarningFor(SerializedProperty property)
        {
            var path = property.FindPropertyRelative("resourcePath").stringValue;

            if (string.IsNullOrEmpty(path))
            {
                return "No path — this locale falls back to the default.";
            }

            return Resources.Load(path) == null
                ? $"Nothing loads from Resources path '{path}'."
                : null;
        }

        private static GUIStyle WarningStyle => new(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.95f, 0.68f, 0.2f) },
            wordWrap = false,
        };
    }
}
