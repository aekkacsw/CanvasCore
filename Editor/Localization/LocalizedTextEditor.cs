using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Inspector for LocalizedText: the key picker with its live preview, the two possible targets, and a
    /// button that writes the preview text straight into the target component so the Scene view shows real
    /// words at authoring time.
    ///
    /// That last part is opt-in rather than automatic on purpose. Pushing the translation into the text
    /// component dirties the prefab and bakes one language's string into the saved asset — harmless (it is
    /// overwritten on enable at runtime) but it shows up in version control, so it should happen when the
    /// author asks for it, not on every key edit.
    /// </summary>
    [CustomEditor(typeof(LocalizedText))]
    [CanEditMultipleObjects]
    public sealed class LocalizedTextEditor : UnityEditor.Editor
    {
        private SerializedProperty _keyProp;
        private SerializedProperty _tmpTargetProp;
        private SerializedProperty _uguiTargetProp;

        private void OnEnable()
        {
            _keyProp = serializedObject.FindProperty("key");
            _tmpTargetProp = serializedObject.FindProperty("tmpTarget");
            _uguiTargetProp = serializedObject.FindProperty("uguiTarget");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var keyRect = EditorGUILayout.GetControlRect(true, LocalizationKeyField.GetHeight());
            LocalizationKeyField.Draw(keyRect, _keyProp, new GUIContent("Key", "Key looked up in the active locale table."));

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_tmpTargetProp, new GUIContent("TMP Target"));
            EditorGUILayout.PropertyField(_uguiTargetProp, new GUIContent("uGUI Target"));

            if (_tmpTargetProp.objectReferenceValue == null && _uguiTargetProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No text component assigned — this LocalizedText has nothing to write to. Assign a TMP_Text " +
                    "or a uGUI Text (adding the component to a GameObject that already has one fills this in " +
                    "automatically).",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(2);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_keyProp.stringValue)))
            {
                if (GUILayout.Button(new GUIContent(
                        "Apply Preview To Text Component",
                        "Writes the current translation into the target so the Scene view shows it. Only affects authoring — at runtime the string is set on enable regardless.")))
                {
                    ApplyPreview();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyPreview()
        {
            var applied = 0;

            foreach (var each in targets)
            {
                var localized = (LocalizedText)each;
                var serializedTarget = new SerializedObject(localized);
                var key = serializedTarget.FindProperty("key").stringValue;
                var text = LocalizationEditorUtility.PreviewValue(key);

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (serializedTarget.FindProperty("tmpTarget").objectReferenceValue is TMP_Text tmp)
                {
                    Undo.RecordObject(tmp, "Apply Localization Preview");
                    tmp.text = text;
                    EditorUtility.SetDirty(tmp);
                }

                if (serializedTarget.FindProperty("uguiTarget").objectReferenceValue is Text ugui)
                {
                    Undo.RecordObject(ugui, "Apply Localization Preview");
                    ugui.text = text;
                    EditorUtility.SetDirty(ugui);
                }

                applied++;
            }

            if (applied == 0)
            {
                Debug.LogWarning("LocalizedText: nothing to preview — the selected keys have no translation in the default or fallback locale yet.");
            }
        }
    }
}
