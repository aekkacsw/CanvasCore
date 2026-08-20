using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Presentation for <see cref="LocalizationKeyUsage"/>: run a scan, see which keys are used without being
    /// translated and which are translated without being used.
    ///
    /// <para><b>It never deletes.</b> A key can be built at runtime and no static scan can see that, so
    /// "unused" here means "no use was found" — a strong hint, not a verdict. Acting on a hint by destroying
    /// translations in every language is the wrong trade, so the unused list is a list, with a button to show
    /// you the key and nothing else. Missing keys are the opposite case: nothing is at risk in adding an empty
    /// row to every table, so that one is offered as a fix.</para>
    /// </summary>
    public sealed class LocalizationKeyUsageWindow : EditorWindow
    {
        private LocalizationKeyUsage.Report _report;
        private LocalizationKeyUsage.Scope _scope = LocalizationKeyUsage.Scope.All;
        private Vector2 _scroll;
        private readonly HashSet<string> _expanded = new();

        [MenuItem("Tools/CanvasCore/Localization/Key Usage...", priority = 111)]
        public static void Open()
        {
            var window = GetWindow<LocalizationKeyUsageWindow>(false, "Key Usage", true);
            window.minSize = new Vector2(460f, 340f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_report == null)
            {
                EditorGUILayout.HelpBox(
                    "Scans your scripts for Localization.Get(\"...\") literals and your prefabs, scenes, and " +
                    "assets for keys set in the Inspector — LocalizedText fields and LocalizedString fields " +
                    "both — then compares what it found against the locale tables.\n\n" +
                    "Press Scan to start. Scenes are the slow part; untick them for a quick pass.",
                    MessageType.Info);
                return;
            }

            DrawSummary();

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;

            DrawMissingSection();
            DrawUnusedSection();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    _report = LocalizationKeyUsage.Scan(_scope);
                    _expanded.Clear();
                }

                GUILayout.Space(8f);
                DrawScopeToggle("Scripts", LocalizationKeyUsage.Scope.Scripts);
                DrawScopeToggle("Prefabs", LocalizationKeyUsage.Scope.Prefabs);
                DrawScopeToggle("Assets", LocalizationKeyUsage.Scope.ScriptableObjects);
                DrawScopeToggle("Scenes", LocalizationKeyUsage.Scope.Scenes);

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawScopeToggle(string label, LocalizationKeyUsage.Scope flag)
        {
            var enabled = GUILayout.Toggle(_scope.HasFlag(flag), label, EditorStyles.toolbarButton, GUILayout.Width(62f));
            _scope = enabled ? _scope | flag : _scope & ~flag;
        }

        private void DrawSummary()
        {
            EditorGUILayout.HelpBox(
                $"{_report.TableKeyCount} key(s) in the locale tables · {_report.UsedKeyCount} key(s) used somewhere\n" +
                $"Scanned {_report.ScannedScripts} script(s), {_report.ScannedPrefabs} prefab(s), " +
                $"{_report.ScannedAssets} asset(s), {_report.ScannedScenes} scene(s).",
                MessageType.None);
        }

        private void DrawMissingSection()
        {
            EditorGUILayout.LabelField($"Used but not in any table ({_report.MissingKeys.Count})", EditorStyles.boldLabel);

            if (_report.MissingKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("Every key in use is translated somewhere. Nothing will render as #key# on screen.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "These render as the Missing Key Display (#key# by default) at runtime. Adding one puts an " +
                    "empty row in every locale table, which the table inspector then flags as untranslated.",
                    MessageType.Warning);

                foreach (var key in _report.MissingKeys)
                {
                    DrawKeyRow(key, "Add To All Tables", () =>
                    {
                        LocalizationEditorUtility.AddKeyToAllTables(key);
                        _report = LocalizationKeyUsage.Scan(_scope);
                        GUIUtility.ExitGUI();
                    });
                }
            }

            EditorGUILayout.Space(10);
        }

        private void DrawUnusedSection()
        {
            EditorGUILayout.LabelField($"In the tables but no use found ({_report.UnusedKeys.Count})", EditorStyles.boldLabel);

            if (_report.UnusedKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("Every translated key is used somewhere.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Flagged, not removed. A key built at runtime — Get(\"item.\" + id) — cannot be seen by any " +
                "scan and would appear here too, so check a key yourself before deleting it (the × in the " +
                "table inspector removes it from every language at once).",
                MessageType.Info);

            foreach (var key in _report.UnusedKeys)
            {
                DrawKeyRow(key, null, null);
            }
        }

        private void DrawKeyRow(string key, string actionLabel, System.Action action)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var sources = _report.Sources.TryGetValue(key, out var found) ? found : null;
                    var expandable = sources != null && sources.Count > 0;
                    var label = expandable ? $"{key}  ({sources.Count})" : key;

                    if (expandable)
                    {
                        if (GUILayout.Button(label, EditorStyles.foldout, GUILayout.ExpandWidth(true)) && !_expanded.Add(key))
                        {
                            _expanded.Remove(key);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(label);
                    }

                    var preview = LocalizationEditorUtility.PreviewValue(key);

                    if (!string.IsNullOrEmpty(preview))
                    {
                        EditorGUILayout.LabelField(preview, EditorStyles.miniLabel, GUILayout.Width(180f));
                    }

                    if (actionLabel != null && GUILayout.Button(actionLabel, EditorStyles.miniButton, GUILayout.Width(110f)))
                    {
                        action();
                    }
                }

                if (_expanded.Contains(key) && _report.Sources.TryGetValue(key, out var list))
                {
                    foreach (var source in list)
                    {
                        DrawSourceRow(source);
                    }
                }
            }
        }

        /// <summary>One place a key was found, clickable — a report you cannot navigate from is a report you check once.</summary>
        private static void DrawSourceRow(string source)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);
                EditorGUILayout.LabelField(source, EditorStyles.miniLabel);

                if (GUILayout.Button("Show", EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(source);

                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }
        }
    }
}
