using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.CommandTree.Editor
{
    public sealed class CommandTreeEditorWindow : EditorWindow
    {
        private const float toolbarFieldWidth = 300.0f;
        private const float graphWidth = 520.0f;

        private ObjectField dataField;
        private ToolbarButton createDataTypeButton;
        private ToolbarButton addRootButton;
        private ToolbarButton addChildButton;
        private ToolbarButton deleteButton;
        private ToolbarButton saveButton;
        private CommandTreeGraphView graphView;
        private IMGUIContainer detailContainer;
        private HelpBox statusBox;

        private CommandData selectedData;
        private SerializedObject serializedData;
        private CommandNodeItem selectedNode;
        private Vector2 detailScrollPosition;
        private string selectedPropertyPath;

        [MenuItem("UnityFramework/Command Tree/Editor", false, 0)]
        public static void OpenWindow()
        {
            CommandTreeEditorWindow window = GetWindow<CommandTreeEditorWindow>("Command Tree Editor");
            window.minSize = new Vector2(760.0f, 420.0f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            graphView?.SavePositions();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnFocus()
        {
            if (selectedData != null && graphView != null)
                RefreshGraph(false);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is CommandData commandData)
                SetData(commandData);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            CreateToolbar();
            CreateContent();

            statusBox = new HelpBox("Select a CommandData asset.", HelpBoxMessageType.Info);
            statusBox.style.display = DisplayStyle.Flex;
            rootVisualElement.Add(statusBox);

            if (selectedData == null && Selection.activeObject is CommandData commandData)
                selectedData = commandData;

            SetData(selectedData);
        }

        private void CreateToolbar()
        {
            Toolbar toolbar = new Toolbar();

            dataField = new ObjectField("Data")
            {
                objectType = typeof(CommandData),
                allowSceneObjects = false
            };
            dataField.style.width = toolbarFieldWidth;
            dataField.RegisterValueChangedCallback(changeEvent => SetData(changeEvent.newValue as CommandData));
            toolbar.Add(dataField);

            createDataTypeButton = new ToolbarButton(CommandDataTypeGeneratorWindow.OpenWindow)
            {
                text = "Create Data Type"
            };
            toolbar.Add(createDataTypeButton);

            addRootButton = new ToolbarButton(AddRootNode)
            {
                text = "Add Root"
            };
            toolbar.Add(addRootButton);

            addChildButton = new ToolbarButton(AddChildNode)
            {
                text = "Add Child"
            };
            toolbar.Add(addChildButton);

            deleteButton = new ToolbarButton(DeleteSelectedNode)
            {
                text = "Delete"
            };
            toolbar.Add(deleteButton);

            saveButton = new ToolbarButton(SaveData)
            {
                text = "Save"
            };
            toolbar.Add(saveButton);

            rootVisualElement.Add(toolbar);
        }

        private void CreateContent()
        {
            TwoPaneSplitView splitView = new TwoPaneSplitView(0, graphWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1.0f;

            graphView = new CommandTreeGraphView();
            graphView.AddRootRequested = AddRootNode;
            graphView.AddChildRequested = AddChildNode;
            graphView.DeleteRequested = DeleteNode;
            graphView.NodeSelected = SelectNode;
            splitView.Add(graphView);

            detailContainer = new IMGUIContainer(DrawSelectedNode);
            detailContainer.style.flexGrow = 1.0f;
            detailContainer.style.paddingLeft = 6.0f;
            detailContainer.style.paddingRight = 6.0f;
            detailContainer.style.paddingTop = 4.0f;
            splitView.Add(detailContainer);

            rootVisualElement.Add(splitView);
        }

        private void SelectNode(CommandNodeItem item)
        {
            if (item == null)
                return;

            selectedNode = item;
            selectedPropertyPath = item.propertyPath;
            UpdateButtonState();
            detailContainer?.MarkDirtyRepaint();
        }

        private void SetData(CommandData commandData)
        {
            if (selectedData == commandData && serializedData != null)
            {
                RefreshGraph(false);
                return;
            }

            selectedData = commandData;
            serializedData = selectedData == null ? null : new SerializedObject(selectedData);
            selectedNode = null;
            selectedPropertyPath = null;
            detailScrollPosition = Vector2.zero;

            if (dataField != null)
                dataField.SetValueWithoutNotify(selectedData);

            RefreshGraph(true);
        }

        private void RefreshGraph(bool frameGraph)
        {
            if (graphView == null)
                return;

            string refreshSelectionPath = selectedPropertyPath;
            selectedNode = null;
            selectedPropertyPath = refreshSelectionPath;

            List<CommandNodeItem> rootItems = new List<CommandNodeItem>();
            if (serializedData != null)
            {
                serializedData.UpdateIfRequiredOrScript();
                SerializedProperty rootProperty = GetRootProperty();
                SerializedProperty childrenProperty = rootProperty?.FindPropertyRelative("children");
                if (childrenProperty != null)
                {
                    for (int childIndex = 0; childIndex < childrenProperty.arraySize; childIndex++)
                    {
                        SerializedProperty childProperty = childrenProperty.GetArrayElementAtIndex(childIndex);
                        rootItems.Add(CreateGraphItem(childProperty, childrenProperty.propertyPath, childIndex, "root"));
                    }
                }
            }

            if (selectedNode == null)
                selectedPropertyPath = null;
            graphView.SetTree(GetDataContextKey(), serializedData == null ? null : rootItems, selectedPropertyPath, frameGraph);
            UpdateButtonState();
            detailContainer?.MarkDirtyRepaint();

            if (selectedData == null)
                SetStatus("Select a CommandData asset.", HelpBoxMessageType.Info);
            else if (GetRootProperty() == null)
                SetStatus("The selected asset does not contain a serialized CommandTree.", HelpBoxMessageType.Error);
            else
                ValidateData(false);
        }

        private CommandNodeItem CreateGraphItem(SerializedProperty nodeProperty, string parentChildrenPath, int index, string parentIdentity)
        {
            SerializedProperty keyProperty = nodeProperty.FindPropertyRelative("key");
            SerializedProperty valueProperty = nodeProperty.FindPropertyRelative("value");
            string identity = CreateNodeIdentity(parentIdentity, keyProperty);
            CommandNodeItem item = new CommandNodeItem(identity, parentIdentity, nodeProperty.propertyPath, parentChildrenPath, index, GetPropertyDisplayValue(keyProperty), GetManagedReferenceTypeName(valueProperty));

            if (nodeProperty.propertyPath == selectedPropertyPath)
                selectedNode = item;

            SerializedProperty childrenProperty = nodeProperty.FindPropertyRelative("children");
            if (childrenProperty != null)
            {
                for (int childIndex = 0; childIndex < childrenProperty.arraySize; childIndex++)
                {
                    SerializedProperty childProperty = childrenProperty.GetArrayElementAtIndex(childIndex);
                    item.children.Add(CreateGraphItem(childProperty, childrenProperty.propertyPath, childIndex, identity));
                }
            }

            return item;
        }

        private void AddRootNode()
        {
            if (serializedData == null)
                return;

            serializedData.UpdateIfRequiredOrScript();
            SerializedProperty rootProperty = GetRootProperty();
            AddNode(rootProperty?.FindPropertyRelative("children"));
        }

        private void AddChildNode()
        {
            AddChildNode(selectedNode);
        }

        private void AddChildNode(CommandNodeItem item)
        {
            if (serializedData == null || item == null)
                return;

            serializedData.UpdateIfRequiredOrScript();
            SerializedProperty nodeProperty = serializedData.FindProperty(item.propertyPath);
            AddNode(nodeProperty?.FindPropertyRelative("children"));
        }

        private void AddNode(SerializedProperty childrenProperty)
        {
            if (selectedData == null || serializedData == null || childrenProperty == null)
                return;

            Undo.RecordObject(selectedData, "Add Command Node");

            int nodeIndex = childrenProperty.arraySize;
            childrenProperty.InsertArrayElementAtIndex(nodeIndex);
            SerializedProperty nodeProperty = childrenProperty.GetArrayElementAtIndex(nodeIndex);
            ResetNode(nodeProperty);
            if (!TrySetUniqueDefaultKey(nodeProperty.FindPropertyRelative("key"), childrenProperty, nodeIndex))
            {
                childrenProperty.DeleteArrayElementAtIndex(nodeIndex);
                serializedData.ApplyModifiedPropertiesWithoutUndo();
                SetStatus("No unused key is available under this node.", HelpBoxMessageType.Warning);
                return;
            }

            selectedPropertyPath = nodeProperty.propertyPath;

            serializedData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(selectedData);
            RefreshGraph(false);
        }

        private void DeleteSelectedNode()
        {
            DeleteNode(selectedNode);
        }

        private void DeleteNode(CommandNodeItem item)
        {
            if (selectedData == null || serializedData == null || item == null)
                return;

            serializedData.UpdateIfRequiredOrScript();
            SerializedProperty childrenProperty = serializedData.FindProperty(item.parentChildrenPath);
            if (childrenProperty == null || item.index < 0 || item.index >= childrenProperty.arraySize)
                return;

            Undo.RecordObject(selectedData, "Delete Command Node");
            childrenProperty.DeleteArrayElementAtIndex(item.index);
            serializedData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(selectedData);
            selectedNode = null;
            selectedPropertyPath = null;
            RefreshGraph(false);
        }

        private void ResetNode(SerializedProperty nodeProperty)
        {
            SerializedProperty valueProperty = nodeProperty.FindPropertyRelative("value");
            SerializedProperty childrenProperty = nodeProperty.FindPropertyRelative("children");

            if (valueProperty != null)
                valueProperty.managedReferenceValue = null;
            if (childrenProperty != null)
                childrenProperty.arraySize = 0;
        }

        private bool TrySetUniqueDefaultKey(SerializedProperty keyProperty, SerializedProperty siblingsProperty, int nodeIndex)
        {
            if (keyProperty == null)
                return false;

            switch (keyProperty.propertyType)
            {
                case SerializedPropertyType.Enum:
                    for (int enumIndex = 0; enumIndex < keyProperty.enumDisplayNames.Length; enumIndex++)
                    {
                        if (!ContainsEnumValue(siblingsProperty, nodeIndex, enumIndex))
                        {
                            keyProperty.enumValueIndex = enumIndex;
                            return true;
                        }
                    }
                    return false;

                case SerializedPropertyType.Integer:
                    long integerValue = 0;
                    while (ContainsIntegerValue(siblingsProperty, nodeIndex, integerValue))
                        integerValue++;
                    keyProperty.longValue = integerValue;
                    return true;

                case SerializedPropertyType.String:
                    string keyName = "New Key";
                    int suffix = 2;
                    while (ContainsStringValue(siblingsProperty, nodeIndex, keyName))
                        keyName = $"New Key {suffix++}";
                    keyProperty.stringValue = keyName;
                    return true;

                case SerializedPropertyType.Boolean:
                    if (!ContainsBooleanValue(siblingsProperty, nodeIndex, false))
                        keyProperty.boolValue = false;
                    else if (!ContainsBooleanValue(siblingsProperty, nodeIndex, true))
                        keyProperty.boolValue = true;
                    else
                        return false;
                    return true;

                case SerializedPropertyType.Float:
                    double floatValue = 0.0d;
                    while (ContainsFloatValue(siblingsProperty, nodeIndex, floatValue))
                        floatValue += 1.0d;
                    keyProperty.doubleValue = floatValue;
                    return true;
            }

            return false;
        }

        private bool ContainsEnumValue(SerializedProperty siblingsProperty, int excludedIndex, int value)
        {
            for (int index = 0; index < siblingsProperty.arraySize; index++)
            {
                if (index == excludedIndex)
                    continue;
                SerializedProperty keyProperty = siblingsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                if (keyProperty != null && keyProperty.enumValueIndex == value)
                    return true;
            }
            return false;
        }

        private bool ContainsIntegerValue(SerializedProperty siblingsProperty, int excludedIndex, long value)
        {
            for (int index = 0; index < siblingsProperty.arraySize; index++)
            {
                if (index == excludedIndex)
                    continue;
                SerializedProperty keyProperty = siblingsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                if (keyProperty != null && keyProperty.longValue == value)
                    return true;
            }
            return false;
        }

        private bool ContainsStringValue(SerializedProperty siblingsProperty, int excludedIndex, string value)
        {
            for (int index = 0; index < siblingsProperty.arraySize; index++)
            {
                if (index == excludedIndex)
                    continue;
                SerializedProperty keyProperty = siblingsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                if (keyProperty != null && keyProperty.stringValue == value)
                    return true;
            }
            return false;
        }

        private bool ContainsBooleanValue(SerializedProperty siblingsProperty, int excludedIndex, bool value)
        {
            for (int index = 0; index < siblingsProperty.arraySize; index++)
            {
                if (index == excludedIndex)
                    continue;
                SerializedProperty keyProperty = siblingsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                if (keyProperty != null && keyProperty.boolValue == value)
                    return true;
            }
            return false;
        }

        private bool ContainsFloatValue(SerializedProperty siblingsProperty, int excludedIndex, double value)
        {
            for (int index = 0; index < siblingsProperty.arraySize; index++)
            {
                if (index == excludedIndex)
                    continue;
                SerializedProperty keyProperty = siblingsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("key");
                if (keyProperty != null && keyProperty.doubleValue.Equals(value))
                    return true;
            }
            return false;
        }

        private void DrawSelectedNode()
        {
            detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);

            if (selectedData == null)
            {
                EditorGUILayout.HelpBox("Select a CommandData asset.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            SerializedProperty nodeProperty = GetSelectedNodeProperty();
            if (nodeProperty == null)
            {
                EditorGUILayout.HelpBox("Select a command node.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            serializedData.UpdateIfRequiredOrScript();
            SerializedProperty keyProperty = nodeProperty.FindPropertyRelative("key");
            SerializedProperty valueProperty = nodeProperty.FindPropertyRelative("value");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("Key"), true);
            EditorGUILayout.Space(4.0f);
            DrawBehavior(valueProperty);

            if (EditorGUI.EndChangeCheck())
            {
                serializedData.ApplyModifiedProperties();
                EditorUtility.SetDirty(selectedData);
                graphView.RefreshNode(selectedPropertyPath, CreateNodeIdentity(selectedNode.parentIdentity, keyProperty), GetPropertyDisplayValue(keyProperty), GetManagedReferenceTypeName(valueProperty));
                ValidateData(false);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBehavior(SerializedProperty valueProperty)
        {
            if (valueProperty == null)
            {
                EditorGUILayout.HelpBox("Behavior property was not found.", MessageType.Error);
                return;
            }

            Rect line = EditorGUILayout.GetControlRect();
            Rect fieldRect = EditorGUI.PrefixLabel(line, new GUIContent("Behavior"));
            const float selectorWidth = 19.0f;
            Rect valueRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - selectorWidth, fieldRect.height);
            Rect selectorRect = new Rect(valueRect.xMax, fieldRect.y, selectorWidth, fieldRect.height);

            GUIStyle objectFieldStyle = GUI.skin.FindStyle("ObjectField") ?? EditorStyles.textField;
            GUIStyle objectFieldButtonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? EditorStyles.miniButton;
            string typeName = GetManagedReferenceTypeName(valueProperty);

            using (new EditorGUI.DisabledScope(valueProperty.managedReferenceValue == null))
            {
                if (GUI.Button(valueRect, typeName, objectFieldStyle))
                {
                    Type behaviorType = valueProperty.managedReferenceValue.GetType();
                    MonoScript script = SerializeReferenceTypeSelectorWindow.FindMonoScript(behaviorType);
                    if (script != null)
                        EditorGUIUtility.PingObject(script);
                }
            }

            if (GUI.Button(selectorRect, GUIContent.none, objectFieldButtonStyle))
                SerializeReferenceTypeSelectorWindow.Open(typeof(ICommandBehavior), serializedData.targetObjects, valueProperty.propertyPath);

            if (valueProperty.managedReferenceValue == null)
                return;

            valueProperty.isExpanded = EditorGUILayout.Foldout(valueProperty.isExpanded, "Behavior Data", true);
            if (!valueProperty.isExpanded)
                return;

            EditorGUI.indentLevel++;
            SerializedProperty iterator = valueProperty.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(iterator, true);
            }
            EditorGUI.indentLevel--;
        }

        private SerializedProperty GetRootProperty()
        {
            if (serializedData == null)
                return null;

            SerializedProperty commandTreeProperty = serializedData.FindProperty("commandTree");
            return commandTreeProperty?.FindPropertyRelative("root");
        }

        private string GetDataContextKey()
        {
            if (selectedData == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(selectedData);
            if (!string.IsNullOrEmpty(assetPath))
                return AssetDatabase.AssetPathToGUID(assetPath);

            return $"instance:{selectedData.GetInstanceID()}";
        }

        private SerializedProperty GetSelectedNodeProperty()
        {
            if (serializedData == null || selectedNode == null)
                return null;

            return serializedData.FindProperty(selectedNode.propertyPath);
        }

        private string GetPropertyDisplayValue(SerializedProperty property)
        {
            if (property == null)
                return "<missing key>";

            switch (property.propertyType)
            {
                case SerializedPropertyType.Enum:
                    if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length)
                        return property.enumDisplayNames[property.enumValueIndex];
                    return property.intValue.ToString();
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("0.###");
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(property.stringValue) ? "<empty>" : property.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null ? "null" : property.objectReferenceValue.name;
                default:
                    try
                    {
                        object value = property.boxedValue;
                        return value == null ? "null" : value.ToString();
                    }
                    catch
                    {
                        return property.displayName;
                    }
            }
        }

        private string CreateNodeIdentity(string parentIdentity, SerializedProperty property)
        {
            string value;
            if (property == null)
            {
                value = "missing";
            }
            else
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Integer:
                        value = property.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.Boolean:
                        value = property.boolValue ? "1" : "0";
                        break;
                    case SerializedPropertyType.Float:
                        value = property.doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.String:
                        value = property.stringValue ?? string.Empty;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        value = property.objectReferenceValue == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(property.objectReferenceValue).ToString();
                        break;
                    default:
                        value = GetPropertyDisplayValue(property);
                        break;
                }
            }

            return $"{parentIdentity}/{(int)(property?.propertyType ?? SerializedPropertyType.Generic)}:{value.Length}:{value}";
        }

        private string GetManagedReferenceTypeName(SerializedProperty property)
        {
            if (property == null || string.IsNullOrEmpty(property.managedReferenceFullTypename))
                return "null";

            string fullTypeName = property.managedReferenceFullTypename;
            int assemblySplitIndex = fullTypeName.LastIndexOf(' ');
            if (assemblySplitIndex >= 0)
                fullTypeName = fullTypeName.Substring(assemblySplitIndex + 1);
            int nestedIndex = fullTypeName.LastIndexOf('/');
            if (nestedIndex >= 0)
                fullTypeName = fullTypeName.Substring(nestedIndex + 1);
            int namespaceIndex = fullTypeName.LastIndexOf('.');
            return namespaceIndex < 0 ? fullTypeName : fullTypeName.Substring(namespaceIndex + 1);
        }

        private bool ValidateData(bool showSuccess)
        {
            if (selectedData == null)
                return false;

            try
            {
                selectedData.CompleteSetup();
                if (showSuccess)
                    SetStatus($"Valid command tree. Behaviors: {selectedData.Count}", HelpBoxMessageType.Info);
                else
                    SetStatus($"Behaviors: {selectedData.Count}", HelpBoxMessageType.None);
                return true;
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, HelpBoxMessageType.Error);
                return false;
            }
        }

        private void SaveData()
        {
            if (selectedData == null || !ValidateData(true))
                return;

            EditorUtility.SetDirty(selectedData);
            AssetDatabase.SaveAssetIfDirty(selectedData);
        }

        private void OnKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.S || !keyEvent.actionKey)
                return;

            SaveData();
            keyEvent.StopPropagation();
        }

        private void OnUndoRedo()
        {
            RefreshGraph(false);
        }

        private void OnProjectChanged()
        {
            if (selectedData == null)
                return;

            RefreshGraph(false);
        }

        private void UpdateButtonState()
        {
            bool hasData = selectedData != null && serializedData != null && GetRootProperty() != null;
            addRootButton?.SetEnabled(hasData);
            addChildButton?.SetEnabled(hasData && selectedNode != null);
            deleteButton?.SetEnabled(hasData && selectedNode != null);
            saveButton?.SetEnabled(hasData);
        }

        private void SetStatus(string message, HelpBoxMessageType messageType)
        {
            if (statusBox == null)
                return;

            statusBox.text = message;
            statusBox.messageType = messageType;
            statusBox.style.display = DisplayStyle.Flex;
        }
    }
}
