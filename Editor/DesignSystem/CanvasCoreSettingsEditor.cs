using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    [CustomEditor(typeof(CanvasCoreSettings))]
    public sealed class CanvasCoreSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _prefabSourceFolderProp;

        private void OnEnable()
        {
            _prefabSourceFolderProp = serializedObject.FindProperty("prefabSourceFolder");
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
                "Any prefab under this folder — doesn't matter what it is — shows up under " +
                "\"GameObject > Canvas Core > Create...\". The list is built live each time that menu is " +
                "opened, so adding or removing a prefab here takes effect immediately — nothing to regenerate.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
