using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Sole editor surface for UICatalogSO — replaces Unity's default array Inspector entirely (that
    /// default UI is what let a stray "+" click silently clone a duplicate entry in the first place).
    /// Kept deliberately compact: only layers with entries get a header, expanded rows show three
    /// tight lines instead of stacking every field on its own line, and the resolved type name is
    /// shown short (not the full assembly-qualified string).
    /// </summary>
    [CustomEditor(typeof(UICatalogSO))]
    public sealed class UICatalogSOEditor : UnityEditor.Editor
    {
        private const string ResourcesMarker = "/Resources/";

        private static readonly UILayerId[] LayerOrder =
        {
            UILayerId.Background, UILayerId.Screen, UILayerId.Popup,
            UILayerId.Overlay, UILayerId.Toast, UILayerId.Blocker
        };

        private readonly Dictionary<string, UIView> _dragBuffer = new();
        private SerializedProperty _entriesProp;

        private void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty("entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDuplicateWarning();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Entry"))
                {
                    AddBlankEntry();
                }

                if (GUILayout.Button("Scan Resources/UI"))
                {
                    ScanAndAddMissingEntries();
                }
            }

            EditorGUILayout.Space(6);

            int? removeIndex = null;
            var drewAnyGroup = false;

            foreach (var layer in LayerOrder)
            {
                var indices = IndicesForLayer(layer);

                if (indices.Count == 0)
                {
                    continue;
                }

                DrawLayerGroup(layer, indices, ref removeIndex);
                drewAnyGroup = true;
            }

            if (!drewAnyGroup)
            {
                EditorGUILayout.HelpBox("ยังไม่มี entry — กด \"Scan Resources/UI\" หรือ \"+ Add Entry\" เพื่อเริ่ม", MessageType.Info);
            }

            if (removeIndex.HasValue)
            {
                _entriesProp.DeleteArrayElementAtIndex(removeIndex.Value);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private List<int> IndicesForLayer(UILayerId layer)
        {
            var indices = new List<int>();

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                if ((UILayerId)_entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("layer").intValue == layer)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private void DrawDuplicateWarning()
        {
            var seen = new HashSet<string>();
            var duplicates = new HashSet<string>();

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var typeName = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("typeAssemblyQualifiedName").stringValue;

                if (string.IsNullOrEmpty(typeName))
                {
                    continue;
                }

                if (!seen.Add(typeName))
                {
                    duplicates.Add(typeName);
                }
            }

            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Duplicate entries for: " + string.Join(", ", duplicates.Select(ShortTypeName)) +
                    " — UIManager.Show<T>() will only ever resolve one of them.",
                    MessageType.Warning);
            }
        }

        private void DrawLayerGroup(UILayerId layer, List<int> indices, ref int? removeIndex)
        {
            EditorGUILayout.LabelField($"{layer}  ·  {indices.Count}", EditorStyles.miniBoldLabel);

            foreach (var i in indices)
            {
                DrawEntry(_entriesProp.GetArrayElementAtIndex(i), i, ref removeIndex);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawEntry(SerializedProperty entryProp, int index, ref int? removeIndex)
        {
            var typeNameProp = entryProp.FindPropertyRelative("typeAssemblyQualifiedName");
            var resourcePathProp = entryProp.FindPropertyRelative("resourcePath");
            var prewarmOnBootProp = entryProp.FindPropertyRelative("prewarmOnBoot");

            // EditorGUILayout.Foldout does not reserve honest layout width inside a HorizontalScope,
            // so any GUILayout control placed after it on the same row draws on top of it. Explicit
            // rects sidestep that entirely.
            var row = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

            const float removeWidth = 20f;
            const float prewarmWidth = 46f;
            var removeRect = new Rect(row.xMax - removeWidth, row.y, removeWidth, row.height);

            var prewarmRect = default(Rect);
            var afterPrewarmX = removeRect.x;

            if (prewarmOnBootProp.boolValue)
            {
                prewarmRect = new Rect(removeRect.x - prewarmWidth - 4, row.y, prewarmWidth, row.height);
                afterPrewarmX = prewarmRect.x;
            }

            var foldoutWidth = Mathf.Min(row.width * 0.45f, 180f);
            var foldoutRect = new Rect(row.x, row.y, foldoutWidth, row.height);
            var pathRect = new Rect(foldoutRect.xMax + 6, row.y, Mathf.Max(0, afterPrewarmX - foldoutRect.xMax - 10), row.height);

            entryProp.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                entryProp.isExpanded,
                string.IsNullOrEmpty(typeNameProp.stringValue) ? "(unset)" : ShortTypeName(typeNameProp.stringValue),
                true);

            EditorGUI.LabelField(pathRect, resourcePathProp.stringValue, EditorStyles.miniLabel);

            if (prewarmOnBootProp.boolValue)
            {
                EditorGUI.LabelField(prewarmRect, "prewarm", EditorStyles.miniLabel);
            }

            if (GUI.Button(removeRect, "×"))
            {
                removeIndex = index;
            }

            if (entryProp.isExpanded)
            {
                DrawEntryDetails(entryProp);
            }

            DrawSeparator();
        }

        private void DrawEntryDetails(SerializedProperty entryProp)
        {
            var resourcePathProp = entryProp.FindPropertyRelative("resourcePath");
            var typeNameProp = entryProp.FindPropertyRelative("typeAssemblyQualifiedName");
            var layerProp = entryProp.FindPropertyRelative("layer");
            var prewarmOnBootProp = entryProp.FindPropertyRelative("prewarmOnBoot");
            var prewarmCountProp = entryProp.FindPropertyRelative("prewarmCount");
            var maxPoolSizeProp = entryProp.FindPropertyRelative("maxPoolSize");

            var key = entryProp.propertyPath;
            _dragBuffer.TryGetValue(key, out var dragPrefab);

            using (new EditorGUILayout.HorizontalScope())
            {
                dragPrefab = (UIView)EditorGUILayout.ObjectField("Drag prefab", dragPrefab, typeof(UIView), false);
                _dragBuffer[key] = dragPrefab;

                if (GUILayout.Button("Sync", GUILayout.Width(50)))
                {
                    SyncFromPrefab(dragPrefab, resourcePathProp, typeNameProp);
                }
            }

            var shortType = string.IsNullOrEmpty(typeNameProp.stringValue) ? "—" : ShortTypeName(typeNameProp.stringValue);
            var pathLabel = string.IsNullOrEmpty(resourcePathProp.stringValue) ? "—" : resourcePathProp.stringValue;
            EditorGUILayout.LabelField($"Resources/{pathLabel}  →  {shortType}", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(layerProp, GUIContent.none, GUILayout.Width(100));
                EditorGUILayout.LabelField("Max Pool", GUILayout.Width(56));
                EditorGUILayout.PropertyField(maxPoolSizeProp, GUIContent.none, GUILayout.Width(40));
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                prewarmOnBootProp.boolValue = EditorGUILayout.ToggleLeft("Prewarm on boot", prewarmOnBootProp.boolValue, GUILayout.Width(120));

                if (prewarmOnBootProp.boolValue)
                {
                    EditorGUILayout.LabelField("Count", GUILayout.Width(40));
                    EditorGUILayout.PropertyField(prewarmCountProp, GUIContent.none, GUILayout.Width(40));
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(2);
        }

        private static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.2f));
        }

        private void AddBlankEntry()
        {
            var defaults = GetLayerDefaults(UILayerId.Screen);

            _entriesProp.arraySize++;
            var entry = _entriesProp.GetArrayElementAtIndex(_entriesProp.arraySize - 1);
            entry.FindPropertyRelative("resourcePath").stringValue = string.Empty;
            entry.FindPropertyRelative("typeAssemblyQualifiedName").stringValue = string.Empty;
            entry.FindPropertyRelative("layer").intValue = (int)UILayerId.Screen;
            entry.FindPropertyRelative("prewarmOnBoot").boolValue = defaults.prewarmOnBoot;
            entry.FindPropertyRelative("prewarmCount").intValue = defaults.prewarmCount;
            entry.FindPropertyRelative("maxPoolSize").intValue = defaults.maxPoolSize;
            entry.isExpanded = true;
        }

        private void ScanAndAddMissingEntries()
        {
            var existingTypeNames = new HashSet<string>();

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var t = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("typeAssemblyQualifiedName").stringValue;

                if (!string.IsNullOrEmpty(t))
                {
                    existingTypeNames.Add(t);
                }
            }

            var added = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var markerIndex = path.IndexOf(ResourcesMarker, StringComparison.Ordinal);

                if (markerIndex < 0)
                {
                    continue;
                }

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = go != null ? go.GetComponent<UIView>() : null;

                if (view == null)
                {
                    continue;
                }

                var typeName = view.GetType().AssemblyQualifiedName;

                if (!existingTypeNames.Add(typeName))
                {
                    continue;
                }

                var relative = path.Substring(markerIndex + ResourcesMarker.Length);
                var extension = Path.GetExtension(relative);
                var resourcePath = relative.Substring(0, relative.Length - extension.Length);
                var guessedLayer = GuessLayer(view.GetType());
                var defaults = GetLayerDefaults(guessedLayer);

                _entriesProp.arraySize++;
                var entry = _entriesProp.GetArrayElementAtIndex(_entriesProp.arraySize - 1);
                entry.FindPropertyRelative("resourcePath").stringValue = resourcePath;
                entry.FindPropertyRelative("typeAssemblyQualifiedName").stringValue = typeName;
                entry.FindPropertyRelative("layer").intValue = (int)guessedLayer;
                entry.FindPropertyRelative("prewarmOnBoot").boolValue = defaults.prewarmOnBoot;
                entry.FindPropertyRelative("prewarmCount").intValue = defaults.prewarmCount;
                entry.FindPropertyRelative("maxPoolSize").intValue = defaults.maxPoolSize;

                added++;
            }

            Debug.Log($"UICatalogSO: scan complete — added {added} new entr{(added == 1 ? "y" : "ies")} from Resources/ folders.");
        }

        private static void SyncFromPrefab(UIView view, SerializedProperty resourcePathProp, SerializedProperty typeNameProp)
        {
            if (view == null)
            {
                Debug.LogError("UICatalogEntry: drag a prefab with a UIView-derived component into the field above before syncing.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(view.gameObject);
            var markerIndex = assetPath.IndexOf(ResourcesMarker, StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                Debug.LogError($"UICatalogEntry: '{assetPath}' is not inside a folder literally named 'Resources' — Resources.Load requires that. Move the prefab first.", view);
                return;
            }

            var relative = assetPath.Substring(markerIndex + ResourcesMarker.Length);
            var extension = Path.GetExtension(relative);
            var withoutExtension = relative.Substring(0, relative.Length - extension.Length);

            resourcePathProp.stringValue = withoutExtension;
            typeNameProp.stringValue = view.GetType().AssemblyQualifiedName;
        }

        private static UILayerId GuessLayer(Type viewType)
        {
            if (typeof(UIScreen).IsAssignableFrom(viewType)) return UILayerId.Screen;
            if (typeof(UIPopup).IsAssignableFrom(viewType)) return UILayerId.Popup;
            // Checked before the UIWidget catch-all below — UIToast IS a UIWidget, but it belongs in
            // the dedicated Toast layer (the one with the stacking VerticalLayoutGroup in UIRoot.prefab),
            // not lumped into Overlay with everything else.
            if (typeof(UIToast).IsAssignableFrom(viewType)) return UILayerId.Toast;
            if (typeof(UIWidget).IsAssignableFrom(viewType)) return UILayerId.Overlay;
            return UILayerId.Screen;
        }

        /// <summary>
        /// Sensible starting point per layer so a fresh entry doesn't default to the same generic
        /// maxPoolSize/prewarm regardless of what kind of UI it is — tune per-entry afterward as needed.
        /// </summary>
        private static (int maxPoolSize, bool prewarmOnBoot, int prewarmCount) GetLayerDefaults(UILayerId layer) => layer switch
        {
            // Backdrop — one instance, needs to be there the moment the app boots.
            UILayerId.Background => (1, true, 1),
            // Shown one at a time via Show/Hide, but hidden ones stay pooled across back-stack navigation.
            UILayerId.Screen => (5, false, 1),
            // Modal, on-demand — no boot cost, low reuse pressure.
            UILayerId.Popup => (3, false, 1),
            // Persistent HUD elements, often several alive at once — should be ready at boot.
            UILayerId.Overlay => (5, true, 1),
            // Bursty, repeated, several stacked on screen at once — biggest pool, small prewarm buffer
            // so the first couple of toasts don't pay an instantiate cost, but no need to block boot.
            UILayerId.Toast => (8, false, 2),
            // Loading blocker — one instance, must be instantly ready to block interaction.
            UILayerId.Blocker => (1, true, 1),
            _ => (10, false, 1)
        };

        private static string ShortTypeName(string assemblyQualifiedName)
        {
            var commaIndex = assemblyQualifiedName.IndexOf(',');
            var typeName = commaIndex < 0 ? assemblyQualifiedName : assemblyQualifiedName.Substring(0, commaIndex);

            // Strip the namespace (and any enclosing-type '+' separator for nested types) — only the
            // class name itself is short enough to fit the collapsed row without colliding with the
            // resource path label next to it.
            var lastSeparator = typeName.LastIndexOfAny(new[] { '.', '+' });
            return lastSeparator < 0 ? typeName : typeName.Substring(lastSeparator + 1);
        }
    }
}
