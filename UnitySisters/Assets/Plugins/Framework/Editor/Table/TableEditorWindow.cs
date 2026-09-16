using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.Table.Editor
{
    public sealed class TableEditorWindow : EditorWindow
    {
        private const float RowHeight = 22.0f;
        private const float DefaultColumnWidth = 130.0f;
        private const float NameColumnWidth = 190.0f;

        private readonly List<TableDescriptor> tables = new List<TableDescriptor>();
        private readonly HashSet<string> modifiedAssetGuids = new HashSet<string>();
        private readonly Dictionary<int, SerializedObject> serializedObjectCache = new Dictionary<int, SerializedObject>();

        private ObjectField rootFolderField;
        private ListView tableListView;
        private MultiColumnListView dataListView;
        private IMGUIContainer inspectorContainer;
        private Label statusLabel;
        private Button addRowButton;
        private Button saveButton;

        private TableDescriptor selectedTable;
        private ScriptableObject selectedAsset;
        private UnityEditor.Editor selectedEditor;
        private string rootFolderPath;

        [MenuItem("UnityFramework/Table/Editor", false, 0)]
        private static void Open()
        {
            TableEditorWindow window = GetWindow<TableEditorWindow>("Table Editor");
            window.minSize = new Vector2(860.0f, 420.0f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            rootFolderPath = TableEditorSettings.instance.RootFolderPath;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            DestroySelectedEditor();
            serializedObjectCache.Clear();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            BuildToolbar();
            BuildContent();
            ReloadTables();
        }

        private void BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();

            rootFolderField = new ObjectField("Root")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
            };
            rootFolderField.style.minWidth = 300.0f;
            rootFolderField.style.flexGrow = 1.0f;
            rootFolderField.RegisterValueChangedCallback(OnRootFolderChanged);
            toolbar.Add(rootFolderField);

            ToolbarButton useSelectionButton = new ToolbarButton(UseSelectedFolder)
            {
                text = "Use Selected",
                tooltip = "Use the selected Project folder as the table root.",
            };
            toolbar.Add(useSelectionButton);

            ToolbarButton reloadButton = new ToolbarButton(ReloadTables)
            {
                text = "Reload",
                tooltip = "Rescan ScriptableObject assets under the root folder.",
            };
            toolbar.Add(reloadButton);

            ToolbarButton createTableButton = new ToolbarButton(ShowCreateTableMenu)
            {
                text = "Create Table",
                tooltip = "Create the first row for a ScriptableObject table.",
            };
            toolbar.Add(createTableButton);

            addRowButton = new ToolbarButton(CreateRow)
            {
                text = "Add Row",
                tooltip = "Create a new ScriptableObject row in the selected table.",
            };
            toolbar.Add(addRowButton);

            saveButton = new ToolbarButton(SaveModifiedAssets)
            {
                text = "Save",
                tooltip = "Save table assets changed in this window (Ctrl+S).",
            };
            toolbar.Add(saveButton);

            rootVisualElement.Add(toolbar);
        }

        private void BuildContent()
        {
            TwoPaneSplitView outerSplit = new TwoPaneSplitView(0, 210.0f, TwoPaneSplitViewOrientation.Horizontal);
            outerSplit.style.flexGrow = 1.0f;

            VisualElement tablePane = new VisualElement();
            tablePane.style.flexGrow = 1.0f;
            tablePane.style.paddingLeft = 4.0f;
            tablePane.style.paddingRight = 4.0f;
            tablePane.Add(new Label("Tables") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            tableListView = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft } },
                bindItem = BindTableItem,
            };
            tableListView.style.flexGrow = 1.0f;
            tableListView.selectionChanged += OnTableSelectionChanged;
            tablePane.Add(tableListView);
            outerSplit.Add(tablePane);

            TwoPaneSplitView contentSplit = new TwoPaneSplitView(1, 330.0f, TwoPaneSplitViewOrientation.Horizontal);
            contentSplit.style.flexGrow = 1.0f;

            VisualElement dataPane = new VisualElement();
            dataPane.style.flexGrow = 1.0f;
            dataListView = new MultiColumnListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                horizontalScrollingEnabled = true,
                reorderable = false,
            };
            dataListView.style.flexGrow = 1.0f;
            dataListView.selectionChanged += OnAssetSelectionChanged;
            dataListView.itemsChosen += OnAssetsChosen;
            dataPane.Add(dataListView);

            statusLabel = new Label();
            statusLabel.style.height = 20.0f;
            statusLabel.style.paddingLeft = 5.0f;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            dataPane.Add(statusLabel);
            contentSplit.Add(dataPane);

            VisualElement inspectorPane = new VisualElement();
            inspectorPane.style.flexGrow = 1.0f;
            inspectorPane.style.paddingLeft = 6.0f;
            inspectorPane.style.paddingRight = 6.0f;
            inspectorPane.Add(new Label("Inspector") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            inspectorContainer = new IMGUIContainer(DrawSelectedInspector);
            inspectorContainer.style.flexGrow = 1.0f;
            inspectorPane.Add(inspectorContainer);
            contentSplit.Add(inspectorPane);

            outerSplit.Add(contentSplit);
            rootVisualElement.Add(outerSplit);
        }

        private void ReloadTables()
        {
            string selectedKey = selectedTable?.Key;
            string selectedAssetPath = selectedAsset == null ? string.Empty : AssetDatabase.GetAssetPath(selectedAsset);

            tables.Clear();
            serializedObjectCache.Clear();
            selectedTable = null;
            SelectAsset(null);

            if (!AssetDatabase.IsValidFolder(rootFolderPath))
            {
                UpdateRootFolderField();
                RefreshViews();
                SetStatus("Select a valid folder under Assets.");
                return;
            }

            Dictionary<string, TableDescriptor> tablesByKey = new Dictionary<string, TableDescriptor>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { rootFolderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                {
                    continue;
                }

                Type assetType = asset.GetType();
                string folderPath = NormalizePath(Path.GetDirectoryName(path));
                string key = $"{folderPath}|{assetType.AssemblyQualifiedName}";
                if (!tablesByKey.TryGetValue(key, out TableDescriptor table))
                {
                    table = new TableDescriptor(folderPath, assetType);
                    tablesByKey.Add(key, table);
                }

                table.Assets.Add(asset);
            }

            tables.AddRange(tablesByKey.Values);
            for (int i = 0; i < tables.Count; i++)
            {
                tables[i].Assets.Sort(CompareAssetsByName);
            }

            tables.Sort(CompareTables);
            UpdateRootFolderField();
            RefreshViews();

            TableDescriptor tableToSelect = tables.FirstOrDefault(table => table.Key == selectedKey) ?? tables.FirstOrDefault();
            if (tableToSelect != null)
            {
                int tableIndex = tables.IndexOf(tableToSelect);
                tableListView.SetSelection(tableIndex);
                SelectTable(tableToSelect);

                if (!string.IsNullOrEmpty(selectedAssetPath))
                {
                    int assetIndex = tableToSelect.Assets.FindIndex(asset => AssetDatabase.GetAssetPath(asset) == selectedAssetPath);
                    if (assetIndex >= 0)
                    {
                        dataListView.SetSelection(assetIndex);
                        SelectAsset(tableToSelect.Assets[assetIndex]);
                    }
                }
            }

            SetStatus($"{tables.Count} tables under {rootFolderPath}");
        }

        private void RefreshViews()
        {
            tableListView.itemsSource = tables;
            tableListView.Rebuild();
            addRowButton.SetEnabled(selectedTable != null);
            UpdateSaveButton();
            RebuildColumns();
        }

        private void BindTableItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            if (index < 0 || index >= tables.Count)
            {
                label.text = string.Empty;
                return;
            }

            TableDescriptor table = tables[index];
            bool dirty = table.Assets.Any(IsTrackedAndDirty);
            label.text = $"{(dirty ? "* " : string.Empty)}{table.DisplayName} ({table.Assets.Count})";
            label.tooltip = $"{table.AssetType.FullName}\n{table.FolderPath}";
        }

        private void OnTableSelectionChanged(IEnumerable<object> selection)
        {
            SelectTable(selection.OfType<TableDescriptor>().FirstOrDefault());
        }

        private void SelectTable(TableDescriptor table)
        {
            selectedTable = table;
            SelectAsset(null);
            RebuildColumns();
            addRowButton.SetEnabled(selectedTable != null);

            int count = selectedTable?.Assets.Count ?? 0;
            SetStatus(selectedTable == null
                ? "No table selected."
                : $"{selectedTable.DisplayName}: {count} rows");
        }

        private void RebuildColumns()
        {
            dataListView.columns.Clear();
            dataListView.itemsSource = selectedTable?.Assets;

            Column nameColumn = new Column
            {
                name = "assetName",
                title = "Asset",
                width = NameColumnWidth,
                minWidth = 100.0f,
                stretchable = false,
                makeCell = MakeNameCell,
                bindCell = BindNameCell,
            };
            dataListView.columns.Add(nameColumn);

            if (selectedTable != null && selectedTable.Assets.Count > 0)
            {
                List<PropertyDescriptor> properties = GetTopLevelProperties(selectedTable.Assets[0]);
                for (int i = 0; i < properties.Count; i++)
                {
                    PropertyDescriptor descriptor = properties[i];
                    Column column = new Column
                    {
                        name = descriptor.Path,
                        title = descriptor.DisplayName,
                        width = GetPreferredColumnWidth(descriptor),
                        minWidth = 80.0f,
                        stretchable = false,
                        makeCell = MakePropertyCell,
                        bindCell = (element, index) => BindPropertyCell(element, index, descriptor),
                        unbindCell = UnbindPropertyCell,
                    };
                    dataListView.columns.Add(column);
                }
            }

            dataListView.Rebuild();
        }

        private static VisualElement MakeNameCell()
        {
            return new Label
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    paddingLeft = 4.0f,
                },
            };
        }

        private void BindNameCell(VisualElement element, int index)
        {
            Label label = (Label)element;
            ScriptableObject asset = GetAssetAt(index);
            if (asset == null)
            {
                label.text = string.Empty;
                label.tooltip = string.Empty;
                return;
            }

            label.text = $"{(IsTrackedAndDirty(asset) ? "* " : string.Empty)}{asset.name}";
            label.tooltip = AssetDatabase.GetAssetPath(asset);
        }

        private static VisualElement MakePropertyCell()
        {
            IMGUIContainer container = new IMGUIContainer();
            container.style.flexGrow = 1.0f;
            container.style.height = RowHeight;
            container.onGUIHandler = () => DrawPropertyCell(container);
            return container;
        }

        private void BindPropertyCell(VisualElement element, int index, PropertyDescriptor descriptor)
        {
            IMGUIContainer container = (IMGUIContainer)element;
            container.userData = new CellBinding(this, GetAssetAt(index), descriptor);
            container.MarkDirtyRepaint();
        }

        private static void UnbindPropertyCell(VisualElement element, int index)
        {
            element.userData = null;
        }

        private static void DrawPropertyCell(IMGUIContainer container)
        {
            if (!(container.userData is CellBinding binding) || binding.Asset == null)
            {
                return;
            }

            SerializedObject serializedObject = binding.Owner.GetSerializedObject(binding.Asset);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.FindProperty(binding.Descriptor.Path);
            if (property == null)
            {
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(1.0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            rect.xMin += 2.0f;
            rect.xMax -= 2.0f;

            if (!binding.Descriptor.EditableInline)
            {
                EditorGUI.LabelField(rect, GetPropertySummary(property), EditorStyles.miniLabel);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, property, GUIContent.none, false);
            if (EditorGUI.EndChangeCheck() && serializedObject.ApplyModifiedProperties())
            {
                binding.Owner.MarkModified(binding.Asset);
            }
        }

        private void OnAssetSelectionChanged(IEnumerable<object> selection)
        {
            SelectAsset(selection.OfType<ScriptableObject>().FirstOrDefault());
        }

        private static void OnAssetsChosen(IEnumerable<object> selection)
        {
            ScriptableObject asset = selection.OfType<ScriptableObject>().FirstOrDefault();
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }

        private void SelectAsset(ScriptableObject asset)
        {
            if (selectedAsset == asset)
            {
                return;
            }

            DestroySelectedEditor();
            selectedAsset = asset;
            if (selectedAsset != null)
            {
                selectedEditor = UnityEditor.Editor.CreateEditor(selectedAsset);
            }

            inspectorContainer?.MarkDirtyRepaint();
        }

        private void DrawSelectedInspector()
        {
            if (selectedAsset == null || selectedEditor == null)
            {
                EditorGUILayout.HelpBox("Select a table row.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            selectedEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                MarkModified(selectedAsset);
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

        private void MarkModified(ScriptableObject asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
            {
                modifiedAssetGuids.Add(guid);
            }

            titleContent.text = "Table Editor *";
            UpdateSaveButton();
            tableListView?.RefreshItems();
        }

        private void SaveModifiedAssets()
        {
            List<string> savedGuids = new List<string>();
            foreach (string guid in modifiedAssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    savedGuids.Add(guid);
                    continue;
                }

                AssetDatabase.SaveAssetIfDirty(asset);
                if (!EditorUtility.IsDirty(asset))
                {
                    savedGuids.Add(guid);
                }
            }

            for (int i = 0; i < savedGuids.Count; i++)
            {
                modifiedAssetGuids.Remove(savedGuids[i]);
            }

            titleContent.text = modifiedAssetGuids.Count == 0 ? "Table Editor" : "Table Editor *";
            UpdateSaveButton();
            tableListView.RefreshItems();
            dataListView.RefreshItems();
            SetStatus(modifiedAssetGuids.Count == 0 ? "Saved." : $"{modifiedAssetGuids.Count} assets remain dirty.");
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.keyCode != KeyCode.S)
            {
                return;
            }

            SaveModifiedAssets();
            evt.StopPropagation();
        }

        private void UpdateSaveButton()
        {
            saveButton?.SetEnabled(modifiedAssetGuids.Count > 0);
        }

        private void OnUndoRedo()
        {
            dataListView?.RefreshItems();
            inspectorContainer?.MarkDirtyRepaint();
            tableListView?.RefreshItems();
        }

        private void OnProjectChanged()
        {
            rootVisualElement?.schedule.Execute(ReloadTables).StartingIn(100);
        }

        private void OnRootFolderChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            string path = evt.newValue == null ? string.Empty : AssetDatabase.GetAssetPath(evt.newValue);
            if (!TrySetRootFolder(path))
            {
                UpdateRootFolderField();
            }
        }

        private void UseSelectedFolder()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!TrySetRootFolder(path))
            {
                SetStatus("Select a folder under Assets in the Project window.");
            }
        }

        private bool TrySetRootFolder(string path)
        {
            path = NormalizePath(path);
            if (!AssetDatabase.IsValidFolder(path) || (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                return false;
            }

            rootFolderPath = path;
            TableEditorSettings.instance.SetRootFolder(path);
            ReloadTables();
            return true;
        }

        private void UpdateRootFolderField()
        {
            DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rootFolderPath);
            rootFolderField?.SetValueWithoutNotify(folder);
        }

        private void ShowCreateTableMenu()
        {
            GenericMenu menu = new GenericMenu();
            List<Type> types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(IsCreatableTableType)
                .OrderBy(type => type.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                string menuPath = GetTypeMenuPath(type);
                menu.AddItem(new GUIContent(menuPath), false, () => CreateTable(type));
            }

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No ScriptableObject types found"));
            }

            menu.ShowAsContext();
        }

        private static bool IsCreatableTableType(Type type)
        {
            if (type == null || !type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition || type.IsNestedPrivate)
            {
                return false;
            }

            Assembly assembly = type.Assembly;
            string assemblyName = assembly.GetName().Name;
            if (assemblyName.StartsWith("Unity.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal))
            {
                return false;
            }

            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static string GetTypeMenuPath(Type type)
        {
            string namespacePath = string.IsNullOrEmpty(type.Namespace)
                ? "Global"
                : type.Namespace.Replace('.', '/');
            return $"{namespacePath}/{type.Name}";
        }

        private void CreateTable(Type type)
        {
            if (!AssetDatabase.IsValidFolder(rootFolderPath))
            {
                SetStatus("Select a valid table root first.");
                return;
            }

            string folderPath = EnsureChildFolder(rootFolderPath, type.Name);
            ScriptableObject asset = CreateAsset(type, folderPath);
            if (asset == null)
            {
                return;
            }

            ReloadTables();
            SelectCreatedAsset(asset);
        }

        private void CreateRow()
        {
            if (selectedTable == null)
            {
                return;
            }

            ScriptableObject asset = CreateAsset(selectedTable.AssetType, selectedTable.FolderPath);
            if (asset == null)
            {
                return;
            }

            ReloadTables();
            SelectCreatedAsset(asset);
        }

        private static ScriptableObject CreateAsset(Type type, string folderPath)
        {
            ScriptableObject asset = CreateInstance(type);
            if (asset == null)
            {
                return null;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/New{type.Name}.asset");
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, $"Create {type.Name}");
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        private void SelectCreatedAsset(ScriptableObject asset)
        {
            if (asset == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            TableDescriptor table = tables.FirstOrDefault(candidate =>
                candidate.AssetType == asset.GetType() &&
                string.Equals(candidate.FolderPath, NormalizePath(Path.GetDirectoryName(path)), StringComparison.Ordinal));
            if (table == null)
            {
                return;
            }

            int tableIndex = tables.IndexOf(table);
            tableListView.SetSelection(tableIndex);
            SelectTable(table);

            int assetIndex = table.Assets.IndexOf(asset);
            if (assetIndex >= 0)
            {
                dataListView.SetSelection(assetIndex);
                SelectAsset(asset);
            }
        }

        private static string EnsureChildFolder(string parentFolder, string childName)
        {
            string safeName = string.Concat(childName.Select(character =>
                Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character));
            string childPath = $"{parentFolder}/{safeName}";
            if (!AssetDatabase.IsValidFolder(childPath))
            {
                AssetDatabase.CreateFolder(parentFolder, safeName);
            }

            return childPath;
        }

        private static List<PropertyDescriptor> GetTopLevelProperties(ScriptableObject asset)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                properties.Add(new PropertyDescriptor(
                    iterator.propertyPath,
                    iterator.displayName,
                    iterator.propertyType,
                    IsInlineEditable(iterator)));
            }

            return properties;
        }

        private static bool IsInlineEditable(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return false;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ManagedReference:
                case SerializedPropertyType.FixedBufferSize:
                case SerializedPropertyType.Gradient:
                case SerializedPropertyType.AnimationCurve:
                    return false;
                default:
                    return true;
            }
        }

        private static string GetPropertySummary(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return $"Count: {property.arraySize}";
            }

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                string typeName = property.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(typeName))
                {
                    return "null";
                }

                int separatorIndex = typeName.LastIndexOf(' ');
                return separatorIndex >= 0 ? typeName.Substring(separatorIndex + 1) : typeName;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.AnimationCurve:
                    return "Animation Curve";
                case SerializedPropertyType.Gradient:
                    return "Gradient";
                case SerializedPropertyType.Generic:
                    return property.type;
                default:
                    return property.displayName;
            }
        }

        private static float GetPreferredColumnWidth(PropertyDescriptor descriptor)
        {
            switch (descriptor.PropertyType)
            {
                case SerializedPropertyType.Boolean:
                    return 80.0f;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Enum:
                    return 100.0f;
                case SerializedPropertyType.Vector2:
                    return 170.0f;
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                    return 230.0f;
                case SerializedPropertyType.Color:
                    return 120.0f;
                case SerializedPropertyType.ObjectReference:
                    return 190.0f;
                default:
                    return DefaultColumnWidth;
            }
        }

        private ScriptableObject GetAssetAt(int index)
        {
            if (selectedTable == null || index < 0 || index >= selectedTable.Assets.Count)
            {
                return null;
            }

            return selectedTable.Assets[index];
        }

        private SerializedObject GetSerializedObject(ScriptableObject asset)
        {
            int instanceId = asset.GetInstanceID();
            if (!serializedObjectCache.TryGetValue(instanceId, out SerializedObject serializedObject) ||
                serializedObject.targetObject == null)
            {
                serializedObject = new SerializedObject(asset);
                serializedObjectCache[instanceId] = serializedObject;
            }

            return serializedObject;
        }

        private bool IsTrackedAndDirty(ScriptableObject asset)
        {
            if (asset == null || !EditorUtility.IsDirty(asset))
            {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return modifiedAssetGuids.Contains(guid);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static int CompareAssetsByName(ScriptableObject left, ScriptableObject right)
        {
            return string.Compare(left?.name, right?.name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareTables(TableDescriptor left, TableDescriptor right)
        {
            int folderComparison = string.Compare(left.FolderPath, right.FolderPath, StringComparison.OrdinalIgnoreCase);
            return folderComparison != 0
                ? folderComparison
                : string.Compare(left.AssetType.FullName, right.AssetType.FullName, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class TableDescriptor
        {
            internal TableDescriptor(string folderPath, Type assetType)
            {
                FolderPath = folderPath;
                AssetType = assetType;
            }

            internal string FolderPath { get; }
            internal Type AssetType { get; }
            internal List<ScriptableObject> Assets { get; } = new List<ScriptableObject>();
            internal string Key => $"{FolderPath}|{AssetType.AssemblyQualifiedName}";
            internal string DisplayName => $"{Path.GetFileName(FolderPath)} / {AssetType.Name}";
        }

        private sealed class PropertyDescriptor
        {
            internal PropertyDescriptor(string path, string displayName, SerializedPropertyType propertyType, bool editableInline)
            {
                Path = path;
                DisplayName = displayName;
                PropertyType = propertyType;
                EditableInline = editableInline;
            }

            internal string Path { get; }
            internal string DisplayName { get; }
            internal SerializedPropertyType PropertyType { get; }
            internal bool EditableInline { get; }
        }

        private sealed class CellBinding
        {
            internal CellBinding(TableEditorWindow owner, ScriptableObject asset, PropertyDescriptor descriptor)
            {
                Owner = owner;
                Asset = asset;
                Descriptor = descriptor;
            }

            internal TableEditorWindow Owner { get; }
            internal ScriptableObject Asset { get; }
            internal PropertyDescriptor Descriptor { get; }
        }
    }
}
