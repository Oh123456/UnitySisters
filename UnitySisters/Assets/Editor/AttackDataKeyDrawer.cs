using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AttackDataKeyAttribute))]
public sealed class AttackDataKeyDrawer : PropertyDrawer
{
    private const string AttackDataFolderPath = "Assets/Datas/AttackDatas";
    private const string FolderPathPrefsKey = "UnitySisters.AttackDataEditor.FolderPath";
    private const string NoneOption = "<None>";
    private const string MissingPrefix = "<Missing: ";

    private static readonly List<string> cachedKeys = new List<string>();
    private static double lastRefreshTime;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "AttackDataKey works with string only");
            return;
        }

        RefreshKeysIfNeeded();

        List<string> options = new List<string>(cachedKeys.Count + 2) { NoneOption };
        options.AddRange(cachedKeys);

        string currentValue = property.stringValue;
        int selectedIndex = string.IsNullOrEmpty(currentValue) ? 0 : options.IndexOf(currentValue);
        if (selectedIndex < 0)
        {
            selectedIndex = options.Count;
            options.Add($"{MissingPrefix}{currentValue}>");
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, options.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            if (newIndex == 0)
            {
                property.stringValue = string.Empty;
            }
            else if (newIndex < options.Count && !options[newIndex].StartsWith(MissingPrefix, StringComparison.Ordinal))
            {
                property.stringValue = options[newIndex];
            }
        }

        EditorGUI.EndProperty();
    }

    private static void RefreshKeysIfNeeded()
    {
        if (EditorApplication.timeSinceStartup - lastRefreshTime < 1.0f)
        {
            return;
        }

        cachedKeys.Clear();
        lastRefreshTime = EditorApplication.timeSinceStartup;

        string folderPath = EditorPrefs.GetString(FolderPathPrefsKey, AttackDataFolderPath);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] assetPaths = FindAssetFilesInFolder(folderPath);
        HashSet<string> uniqueKeys = new HashSet<string>();
        for (int i = 0; i < assetPaths.Length; i++)
        {
            AttackDataScriptableObject data = AssetDatabase.LoadAssetAtPath<AttackDataScriptableObject>(assetPaths[i]);
            if (data == null || string.IsNullOrWhiteSpace(data.Key))
            {
                continue;
            }

            uniqueKeys.Add(data.Key);
        }

        cachedKeys.AddRange(uniqueKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
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
}
