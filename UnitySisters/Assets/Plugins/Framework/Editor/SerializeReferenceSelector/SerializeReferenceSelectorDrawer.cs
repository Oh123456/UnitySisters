#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEditor;

using UnityEngine;

using UnityFramework;

[CustomPropertyDrawer(typeof(SerializeReferenceSelectorAttribute))]
public sealed class SerializeReferenceSelectorDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2.0f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        if (!HasSerializeReferenceAttribute())
        {
            EditorGUI.HelpBox(position, "Use with [SerializeReference].", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        Type baseType = GetManagedReferenceBaseType();
        DrawManagedReference(position, property, label, baseType);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!HasSerializeReferenceAttribute())
            return EditorGUIUtility.singleLineHeight * 2.0f;

        return GetManagedReferenceHeight(property);
    }

    private void DrawManagedReference(Rect position, SerializedProperty property, GUIContent label, Type baseType)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.HelpBox(position, "This field is not a managed reference.", MessageType.Warning);
            return;
        }

        Rect line = GetLine(position);
        Rect foldoutRect = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
        Rect fieldRect = EditorGUI.PrefixLabel(line, label);

        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

        DrawManagedReferenceObjectField(fieldRect, property, baseType);

        if (!property.isExpanded || property.managedReferenceValue == null)
            return;

        EditorGUI.indentLevel++;
        DrawChildProperties(position, property);
        EditorGUI.indentLevel--;
    }

    private void DrawManagedReferenceObjectField(Rect position, SerializedProperty property, Type baseType)
    {
        const float selectorButtonWidth = 19.0f;

        Rect valueRect = new Rect(position.x, position.y, position.width - selectorButtonWidth, position.height);
        Rect selectorRect = new Rect(valueRect.xMax, position.y, selectorButtonWidth, position.height);

        GUIStyle objectFieldStyle = GUI.skin.FindStyle("ObjectField") ?? EditorStyles.textField;
        GUIStyle objectFieldButtonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? EditorStyles.miniButton;

        string typeName = GetDisplayTypeName(property.managedReferenceFullTypename);
        using (new EditorGUI.DisabledScope(property.managedReferenceValue == null))
        {
            if (GUI.Button(valueRect, typeName, objectFieldStyle))
                FocusScript(property.managedReferenceValue.GetType());
        }

        if (GUI.Button(selectorRect, GUIContent.none, objectFieldButtonStyle))
            SerializeReferenceTypeSelectorWindow.Open(baseType, property.serializedObject.targetObjects, property.propertyPath);
    }

    private void DrawChildProperties(Rect position, SerializedProperty property)
    {
        SerializedProperty iterator = property.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        float y = position.y + EditorGUIUtility.singleLineHeight + VerticalSpacing;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;

            float height = EditorGUI.GetPropertyHeight(iterator, true);
            Rect rect = new Rect(position.x, y, position.width, height);
            EditorGUI.PropertyField(rect, iterator, true);
            y += height + VerticalSpacing;
        }
    }

    private float GetCollectionHeight(SerializedProperty property)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return height;

        height += EditorGUIUtility.singleLineHeight + VerticalSpacing;

        for (int i = 0; i < property.arraySize; i++)
            height += GetManagedReferenceHeight(property.GetArrayElementAtIndex(i)) + VerticalSpacing;

        height += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        return height;
    }

    private float GetManagedReferenceHeight(SerializedProperty property)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.propertyType != SerializedPropertyType.ManagedReference || !property.isExpanded || property.managedReferenceValue == null)
            return height;

        SerializedProperty iterator = property.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            height += EditorGUI.GetPropertyHeight(iterator, true) + VerticalSpacing;
        }

        return height;
    }

    private void FocusScript(Type type)
    {
        MonoScript script = SerializeReferenceTypeSelectorWindow.FindMonoScript(type);
        if (script == null)
            return;

        EditorGUIUtility.PingObject(script);
    }

    private List<Type> GetCreatableTypes(Type baseType)
    {
        if (baseType == null)
            return new List<Type>();

        List<Type> types = TypeCache.GetTypesDerivedFrom(baseType).Cast<Type>().ToList();
        if (IsCreatableManagedReferenceType(baseType, baseType))
            types.Add(baseType);

        return types
            .Where(type => IsCreatableManagedReferenceType(type, baseType))
            .OrderBy(type => type.Namespace)
            .ThenBy(type => type.Name)
            .ToList();
    }

    private bool IsCreatableManagedReferenceType(Type type, Type baseType)
    {
        if (type == null || baseType == null)
            return false;
        if (!baseType.IsAssignableFrom(type))
            return false;
        if (!type.IsClass || type.IsAbstract || type.IsGenericType)
            return false;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;
        if (!Attribute.IsDefined(type, typeof(SerializableAttribute), false))
            return false;

        ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        return constructor != null;
    }

    private bool HasSerializeReferenceAttribute()
    {
        return fieldInfo != null && Attribute.IsDefined(fieldInfo, typeof(SerializeReference), true);
    }

    private Type GetManagedReferenceBaseType()
    {
        if (fieldInfo == null)
            return null;

        Type fieldType = fieldInfo.FieldType;
        if (fieldType.IsArray)
            return fieldType.GetElementType();

        Type listType = GetEnumerableElementType(fieldType);
        return listType ?? fieldType;
    }

    private Type GetEnumerableElementType(Type type)
    {
        if (type == null || type == typeof(string))
            return null;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return type.GetGenericArguments()[0];

        Type enumerableType = type
            .GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableType == null ? null : enumerableType.GetGenericArguments()[0];
    }

    private string GetDisplayTypeName(string managedReferenceFullTypename)
    {
        if (string.IsNullOrEmpty(managedReferenceFullTypename))
            return "null";

        int splitIndex = managedReferenceFullTypename.LastIndexOf(' ');
        string typeName = splitIndex < 0 ? managedReferenceFullTypename : managedReferenceFullTypename.Substring(splitIndex + 1);
        int nestedIndex = typeName.LastIndexOf('/');
        if (nestedIndex >= 0)
            typeName = typeName.Substring(nestedIndex + 1);
        int namespaceIndex = typeName.LastIndexOf('.');
        return namespaceIndex < 0 ? typeName : typeName.Substring(namespaceIndex + 1);
    }

    private Rect GetLine(Rect position)
    {
        return new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
    }
}

