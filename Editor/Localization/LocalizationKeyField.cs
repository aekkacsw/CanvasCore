using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// The key-picking control shared by LocalizedText's inspector and LocalizedString's drawer: a text field,
    /// a dropdown of every key that exists, and — the part that actually matters — a live preview of what the
    /// key reads as in the authoring language, right under the field.
    ///
    /// Without that preview, laying out a screen means staring at rows of "menu.settings.audio.master" with no
    /// idea what any of them say. With it, a key field is about as informative as the literal string it
    /// replaced, which is what makes localizing UI from the start bearable rather than a chore deferred until
    /// it is expensive.
    ///
    /// Everything is drawn from explicit Rects because it is used from a PropertyDrawer, where GUILayout is
    /// not available at all.
    /// </summary>
    public static class LocalizationKeyField
    {
        private const float DropdownWidth = 22f;
        private const float AddButtonWidth = 46f;
        private const float Gap = 2f;

        private static GUIStyle _previewStyle;

        /// <summary>Total height of the control: the key row plus the preview/warning row beneath it.</summary>
        public static float GetHeight() => EditorGUIUtility.singleLineHeight * 2f + Gap;

        /// <summary>Draws the key field for a string property holding a localization key.</summary>
        public static void Draw(Rect position, SerializedProperty keyProp, GUIContent label)
        {
            var keyRow = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var fieldRect = EditorGUI.PrefixLabel(keyRow, label);
            var textRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - DropdownWidth - Gap, fieldRect.height);
            var dropdownRect = new Rect(textRect.xMax + Gap, fieldRect.y, DropdownWidth, fieldRect.height);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = keyProp.hasMultipleDifferentValues;
            var typed = EditorGUI.TextField(textRect, keyProp.stringValue);
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
            {
                keyProp.stringValue = typed;
            }

            if (GUI.Button(dropdownRect, new GUIContent("▾", "Pick from the keys already defined in your locale tables"), EditorStyles.miniButton))
            {
                ShowKeyMenu(keyProp);
            }

            DrawPreviewRow(
                new Rect(fieldRect.x, keyRow.yMax + Gap, fieldRect.width, EditorGUIUtility.singleLineHeight),
                keyProp);
        }

        private static void DrawPreviewRow(Rect row, SerializedProperty keyProp)
        {
            var key = keyProp.stringValue;

            if (keyProp.hasMultipleDifferentValues)
            {
                EditorGUI.LabelField(row, "(multiple keys selected)", PreviewStyle);
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                EditorGUI.LabelField(row, "(no key — this label will render empty)", PreviewStyle);
                return;
            }

            if (!LocalizationEditorUtility.KeyExists(key))
            {
                var labelRect = new Rect(row.x, row.y, row.width - AddButtonWidth - Gap, row.height);
                var buttonRect = new Rect(labelRect.xMax + Gap, row.y, AddButtonWidth, row.height);

                EditorGUI.LabelField(labelRect, new GUIContent($"⚠ '{key}' is in no locale table"), PreviewStyle);

                if (GUI.Button(buttonRect, new GUIContent("Add", "Add this key to every locale table, with an empty value to fill in later"), EditorStyles.miniButton))
                {
                    LocalizationEditorUtility.AddKeyToAllTables(key);
                }

                return;
            }

            var preview = LocalizationEditorUtility.PreviewValue(key);

            EditorGUI.LabelField(
                row,
                new GUIContent(
                    string.IsNullOrEmpty(preview) ? "(defined, but not translated yet)" : $"“{preview}”",
                    preview),
                PreviewStyle);
        }

        /// <summary>
        /// Keys are shown as a nested menu split on '.', so "menu.settings.audio" lands under menu > settings.
        /// A flat list of several hundred entries is unusable; the dotted convention most projects already use
        /// for keys turns into a real hierarchy for free.
        /// </summary>
        private static void ShowKeyMenu(SerializedProperty keyProp)
        {
            var keys = LocalizationEditorUtility.AllKeys();
            var menu = new GenericMenu();

            if (keys.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No keys defined — create a Locale Table first"));
                menu.ShowAsContext();
                return;
            }

            // The callback runs a frame or more later, by which time this SerializedProperty instance may no
            // longer be valid — the inspector redraws and rebuilds its properties in between. Capturing the
            // owning SerializedObject and the property *path* instead, and re-resolving on click, is what
            // makes the menu safe to leave open.
            var owner = keyProp.serializedObject;
            var path = keyProp.propertyPath;
            var current = keyProp.stringValue;

            foreach (var key in keys)
            {
                var captured = key;

                menu.AddItem(
                    new GUIContent(key.Replace('.', '/')),
                    string.Equals(key, current, System.StringComparison.Ordinal),
                    () =>
                    {
                        var property = owner.FindProperty(path);

                        if (property == null)
                        {
                            return;
                        }

                        property.stringValue = captured;
                        owner.ApplyModifiedProperties();
                    });
            }

            menu.ShowAsContext();
        }

        private static GUIStyle PreviewStyle
        {
            get
            {
                if (_previewStyle == null)
                {
                    _previewStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontStyle = FontStyle.Italic,
                        clipping = TextClipping.Clip,
                    };
                }

                return _previewStyle;
            }
        }
    }
}
