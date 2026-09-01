using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public sealed class AttackDataEditorWindow : EditorWindow
{
    private const string DefaultFolderPath = "Assets/Datas/AttackDatas";
    private const string AddressableGroupName = "AttackDatas";
    private const string AddressableLabel = "AttackData";
    private const string FolderPathPrefsKey = "UnitySisters.AttackDataEditor.FolderPath";
    private const string PrefixPatternsPrefsKey = "UnitySisters.AttackDataEditor.PrefixPatterns";
    private const string LastPrefixIndexPrefsKey = "UnitySisters.AttackDataEditor.LastPrefixIndex";
    private const string LastCustomPrefixPrefsKey = "UnitySisters.AttackDataEditor.LastCustomPrefix";
    private const string LeftPaneWidthPrefsKey = "UnitySisters.AttackDataEditor.LeftPaneWidth";
    private const string DefaultPrefixPatterns = "Character;Monster;Boss;Common";
    private const string CustomPrefixOption = "Direct Input";
    private const string KeyPropertyName = "key";
    private const float MinLeftPaneWidth = 280.0f;
    private const float MaxLeftPaneWidth = 620.0f;
    private const float SplitterWidth = 4.0f;
    private const float AssetListHeight = 220.0f;
    private const float KeyIssueListHeight = 160.0f;
    private const float AssetRowHeight = 22.0f;
    private const double KeyIssueRebuildDelay = 0.35;

    private readonly List<AttackDataScriptableObject> attackDatas = new List<AttackDataScriptableObject>();
    private readonly List<DuplicateKeyGroup> duplicateKeyGroups = new List<DuplicateKeyGroup>();
    private readonly List<AttackDataScriptableObject> emptyKeyDatas = new List<AttackDataScriptableObject>();
    private readonly List<string> loadWarnings = new List<string>();

    private Vector2 leftScroll;
    private Vector2 assetListScroll;
    private Vector2 rightScroll;
    private Vector2 duplicateScroll;
    private AttackDataScriptableObject selectedData;
    private UnityEditor.Editor selectedEditor;

    private string folderPath;
    private string prefixPatternsText;
    private string[] prefixOptions;
    private int prefixIndex;
    private string customPrefix;
    private string bodyName = "Attack";
    private string suffixName = "Data";
    private string searchText = string.Empty;
    private bool showSettings = true;
    private bool showDuplicates = true;
    private bool isResizingSplitter;
    private bool keyIssueRebuildPending;
    private double keyIssueRebuildTime;
    private float leftPaneWidth;

    [MenuItem("UnityFramework/Attack Data Editor", false, 30)]
    private static void Open()
    {
        AttackDataEditorWindow window = GetWindow<AttackDataEditorWindow>("Attack Data");
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        folderPath = EditorPrefs.GetString(FolderPathPrefsKey, DefaultFolderPath);
        prefixPatternsText = EditorPrefs.GetString(PrefixPatternsPrefsKey, DefaultPrefixPatterns);
        prefixIndex = EditorPrefs.GetInt(LastPrefixIndexPrefsKey, 0);
        customPrefix = EditorPrefs.GetString(LastCustomPrefixPrefsKey, string.Empty);
        leftPaneWidth = EditorPrefs.GetFloat(LeftPaneWidthPrefsKey, 380.0f);
        RebuildPrefixOptions();
        ReloadAssets();
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        DestroySelectedEditor();
        SavePrefs();
    }

    private void Update()
    {
        ProcessPendingKeyIssueRebuild();
    }

    private void OnGUI()
    {
        ProcessPendingKeyIssueRebuild();
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            leftPaneWidth = Mathf.Clamp(leftPaneWidth, MinLeftPaneWidth, GetMaxAllowedLeftPaneWidth());

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftPaneWidth), GUILayout.ExpandHeight(true)))
            {
                DrawLeftPane();
            }

            Rect splitterRect = GUILayoutUtility.GetRect(SplitterWidth, 1.0f, GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));
            DrawSplitter(splitterRect);

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawInspectorPane();
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70.0f)))
            {
                ReloadAssets();
                Debug.Log($"Attack Data Editor loaded {attackDatas.Count} assets from {folderPath}.");
            }

            GUILayout.Space(6.0f);
            GUILayout.Label(folderPath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(duplicateKeyGroups.Count == 0 && emptyKeyDatas.Count == 0))
            {
                string issueText = duplicateKeyGroups.Count == 0 && emptyKeyDatas.Count == 0
                    ? "Key OK"
                    : $"Key Issues {duplicateKeyGroups.Count + emptyKeyDatas.Count}";
                if (GUILayout.Button(issueText, EditorStyles.toolbarButton, GUILayout.Width(100.0f)))
                {
                    showDuplicates = true;
                }
            }
        }
    }

    private void DrawLeftPane()
    {
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        DrawAssetList();
        EditorGUILayout.Space(10.0f);
        DrawCreateSection();
        EditorGUILayout.Space(8.0f);
        DrawDuplicateSection();
        EditorGUILayout.Space(8.0f);
        DrawFolderSettings();

        EditorGUILayout.EndScrollView();
    }

    private void DrawFolderSettings()
    {
        showSettings = EditorGUILayout.Foldout(showSettings, "Settings", true);
        if (!showSettings)
        {
            return;
        }

        EditorGUI.indentLevel++;

        DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        EditorGUI.BeginChangeCheck();
        DefaultAsset selectedFolder = EditorGUILayout.ObjectField("Asset Folder", folderAsset, typeof(DefaultAsset), false) as DefaultAsset;
        if (EditorGUI.EndChangeCheck())
        {
            string selectedPath = selectedFolder != null ? AssetDatabase.GetAssetPath(selectedFolder) : string.Empty;
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                folderPath = selectedPath;
                SavePrefs();
                ReloadAssets();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.TextField("Path", folderPath);
            if (GUILayout.Button("Use Selection", GUILayout.Width(100.0f)))
            {
                UseSelectedFolder();
            }
        }

        EditorGUI.BeginChangeCheck();
        prefixPatternsText = EditorGUILayout.TextField("Prefix Patterns", prefixPatternsText);
        if (EditorGUI.EndChangeCheck())
        {
            RebuildPrefixOptions();
            SavePrefs();
        }

        EditorGUI.indentLevel--;
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("Create Attack Data", EditorStyles.boldLabel);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorGUILayout.HelpBox("생성 폴더가 유효하지 않음.", MessageType.Warning);
        }

        EditorGUI.BeginChangeCheck();
        prefixIndex = EditorGUILayout.Popup("Prefix", prefixIndex, prefixOptions);
        if (EditorGUI.EndChangeCheck())
        {
            SavePrefs();
        }

        bool customPrefixSelected = IsCustomPrefixSelected();
        using (new EditorGUI.DisabledScope(!customPrefixSelected))
        {
            EditorGUI.BeginChangeCheck();
            customPrefix = EditorGUILayout.TextField("Custom Prefix", customPrefix);
            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }
        }

        bodyName = EditorGUILayout.TextField("Name", bodyName);
        suffixName = EditorGUILayout.TextField("Suffix", suffixName);

        string assetName = BuildAssetName();
        EditorGUILayout.TextField("Result", assetName);

        using (new EditorGUI.DisabledScope(!CanCreate(assetName)))
        {
            if (GUILayout.Button("Create Attack Data"))
            {
                CreateAttackData(assetName);
            }
        }
    }

    private void DrawDuplicateSection()
    {
        showDuplicates = EditorGUILayout.Foldout(showDuplicates, "Key Check", true);
        if (!showDuplicates)
        {
            return;
        }

        EditorGUI.indentLevel++;

        if (duplicateKeyGroups.Count == 0 && emptyKeyDatas.Count == 0)
        {
            EditorGUILayout.HelpBox("중복 Key 없음.", MessageType.Info);
            EditorGUI.indentLevel--;
            return;
        }

        List<KeyIssueRow> issueRows = BuildKeyIssueRows();

        Rect listRect = GUILayoutUtility.GetRect(1.0f, KeyIssueListHeight, GUILayout.ExpandWidth(true));
        GUI.Box(listRect, GUIContent.none, EditorStyles.helpBox);

        Rect headerRect = new Rect(listRect.x + 4.0f, listRect.y + 4.0f, listRect.width - 8.0f, AssetRowHeight);
        DrawKeyIssueHeader(headerRect);

        Rect scrollRect = new Rect(listRect.x + 4.0f, headerRect.yMax + 2.0f, listRect.width - 8.0f, listRect.height - AssetRowHeight - 10.0f);
        Rect viewRect = new Rect(0.0f, 0.0f, scrollRect.width - 16.0f, Mathf.Max(scrollRect.height, issueRows.Count * AssetRowHeight));

        duplicateScroll = GUI.BeginScrollView(scrollRect, duplicateScroll, viewRect);

        for (int i = 0; i < issueRows.Count; i++)
        {
            KeyIssueRow issueRow = issueRows[i];
            Rect rowRect = new Rect(0.0f, i * AssetRowHeight, viewRect.width, AssetRowHeight);
            DrawKeyIssueRow(rowRect, issueRow, i);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                SelectData(issueRow.Data);
                GUI.FocusControl(null);
                currentEvent.Use();
            }
        }

        GUI.EndScrollView();
        EditorGUI.indentLevel--;
    }

    private void DrawAssetList()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Attack Data List ({attackDatas.Count})", EditorStyles.boldLabel);
            searchText = GUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField, GUILayout.Width(150.0f));
        }

        if (loadWarnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", loadWarnings), MessageType.Warning);
        }

        if (attackDatas.Count == 0)
        {
            string message = AssetDatabase.IsValidFolder(folderPath)
                ? "지정 폴더에서 AttackDataScriptableObject를 찾지 못함."
                : "지정 폴더가 유효하지 않음.";
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("Reset To Default Folder"))
            {
                folderPath = DefaultFolderPath;
                SavePrefs();
                ReloadAssets();
            }

            return;
        }

        List<AttackDataScriptableObject> filteredDatas = attackDatas;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filteredDatas = filteredDatas.Where(data =>
                data != null &&
                (data.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (!string.IsNullOrEmpty(data.Key) && data.Key.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
        }

        Rect listRect = GUILayoutUtility.GetRect(1.0f, AssetListHeight, GUILayout.ExpandWidth(true));
        GUI.Box(listRect, GUIContent.none, EditorStyles.helpBox);

        Rect headerRect = new Rect(listRect.x + 4.0f, listRect.y + 4.0f, listRect.width - 8.0f, AssetRowHeight);
        DrawAssetListHeader(headerRect);

        Rect scrollRect = new Rect(listRect.x + 4.0f, headerRect.yMax + 2.0f, listRect.width - 8.0f, listRect.height - AssetRowHeight - 10.0f);
        Rect viewRect = new Rect(0.0f, 0.0f, scrollRect.width - 16.0f, Mathf.Max(scrollRect.height, filteredDatas.Count * AssetRowHeight));

        assetListScroll = GUI.BeginScrollView(scrollRect, assetListScroll, viewRect);

        for (int i = 0; i < filteredDatas.Count; i++)
        {
            AttackDataScriptableObject data = filteredDatas[i];
            if (data == null)
            {
                continue;
            }

            Rect rowRect = new Rect(0.0f, i * AssetRowHeight, viewRect.width, AssetRowHeight);
            DrawAssetRow(rowRect, data, i);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                SelectData(data);
                GUI.FocusControl(null);
                currentEvent.Use();
            }
        }

        GUI.EndScrollView();
    }

    private List<KeyIssueRow> BuildKeyIssueRows()
    {
        List<KeyIssueRow> issueRows = new List<KeyIssueRow>(emptyKeyDatas.Count + duplicateKeyGroups.Sum(group => group.Items.Count));

        for (int i = 0; i < emptyKeyDatas.Count; i++)
        {
            AttackDataScriptableObject data = emptyKeyDatas[i];
            if (data != null)
            {
                issueRows.Add(new KeyIssueRow("Empty Key", data, string.Empty));
            }
        }

        for (int i = 0; i < duplicateKeyGroups.Count; i++)
        {
            DuplicateKeyGroup group = duplicateKeyGroups[i];
            for (int j = 0; j < group.Items.Count; j++)
            {
                AttackDataScriptableObject data = group.Items[j];
                if (data != null)
                {
                    issueRows.Add(new KeyIssueRow($"Duplicate ({group.Items.Count})", data, group.Key));
                }
            }
        }

        return issueRows;
    }

    private static void DrawKeyIssueHeader(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        Rect issueRect = new Rect(rect.x + 6.0f, rect.y + 2.0f, rect.width * 0.30f - 8.0f, rect.height - 4.0f);
        Rect assetRect = new Rect(rect.x + rect.width * 0.30f + 4.0f, rect.y + 2.0f, rect.width * 0.40f - 8.0f, rect.height - 4.0f);
        Rect keyRect = new Rect(rect.x + rect.width * 0.70f + 4.0f, rect.y + 2.0f, rect.width * 0.30f - 10.0f, rect.height - 4.0f);

        EditorGUI.LabelField(issueRect, "Issue", EditorStyles.miniBoldLabel);
        EditorGUI.LabelField(assetRect, "Asset", EditorStyles.miniBoldLabel);
        EditorGUI.LabelField(keyRect, "Key", EditorStyles.miniBoldLabel);
    }

    private void DrawKeyIssueRow(Rect rect, KeyIssueRow issueRow, int rowIndex)
    {
        bool selected = issueRow.Data == selectedData;
        Color backgroundColor = selected
            ? new Color(0.24f, 0.38f, 0.60f, 1.0f)
            : rowIndex % 2 == 0
                ? new Color(0.20f, 0.20f, 0.20f, 1.0f)
                : new Color(0.17f, 0.17f, 0.17f, 1.0f);

        EditorGUI.DrawRect(rect, backgroundColor);

        Rect issueRect = new Rect(rect.x + 6.0f, rect.y + 2.0f, rect.width * 0.30f - 8.0f, rect.height - 4.0f);
        Rect assetRect = new Rect(rect.x + rect.width * 0.30f + 4.0f, rect.y + 2.0f, rect.width * 0.40f - 8.0f, rect.height - 4.0f);
        Rect keyRect = new Rect(rect.x + rect.width * 0.70f + 4.0f, rect.y + 2.0f, rect.width * 0.30f - 10.0f, rect.height - 4.0f);

        GUIStyle labelStyle = selected ? EditorStyles.whiteMiniLabel : EditorStyles.miniLabel;
        string keyText = string.IsNullOrEmpty(issueRow.Key) ? "<empty>" : issueRow.Key;
        EditorGUI.LabelField(issueRect, issueRow.Issue, labelStyle);
        EditorGUI.LabelField(assetRect, issueRow.Data.name, labelStyle);
        EditorGUI.LabelField(keyRect, keyText, labelStyle);
    }

    private static void DrawAssetListHeader(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        Rect nameRect = new Rect(rect.x + 6.0f, rect.y + 2.0f, rect.width * 0.55f - 8.0f, rect.height - 4.0f);
        Rect keyRect = new Rect(rect.x + rect.width * 0.55f + 4.0f, rect.y + 2.0f, rect.width * 0.45f - 10.0f, rect.height - 4.0f);

        EditorGUI.LabelField(nameRect, "Asset", EditorStyles.miniBoldLabel);
        EditorGUI.LabelField(keyRect, "Key", EditorStyles.miniBoldLabel);
    }

    private void DrawAssetRow(Rect rect, AttackDataScriptableObject data, int rowIndex)
    {
        bool selected = data == selectedData;
        Color backgroundColor = selected
            ? new Color(0.24f, 0.38f, 0.60f, 1.0f)
            : rowIndex % 2 == 0
                ? new Color(0.20f, 0.20f, 0.20f, 1.0f)
                : new Color(0.17f, 0.17f, 0.17f, 1.0f);

        EditorGUI.DrawRect(rect, backgroundColor);

        Rect nameRect = new Rect(rect.x + 6.0f, rect.y + 2.0f, rect.width * 0.55f - 8.0f, rect.height - 4.0f);
        Rect keyRect = new Rect(rect.x + rect.width * 0.55f + 4.0f, rect.y + 2.0f, rect.width * 0.45f - 10.0f, rect.height - 4.0f);

        GUIStyle labelStyle = selected ? EditorStyles.whiteMiniLabel : EditorStyles.miniLabel;
        string keyText = string.IsNullOrEmpty(data.Key) ? "<empty>" : data.Key;
        EditorGUI.LabelField(nameRect, data.name, labelStyle);
        EditorGUI.LabelField(keyRect, keyText, labelStyle);
    }

    private void DrawInspectorPane()
    {
        if (selectedData == null)
        {
            EditorGUILayout.HelpBox("왼쪽 목록에서 AttackDataScriptableObject를 선택.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField("Selected", selectedData, typeof(AttackDataScriptableObject), false);
            if (GUILayout.Button("Ping", GUILayout.Width(60.0f)))
            {
                EditorGUIUtility.PingObject(selectedData);
            }
        }

        EditorGUILayout.Space(4.0f);

        if (selectedEditor == null || selectedEditor.target != selectedData)
        {
            RebuildSelectedEditor();
        }

        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedData, "Edit Attack Data");
        try
        {
            selectedEditor.OnInspectorGUI();
        }
        catch (Exception exception)
        {
            EditorGUILayout.HelpBox("AttackData 인스펙터를 그리는 중 오류가 발생함. 자세한 내용은 Console 확인.", MessageType.Error);
            Debug.LogException(exception);
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedData);
            ScheduleKeyIssueRebuild();
        }

        EditorGUILayout.EndScrollView();
    }

    private void UseSelectedFolder()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (AssetDatabase.IsValidFolder(selectedPath))
        {
            folderPath = selectedPath;
            SavePrefs();
            ReloadAssets();
        }
    }

    private void CreateAttackData(string assetName)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"Attack Data folder is invalid: {folderPath}");
            return;
        }

        AttackDataScriptableObject asset = CreateInstance<AttackDataScriptableObject>();
        SerializedObject serializedAsset = new SerializedObject(asset);
        SerializedProperty keyProperty = serializedAsset.FindProperty(KeyPropertyName);
        if (keyProperty != null)
        {
            keyProperty.stringValue = assetName;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{assetName}.asset");
        AssetDatabase.CreateAsset(asset, assetPath);
        Undo.RegisterCreatedObjectUndo(asset, "Create Attack Data");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TryMarkAddressable(assetPath);
        ReloadAssets();

        AttackDataScriptableObject createdAsset = AssetDatabase.LoadAssetAtPath<AttackDataScriptableObject>(assetPath);
        SelectData(createdAsset);
        EditorGUIUtility.PingObject(createdAsset);
    }

    private static void TryMarkAddressable(string assetPath)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(AddressableGroupName) ?? settings.DefaultGroup;
        if (group == null)
        {
            return;
        }

        settings.AddLabel(AddressableLabel);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        if (entry == null)
        {
            return;
        }

        entry.address = assetPath;
        entry.SetLabel(AddressableLabel, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
    }

    private void ReloadAssets()
    {
        attackDatas.Clear();
        loadWarnings.Clear();

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            string[] assetPaths = FindAssetFilesInFolder(folderPath);
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                AttackDataScriptableObject data = AssetDatabase.LoadAssetAtPath<AttackDataScriptableObject>(path);
                if (data != null)
                {
                    attackDatas.Add(data);
                }
                else
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null)
                    {
                        loadWarnings.Add($"Skipped: {path} ({asset.GetType().Name})");
                    }
                }
            }
        }

        attackDatas.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        RebuildKeyIssues();

        if (selectedData != null && !attackDatas.Contains(selectedData))
        {
            SelectData(null);
        }
    }

    private static string[] FindAssetFilesInFolder(string assetFolderPath)
    {
        string projectRootPath = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRootPath))
        {
            return Array.Empty<string>();
        }

        string fullFolderPath = Path.GetFullPath(Path.Combine(projectRootPath, assetFolderPath));
        if (!Directory.Exists(fullFolderPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(fullFolderPath, "*.asset", SearchOption.AllDirectories)
            .Select(path => "Assets" + Path.GetFullPath(path)
                .Substring(Application.dataPath.Length)
                .Replace('\\', '/'))
            .ToArray();
    }

    private void RebuildKeyIssues()
    {
        keyIssueRebuildPending = false;
        duplicateKeyGroups.Clear();
        emptyKeyDatas.Clear();

        Dictionary<string, List<AttackDataScriptableObject>> keyGroups = new Dictionary<string, List<AttackDataScriptableObject>>();
        for (int i = 0; i < attackDatas.Count; i++)
        {
            AttackDataScriptableObject data = attackDatas[i];
            if (data == null)
            {
                continue;
            }

            string key = data.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                emptyKeyDatas.Add(data);
                continue;
            }

            if (!keyGroups.TryGetValue(key, out List<AttackDataScriptableObject> group))
            {
                group = new List<AttackDataScriptableObject>();
                keyGroups.Add(key, group);
            }

            group.Add(data);
        }

        foreach (KeyValuePair<string, List<AttackDataScriptableObject>> pair in keyGroups)
        {
            if (pair.Value.Count > 1)
            {
                duplicateKeyGroups.Add(new DuplicateKeyGroup(pair.Key, pair.Value));
            }
        }

        duplicateKeyGroups.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase));
        emptyKeyDatas.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
    }

    private void ScheduleKeyIssueRebuild()
    {
        keyIssueRebuildPending = true;
        keyIssueRebuildTime = EditorApplication.timeSinceStartup + KeyIssueRebuildDelay;
    }

    private void ProcessPendingKeyIssueRebuild()
    {
        if (!keyIssueRebuildPending || EditorApplication.timeSinceStartup < keyIssueRebuildTime)
        {
            return;
        }

        RebuildKeyIssues();
        Repaint();
    }

    private void DrawSplitter(Rect splitterRect)
    {
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        EditorGUI.DrawRect(splitterRect, isResizingSplitter ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.18f, 0.18f, 0.18f));

        Event currentEvent = Event.current;
        switch (currentEvent.type)
        {
            case EventType.MouseDown:
                if (splitterRect.Contains(currentEvent.mousePosition) && currentEvent.button == 0)
                {
                    isResizingSplitter = true;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isResizingSplitter)
                {
                    leftPaneWidth = Mathf.Clamp(leftPaneWidth + currentEvent.delta.x, MinLeftPaneWidth, GetMaxAllowedLeftPaneWidth());
                    Repaint();
                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (isResizingSplitter && currentEvent.button == 0)
                {
                    isResizingSplitter = false;
                    EditorPrefs.SetFloat(LeftPaneWidthPrefsKey, leftPaneWidth);
                    currentEvent.Use();
                }
                break;
        }
    }

    private float GetMaxAllowedLeftPaneWidth()
    {
        return Mathf.Max(MinLeftPaneWidth, Mathf.Min(MaxLeftPaneWidth, position.width - 260.0f));
    }

    private void OnUndoRedoPerformed()
    {
        RebuildKeyIssues();
        Repaint();
    }

    private void SelectData(AttackDataScriptableObject data)
    {
        if (selectedData == data)
        {
            return;
        }

        selectedData = data;
        rightScroll = Vector2.zero;
        RebuildSelectedEditor();
    }

    private void RebuildSelectedEditor()
    {
        DestroySelectedEditor();
        if (selectedData != null)
        {
            selectedEditor = UnityEditor.Editor.CreateEditor(selectedData);
        }
    }

    private void DestroySelectedEditor()
    {
        if (selectedEditor != null)
        {
            DestroyImmediate(selectedEditor);
            selectedEditor = null;
        }
    }

    private bool CanCreate(string assetName)
    {
        return AssetDatabase.IsValidFolder(folderPath) && !string.IsNullOrWhiteSpace(assetName);
    }

    private string BuildAssetName()
    {
        string prefix = GetSelectedPrefix();
        string[] parts =
        {
            SanitizeNamePart(prefix),
            SanitizeNamePart(bodyName),
            SanitizeNamePart(suffixName)
        };

        return string.Join("_", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private string GetSelectedPrefix()
    {
        if (IsCustomPrefixSelected())
        {
            return customPrefix;
        }

        if (prefixOptions == null || prefixOptions.Length == 0)
        {
            return string.Empty;
        }

        return prefixOptions[Mathf.Clamp(prefixIndex, 0, prefixOptions.Length - 1)];
    }

    private bool IsCustomPrefixSelected()
    {
        return prefixOptions != null &&
               prefixOptions.Length > 0 &&
               prefixIndex == prefixOptions.Length - 1;
    }

    private void RebuildPrefixOptions()
    {
        List<string> options = prefixPatternsText
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(option => option.Trim())
            .Where(option => !string.IsNullOrEmpty(option))
            .Distinct()
            .ToList();

        if (options.Count == 0)
        {
            options.Add("Character");
        }

        options.Add(CustomPrefixOption);
        prefixOptions = options.ToArray();
        prefixIndex = Mathf.Clamp(prefixIndex, 0, prefixOptions.Length - 1);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(FolderPathPrefsKey, folderPath);
        EditorPrefs.SetString(PrefixPatternsPrefsKey, prefixPatternsText);
        EditorPrefs.SetInt(LastPrefixIndexPrefsKey, prefixIndex);
        EditorPrefs.SetString(LastCustomPrefixPrefsKey, customPrefix);
    }

    private static string SanitizeNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Trim().Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray());

        return sanitized.Replace(' ', '_');
    }

    private sealed class DuplicateKeyGroup
    {
        public DuplicateKeyGroup(string key, List<AttackDataScriptableObject> items)
        {
            Key = key;
            Items = items;
        }

        public string Key { get; }
        public List<AttackDataScriptableObject> Items { get; }
    }

    private sealed class KeyIssueRow
    {
        public KeyIssueRow(string issue, AttackDataScriptableObject data, string key)
        {
            Issue = issue;
            Data = data;
            Key = key;
        }

        public string Issue { get; }
        public AttackDataScriptableObject Data { get; }
        public string Key { get; }
    }
}
