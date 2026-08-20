using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aexxa.CanvasCore.Editor
{
    /// <summary>
    /// Sole editor surface for a LocaleTableSO, replacing Unity's default array Inspector — same reasoning as
    /// UICatalogSOEditor: the default "+" clones the previous element, which for a key/value list means a
    /// silent duplicate key, and a duplicate key is exactly the thing this asset must not have.
    ///
    /// A table can hold hundreds of strings, so the layout is built for scanning rather than for editing one
    /// field: one row per key, a search box that filters as you type, and problems (duplicates, untranslated
    /// values, a table sitting outside a Resources folder or missing from CanvasCoreSettings) surfaced at the
    /// top instead of waiting to be discovered at runtime. Rows are one line tall until a translation needs
    /// more, and only that row grows — a description paragraph should be readable without the whole table
    /// paying for it.
    ///
    /// Rows are laid out with explicit Rects rather than nested GUILayout controls — the same fix
    /// UICatalogSOEditor needed after its header row rendered on top of itself.
    /// </summary>
    [CustomEditor(typeof(LocaleTableSO))]
    public sealed class LocaleTableSOEditor : UnityEditor.Editor
    {
        private const float KeyColumnRatio = 0.38f;
        private const float RemoveButtonWidth = 22f;
        private const float ColumnGap = 4f;

        /// <summary>
        /// Ceiling on how tall one value field may grow. A row that expands to fit its text is the point, but
        /// without a limit a single pasted paragraph could reserve a screen-and-a-half of Inspector and push
        /// every other key out of sight — the opposite of the scanning layout this editor is built for.
        /// </summary>
        private const int MaxValueLines = 10;

        /// <summary>Reused for text measurement so laying out a few hundred rows does not allocate a GUIContent per row per repaint.</summary>
        private static readonly GUIContent MeasureContent = new GUIContent();

        private SerializedProperty _localeCodeProp;
        private SerializedProperty _isRightToLeftProp;
        private SerializedProperty _entriesProp;

        private string _search = string.Empty;

        /// <summary>
        /// Width of the value column, used to work out how many lines a translation wraps into. It has to be
        /// known *before* the row's Rect is reserved, and a Rect only carries a real width on a repaint — so
        /// it is measured on one frame and used on the next, held constant for the whole of any single frame.
        /// Varying it mid-frame would give the Layout and Repaint passes different row heights, which is how
        /// IMGUI ends up drawing controls on top of each other.
        /// </summary>
        private float _valueColumnWidth;

        private float _measuredValueColumnWidth;

        /// <summary>Key edit committed this frame (old, new), waiting to be mirrored into the other locale tables once this table's own change is applied.</summary>
        private (string oldKey, string newKey)? _pendingKeyChange;

        /// <summary>Key deleted this frame, waiting to be deleted from the other locale tables too.</summary>
        private string _pendingKeyRemoval;

        private void OnEnable()
        {
            _localeCodeProp = serializedObject.FindProperty("localeCode");
            _isRightToLeftProp = serializedObject.FindProperty("isRightToLeft");
            _entriesProp = serializedObject.FindProperty("entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var table = (LocaleTableSO)target;

            DrawHeader(table);
            DrawProblems(table);
            DrawToolbar();

            EditorGUILayout.Space(4);

            int? removeIndex = null;
            var shown = 0;

            if (_entriesProp.arraySize > 0)
            {
                DrawColumnHeaders();
            }

            // No nested ScrollView here on purpose — the Inspector is already a scroll view, and nesting one
            // inside it gives the two-scrollbar behaviour that makes long lists miserable to navigate. The
            // search box is what keeps a big table workable.
            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var entry = _entriesProp.GetArrayElementAtIndex(i);

                if (!MatchesSearch(entry))
                {
                    continue;
                }

                DrawEntryRow(entry, i, ref removeIndex);
                shown++;
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(
                    _entriesProp.arraySize == 0
                        ? "ยังไม่มี string ในตารางนี้ — กด \"+ Add Key\" หรือ import จาก CSV"
                        : $"ไม่มี key ไหนตรงกับ \"{_search}\"",
                    MessageType.Info);
            }

            if (removeIndex.HasValue)
            {
                _entriesProp.DeleteArrayElementAtIndex(removeIndex.Value);
            }

            serializedObject.ApplyModifiedProperties();

            // Propagation happens after the local edit is committed, never during the draw loop: the other
            // tables are separate assets with their own Undo entries, and writing to them mid-layout would
            // also mean this table's own change was not on disk yet when they were asked to match it.
            ApplyPendingKeySync(table);
            SyncValueColumnWidth();
        }

        /// <summary>
        /// Mirrors the key edit just committed into every other locale table — see
        /// LocalizationEditorUtility.AddKeyToAllTables for why a key is a project-wide thing rather than a
        /// per-language one. Only keys travel; the translated value stays local, which is the entire point.
        /// </summary>
        private void ApplyPendingKeySync(LocaleTableSO table)
        {
            if (_pendingKeyChange == null && _pendingKeyRemoval == null)
            {
                return;
            }

            // The lookup cached inside this table was built before the edit; the other tables are about to be
            // asked whether they already have the key, so it has to be current first.
            table.InvalidateLookup();
            LocalizationEditorUtility.InvalidateCaches();

            var others = LocalizationEditorUtility.TablesOtherThan(table);

            if (_pendingKeyRemoval != null)
            {
                LocalizationEditorUtility.RemoveKeyFromTables(others, _pendingKeyRemoval);
                _pendingKeyRemoval = null;
            }

            if (_pendingKeyChange == null)
            {
                return;
            }

            var (oldKey, newKey) = _pendingKeyChange.Value;
            _pendingKeyChange = null;

            // Note what is deliberately absent: clearing the key field does NOT delete the key from the other
            // languages. Deleting other people's translations is what the "×" button is for, with a
            // confirmation — it should never be the silent consequence of emptying a text field.
            if (string.IsNullOrEmpty(newKey))
            {
                return;
            }

            if (string.IsNullOrEmpty(oldKey))
            {
                LocalizationEditorUtility.AddKeyToTables(others, newKey);
            }
            else
            {
                LocalizationEditorUtility.RenameKeyInTables(others, oldKey, newKey);
            }
        }

        private void DrawHeader(LocaleTableSO table)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_localeCodeProp, new GUIContent("Locale Code"));

                EditorGUI.BeginChangeCheck();
                var rightToLeft = EditorGUILayout.ToggleLeft(
                    new GUIContent("RTL", "Right-to-left script — exposed as Localization.IsRightToLeft for your own layout code."),
                    _isRightToLeftProp.boolValue,
                    GUILayout.Width(46f));

                if (EditorGUI.EndChangeCheck())
                {
                    _isRightToLeftProp.boolValue = rightToLeft;
                }
            }

            var resourcesPath = LocalizationEditorUtility.ToResourcesPath(AssetDatabase.GetAssetPath(table));

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField(
                    "Resources Path",
                    string.IsNullOrEmpty(resourcesPath) ? "(not under a Resources folder)" : resourcesPath);
            }
        }

        private void DrawProblems(LocaleTableSO table)
        {
            var resourcesPath = LocalizationEditorUtility.ToResourcesPath(AssetDatabase.GetAssetPath(table));

            if (string.IsNullOrEmpty(resourcesPath))
            {
                EditorGUILayout.HelpBox(
                    "This asset is not inside a folder named \"Resources\", so Localization can never load it at " +
                    "runtime. Move it under one — e.g. Assets/Plugins/aexxa/CanvasCore/Resources/Localization/.",
                    MessageType.Error);
            }
            else if (!LocalizationEditorUtility.IsRegisteredInSettings(table))
            {
                EditorGUILayout.HelpBox(
                    $"No locale in CanvasCoreSettings points at '{table.LocaleCode}', so this table is never loaded at runtime.",
                    MessageType.Warning);

                if (GUILayout.Button("Add This Locale To CanvasCoreSettings"))
                {
                    LocalizationEditorUtility.RegisterInSettings(table);
                }
            }

            var duplicates = FindDuplicateKeys();

            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Duplicate key(s) — only the first of each is ever read: {string.Join(", ", duplicates)}",
                    MessageType.Error);
            }

            var untranslated = CountUntranslated();

            if (untranslated > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{untranslated} key(s) have an empty value — they fall through to the fallback locale at runtime.",
                    MessageType.Info);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

                if (GUILayout.Button(
                        new GUIContent("+ Add Key", "Adds an empty row. Type the key and press Enter — it is added to every other language at the same time."),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(70f)))
                {
                    AddBlankEntry();
                }

                if (GUILayout.Button("Sort", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                {
                    SortByKey();
                }

                if (GUILayout.Button("Fill Missing", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    FillMissingKeysFromOtherTables();
                }

                if (GUILayout.Button("CSV", EditorStyles.toolbarDropDown, GUILayout.Width(44f)))
                {
                    ShowCsvMenu();
                }
            }
        }

        /// <summary>
        /// One key/value pair. Laid out from a single reserved Rect: nesting fields inside a HorizontalScope
        /// makes their widths unreliable (the bug that had UICatalogSOEditor drawing a label on top of
        /// another), and with hundreds of rows the explicit version is also markedly cheaper.
        ///
        /// The row is as tall as its own translation needs — the key and the "×" stay pinned to the first line.
        /// </summary>
        private void DrawEntryRow(SerializedProperty entry, int index, ref int? removeIndex)
        {
            var keyProp = entry.FindPropertyRelative("key");
            var valueProp = entry.FindPropertyRelative("value");

            SplitRow(ReserveRow(ValueFieldHeight(valueProp.stringValue)), out var keyRect, out var valueRect, out var removeRect);
            MeasureValueColumn(valueRect);

            // Plain fields, not PropertyField: PropertyField routes through whatever PropertyDrawer the
            // field's attributes ask for, and a drawer that decides its own height (TextArea being the one
            // that bit here) draws outside the row it was given and leaves only its scrollbar visible. Height
            // is this editor's decision to make, because only this editor knows the column width.
            // The change check is what PropertyField would otherwise give for free — without it, writing
            // stringValue back every repaint marks the SerializedObject modified even when nothing changed,
            // flooding the Undo stack and leaving the asset permanently dirty.
            DrawKeyField(keyRect, keyProp);
            DrawValueField(valueRect, valueProp);

            if (GUI.Button(removeRect, new GUIContent("×", "Remove this key from every language"), EditorStyles.miniButton)
                && ConfirmRemoval(keyProp.stringValue))
            {
                removeIndex = index;
                _pendingKeyRemoval = keyProp.stringValue;
            }
        }

        /// <summary>
        /// The key column. DelayedTextField rather than TextField because the edit is mirrored into every
        /// other locale table when it commits — with a live field, typing "menu.play" would propagate "m",
        /// "me", "men"… and leave six junk keys in every language. This way the change lands once, on Enter or
        /// when focus leaves.
        /// </summary>
        private void DrawKeyField(Rect rect, SerializedProperty keyProp)
        {
            var previous = keyProp.stringValue;

            EditorGUI.BeginChangeCheck();
            var edited = EditorGUI.DelayedTextField(rect, previous);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            keyProp.stringValue = edited;
            _pendingKeyChange = (previous, edited);
        }

        /// <summary>Deleting a key deletes it in every language, so anything already translated elsewhere is worth one question first.</summary>
        private bool ConfirmRemoval(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }

            var others = LocalizationEditorUtility.TablesOtherThan((LocaleTableSO)target);
            var translated = LocalizationEditorUtility.LocalesWithTranslation(others, key);

            if (translated.Count == 0)
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "CanvasCore Localization",
                $"'{key}' is translated in {translated.Count} other language(s): {string.Join(", ", translated)}.\n\n" +
                "Removing the key removes it — and those translations — from every locale table. Undo (Ctrl+Z) " +
                "restores them.",
                "Remove From All",
                "Cancel");
        }

        /// <summary>
        /// How tall this translation needs its field to be — one line for the short strings that are most of
        /// any table, more for the ones that wrap. Asking the style itself means the answer stays right when
        /// the column is resized, the font changes, or the text is Thai rather than English.
        /// </summary>
        private float ValueFieldHeight(string value)
        {
            var line = EditorGUIUtility.singleLineHeight;

            // Before the first repaint there is no measured width to wrap against; one line is the safe guess,
            // and the row corrects itself on the very next frame.
            if (string.IsNullOrEmpty(value) || _valueColumnWidth < 1f)
            {
                return line;
            }

            MeasureContent.text = value;

            return Mathf.Clamp(EditorStyles.textArea.CalcHeight(MeasureContent, _valueColumnWidth), line, line * MaxValueLines);
        }

        /// <summary>Records the value column's real width for the next frame to wrap against — see <see cref="_valueColumnWidth"/> for why it cannot simply be used now.</summary>
        private void MeasureValueColumn(Rect valueRect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _measuredValueColumnWidth = valueRect.width;
            }
        }

        /// <summary>Adopts the measured width once the frame is over, and repaints so the rows re-wrap to it. Cheap: it only fires on the frames where the Inspector was actually resized.</summary>
        private void SyncValueColumnWidth()
        {
            if (Event.current.type != EventType.Repaint
                || _measuredValueColumnWidth < 1f
                || Mathf.Abs(_measuredValueColumnWidth - _valueColumnWidth) < 0.5f)
            {
                return;
            }

            _valueColumnWidth = _measuredValueColumnWidth;
            Repaint();
        }

        private static Rect ReserveRow(float height)
        {
            var row = GUILayoutUtility.GetRect(0f, height + 2f, GUILayout.ExpandWidth(true));
            row.height = height;
            return row;
        }

        /// <summary>
        /// Column geometry lives in one place so the header labels and every row below them line up by
        /// construction rather than by two copies of the same arithmetic staying in sync. Only the value takes
        /// the row's full height; a key and a delete button gain nothing from being stretched next to it.
        /// </summary>
        private static void SplitRow(Rect row, out Rect keyRect, out Rect valueRect, out Rect removeRect)
        {
            var keyWidth = (row.width - RemoveButtonWidth - ColumnGap * 2f) * KeyColumnRatio;
            var line = EditorGUIUtility.singleLineHeight;

            keyRect = new Rect(row.x, row.y, keyWidth, line);
            valueRect = new Rect(
                keyRect.xMax + ColumnGap,
                row.y,
                row.width - keyWidth - RemoveButtonWidth - ColumnGap * 2f,
                row.height);
            removeRect = new Rect(row.xMax - RemoveButtonWidth, row.y, RemoveButtonWidth, line);
        }

        /// <summary>Column captions — two identical-looking text fields per row give no clue which side is the key.</summary>
        private static void DrawColumnHeaders()
        {
            SplitRow(ReserveRow(EditorGUIUtility.singleLineHeight), out var keyRect, out var valueRect, out _);

            EditorGUI.LabelField(keyRect, "Key", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(valueRect, "Value", EditorStyles.miniBoldLabel);
        }

        /// <summary>
        /// The value column: a wrapping text area sized to the row, writing back only when the user actually
        /// changed something. Being a text area also makes Enter insert a line break instead of ending the
        /// edit, which is what you want for a message that is meant to be two lines — CSV export already
        /// quotes embedded newlines, so a wrapped value survives the round trip.
        /// </summary>
        private static void DrawValueField(Rect rect, SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            var edited = EditorGUI.TextArea(rect, property.stringValue, EditorStyles.textArea);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = edited;
            }
        }

        private bool MatchesSearch(SerializedProperty entry)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            var key = entry.FindPropertyRelative("key").stringValue ?? string.Empty;
            var value = entry.FindPropertyRelative("value").stringValue ?? string.Empty;

            return key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                   || value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<string> FindDuplicateKeys()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var key = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue;

                if (!string.IsNullOrEmpty(key) && !seen.Add(key) && !duplicates.Contains(key))
                {
                    duplicates.Add(key);
                }
            }

            return duplicates;
        }

        private int CountUntranslated()
        {
            var count = 0;

            for (var i = 0; i < _entriesProp.arraySize; i++)
            {
                var element = _entriesProp.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(element.FindPropertyRelative("key").stringValue)
                    && string.IsNullOrEmpty(element.FindPropertyRelative("value").stringValue))
                {
                    count++;
                }
            }

            return count;
        }

        private void AddBlankEntry()
        {
            var index = _entriesProp.arraySize;
            _entriesProp.InsertArrayElementAtIndex(index);

            // InsertArrayElementAtIndex copies the previous element — clear it, or "+" quietly makes a duplicate key.
            var entry = _entriesProp.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("key").stringValue = string.Empty;
            entry.FindPropertyRelative("value").stringValue = string.Empty;

            _search = string.Empty;
        }

        private void SortByKey()
        {
            serializedObject.ApplyModifiedProperties();

            var table = (LocaleTableSO)target;
            Undo.RecordObject(table, "Sort Locale Table");
            table.EditorSortByKey();
            EditorUtility.SetDirty(table);

            serializedObject.Update();
        }

        /// <summary>Adds every key the other tables have but this one lacks, with an empty value — the "what still needs translating" pass, without hand-copying keys between languages.</summary>
        private void FillMissingKeysFromOtherTables()
        {
            serializedObject.ApplyModifiedProperties();

            var table = (LocaleTableSO)target;
            var added = 0;

            Undo.RecordObject(table, "Fill Missing Localization Keys");

            foreach (var key in LocalizationEditorUtility.AllKeys())
            {
                if (table.HasKey(key))
                {
                    continue;
                }

                table.EditorSetValue(key, string.Empty);
                added++;
            }

            if (added > 0)
            {
                table.EditorSortByKey();
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                LocalizationEditorUtility.InvalidateCaches();
            }

            Debug.Log($"LocaleTableSO '{table.name}': added {added} missing key(s) from the other locale tables.");
            serializedObject.Update();
        }

        private static void ShowCsvMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Export All Locales To CSV..."), false, LocalizationCsvIO.ExportWithDialog);
            menu.AddItem(new GUIContent("Import CSV..."), false, LocalizationCsvIO.ImportWithDialog);
            menu.ShowAsContext();
        }
    }
}