public sealed class SerializeReferenceTypeSelectorWindow : EditorWindow
{
    private const float RowHeight = 24.0f;
    private const double TooltipDelay = 1.0d;

    private Type baseType;
    private UnityEngine.Object[] targets;
    private string propertyPath;
    private string searchText = string.Empty;
    private Vector2 scrollPosition;
    private List<Type> types = new List<Type>();
    private Type hoveredType;
    private Rect hoveredRect;
    private Vector2 hoveredMousePosition;
    private double hoverStartTime;
    private bool hasHoverThisFrame;

    public static void Open(Type baseType, UnityEngine.Object[] targets, string propertyPath)
    {
        SerializeReferenceTypeSelectorWindow window = CreateInstance<SerializeReferenceTypeSelectorWindow>();
        window.titleContent = new GUIContent(baseType == null ? "Select Type" : $"Select {baseType.Name}");
        window.baseType = baseType;
        window.targets = targets;
        window.propertyPath = propertyPath;
        window.types = GetCreatableTypes(baseType);
        window.minSize = new Vector2(320.0f, 300.0f);
        window.position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), new Vector2(420.0f, 420.0f));
        window.ShowAuxWindow();
        window.Focus();
    }

    private void OnEnable()
    {
        EditorApplication.update += RepaintForTooltip;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RepaintForTooltip;
    }

    private void RepaintForTooltip()
    {
        if (hoveredRect != default)
            Repaint();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Repaint)
            hasHoverThisFrame = false;

        DrawSearchField();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (DrawTypeRow(null, "None"))
            SelectType(null);

        foreach (Type type in GetFilteredTypes())
        {
            if (DrawTypeRow(type, type.Name))
                SelectType(type);
        }

        EditorGUILayout.EndScrollView();

        if (Event.current.type == EventType.Repaint && !hasHoverThisFrame)
            ClearHover();

        DrawDelayedTooltip();
    }

    private void DrawSearchField()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
        GUIStyle cancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? EditorStyles.toolbarButton;

        searchText = EditorGUILayout.TextField(searchText, searchStyle);
        if (GUILayout.Button(GUIContent.none, cancelStyle))
        {
            searchText = string.Empty;
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();
    }

    private bool DrawTypeRow(Type type, string label)
    {
        Rect rect = GUILayoutUtility.GetRect(0.0f, RowHeight, GUILayout.ExpandWidth(true));
        Event current = Event.current;

        if (rect.Contains(current.mousePosition))
        {
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.36f, 0.52f, 0.45f));
            if (current.type == EventType.Repaint)
                UpdateHover(type, rect, current.mousePosition);
        }

        Texture icon = GetTypeIcon(type);
        Rect iconRect = new Rect(rect.x + 6.0f, rect.y + 4.0f, 16.0f, 16.0f);
        Rect labelRect = new Rect(iconRect.xMax + 6.0f, rect.y + 2.0f, rect.width - 30.0f, EditorGUIUtility.singleLineHeight);

        if (icon != null)
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

        EditorGUI.LabelField(labelRect, label);

        if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
        {
            current.Use();
            return true;
        }

        return false;
    }

    private void UpdateHover(Type type, Rect rect, Vector2 mousePosition)
    {
        if (hoveredType != type)
        {
            hoveredType = type;
            hoverStartTime = EditorApplication.timeSinceStartup;
        }

        hasHoverThisFrame = true;
        hoveredRect = rect;
        hoveredMousePosition = mousePosition;
    }

    private void ClearHover()
    {
        hoveredType = null;
        hoveredRect = default;
        hoveredMousePosition = default;
        hoverStartTime = 0.0d;
    }

    private void DrawDelayedTooltip()
    {
        if (hoveredRect == default)
            return;
        if (EditorApplication.timeSinceStartup - hoverStartTime < TooltipDelay)
            return;

        string tooltip = GetTooltip(hoveredType);
        if (string.IsNullOrEmpty(tooltip))
            return;

        GUIContent content = new GUIContent(tooltip);
        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            normal =
            {
                textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black
            },
            wordWrap = true,
            padding = new RectOffset(8, 8, 6, 6)
        };

        float width = Mathf.Min(Mathf.Max(style.CalcSize(content).x + 12.0f, 220.0f), position.width - 24.0f);
        float height = style.CalcHeight(content, width) + 4.0f;
        Vector2 offset = new Vector2(18.0f, -height - 12.0f);
        Rect tooltipRect = new Rect(hoveredMousePosition + offset, new Vector2(width, height));

        if (tooltipRect.y < 4.0f)
            tooltipRect.y = hoveredMousePosition.y + 20.0f;
        if (tooltipRect.xMax > position.width - 4.0f)
            tooltipRect.x = position.width - tooltipRect.width - 4.0f;
        if (tooltipRect.x < 4.0f)
            tooltipRect.x = 4.0f;
        if (tooltipRect.yMax > position.height - 4.0f)
            tooltipRect.y = position.height - tooltipRect.height - 4.0f;

        Color background = EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f, 0.98f)
            : new Color(0.92f, 0.92f, 0.82f, 0.98f);

        EditorGUI.DrawRect(tooltipRect, background);
        GUI.Box(tooltipRect, GUIContent.none, EditorStyles.helpBox);
        GUI.Label(tooltipRect, content, style);
    }

    private IEnumerable<Type> GetFilteredTypes()
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return types;

        return types.Where(type =>
            type.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (!string.IsNullOrEmpty(type.Namespace) && type.Namespace.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private void SelectType(Type type)
    {
        if (targets == null || string.IsNullOrEmpty(propertyPath))
        {
            Close();
            return;
        }

        foreach (UnityEngine.Object target in targets)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference)
                continue;

            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type, true);
            property.isExpanded = type != null;
            serializedObject.ApplyModifiedProperties();
        }

        Close();
    }

    private Texture GetTypeIcon(Type type)
    {
        if (type == null)
            return EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;

        MonoScript script = FindMonoScript(type);
        if (script == null)
            return EditorGUIUtility.IconContent("cs Script Icon").image;

        Texture icon = AssetPreview.GetMiniThumbnail(script);
        return icon != null ? icon : EditorGUIUtility.ObjectContent(script, typeof(MonoScript)).image;
    }

    private string GetTooltip(Type type)
    {
        if (type == null)
            return "Clear this reference.";

        SerializeReferenceTooltipAttribute tooltip = type.GetCustomAttribute<SerializeReferenceTooltipAttribute>(false);
        if (tooltip == null)
            return string.Empty;

        return tooltip.Tooltip;
    }

    public static MonoScript FindMonoScript(Type type)
    {
        if (type == null)
            return null;

        string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == type)
                return script;
        }

        foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
        {
            if (script != null && script.GetClass() == type)
                return script;
        }

        return null;
    }

    private static List<Type> GetCreatableTypes(Type baseType)
    {
        if (baseType == null)
            return new List<Type>();

        List<Type> derivedTypes = TypeCache.GetTypesDerivedFrom(baseType).Cast<Type>().ToList();
        if (IsCreatableManagedReferenceType(baseType, baseType))
            derivedTypes.Add(baseType);

        return derivedTypes
            .Where(type => IsCreatableManagedReferenceType(type, baseType))
            .OrderBy(type => type.Namespace)
            .ThenBy(type => type.Name)
            .ToList();
    }

    private static bool IsCreatableManagedReferenceType(Type type, Type baseType)
    {
        if (type == null || baseType == null)
            return false;
        if (!baseType.IsAssignableFrom(type))
            return false;
        if (!type.IsClass || type.IsAbstract || type.IsGenericType)
            return false;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;
        if (!Attribute.IsDefined(type, typeof(SerializableAttribute), false))
            return false;

        ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        return constructor != null;
    }
}
#endif
