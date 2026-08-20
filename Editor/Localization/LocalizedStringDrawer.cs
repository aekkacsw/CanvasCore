using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Makes a serialized LocalizedString field render as a key picker with a translation preview instead of
    /// as a bare struct with one string inside it. Applies anywhere the type is used — a MonoBehaviour field,
    /// a ScriptableObject, a nested list — because it is registered on the type rather than on any one owner.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizedString))]
    public sealed class LocalizedStringDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            LocalizationKeyField.GetHeight();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            LocalizationKeyField.Draw(position, property.FindPropertyRelative("key"), label);
            EditorGUI.EndProperty();
        }
    }
}
