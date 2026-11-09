using System;
using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.BehaviorTree.Editor
{
    internal sealed partial class BehaviorTreeEditorWindow : EditorWindow
    {
        [SerializeField] private BehaviorTreeData editData;
        private BehaviorTreeData data;
        private BehaviorTreeComponent observed;
        private BehaviorTreeController observedController;
        private BehaviorTreeGraphView graph;
        private ObjectField dataField;
        private Label modeLabel;
        private Label statusLabel;
        private IMGUIContainer details;
        private IMGUIContainer blackboardPanel;
        private ToolbarButton addButton;
        private ToolbarButton saveButton;
        private ToolbarButton newTreeButton;
        private ToolbarButton newBoardButton;
        private string selectedID;
        private Vector2 detailScroll;
        private Vector2 boardScroll;
        private double nextRefresh;
        private static HashSet<string> runtimeAssemblies;
        private bool CanEdit { get => !EditorApplication.isPlayingOrWillChangePlaymode; }

        [MenuItem("UnityFramework/Behavior Tree/Editor")]
        private static void OpenMenu() { Open(null); }

        internal static void Open(BehaviorTreeData data)
        {
            BehaviorTreeEditorWindow window = GetWindow<BehaviorTreeEditorWindow>("Behavior Tree");
            window.titleContent = new GUIContent("Behavior Tree");
            window.minSize = new Vector2(950, 500);
            window.Show();
            if (window.CanEdit && data != null) window.SetData(data);
            window.RefreshSelection();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += RefreshSelection;
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.update += UpdateObservation;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RefreshSelection;
            Undo.undoRedoPerformed -= Refresh;
            EditorApplication.update -= UpdateObservation;
            EditorApplication.playModeStateChanged -= OnPlayMode;
        }

        private void OnFocus() { if (graph != null) Refresh(); }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            Toolbar toolbar = new Toolbar();
            modeLabel = new Label(); toolbar.Add(modeLabel);
            dataField = new ObjectField("Tree") { objectType = typeof(BehaviorTreeData), allowSceneObjects = false };
            dataField.style.width = 320;
            dataField.RegisterValueChangedCallback(evt => { if (CanEdit) SetData(evt.newValue as BehaviorTreeData); });
            toolbar.Add(dataField);
            newTreeButton = new ToolbarButton(() => CreateAsset<BehaviorTreeData>()) { text = "New Tree" }; toolbar.Add(newTreeButton);
            newBoardButton = new ToolbarButton(() => CreateAsset<BlackboardData>()) { text = "New Blackboard" }; toolbar.Add(newBoardButton);
            addButton = new ToolbarButton(() =>
            {
                Vector2 position = graph.contentViewContainer.WorldToLocal(graph.worldBound.center);
                ShowAddMenu(position, addButton.worldBound, addButton);
            }) { text = "Add node" }; toolbar.Add(addButton);
            saveButton = new ToolbarButton(Save) { text = "Validate / Save" }; toolbar.Add(saveButton);
            rootVisualElement.Add(toolbar);

            TwoPaneSplitView leftSplit = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            leftSplit.style.flexGrow = 1;
            blackboardPanel = new IMGUIContainer(DrawBlackboard); leftSplit.Add(blackboardPanel);
            TwoPaneSplitView rightSplit = new TwoPaneSplitView(1, 300, TwoPaneSplitViewOrientation.Horizontal);
            graph = new BehaviorTreeGraphView();
            graph.NodeSelected = id => { selectedID = id; details?.MarkDirtyRepaint(); };
            graph.Changed = Refresh;
            graph.AddRequested = ShowAddMenu;
            graph.ConnectionMessage = message => { if (statusLabel != null) statusLabel.text = message; };
            rightSplit.Add(graph);
            details = new IMGUIContainer(DrawDetails); rightSplit.Add(details);
            leftSplit.Add(rightSplit); rootVisualElement.Add(leftSplit);
            statusLabel = new Label(); rootVisualElement.Add(statusLabel);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            if (CanEdit) SetData(editData);
            RefreshSelection(); Refresh();
        }

        private void CreateAsset<T>() where T : ScriptableObject
        {
            if (!CanEdit) return;
            string path = EditorUtility.SaveFilePanelInProject("Create " + typeof(T).Name, typeof(T).Name, "asset", "Choose an asset location.");
            if (string.IsNullOrEmpty(path)) return;
            T asset = CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path);
            Selection.activeObject = asset;
            if (asset is BehaviorTreeData tree) SetData(tree);
            else if (data != null)
            {
                using (SerializedObject serialized = new SerializedObject(data))
                { serialized.FindProperty("blackboard").objectReferenceValue = asset; serialized.ApplyModifiedProperties(); }
                Refresh();
            }
        }

        private void SetData(BehaviorTreeData value)
        {
            data = value;
            if (CanEdit) editData = data;
            selectedID = null;
            dataField?.SetValueWithoutNotify(data);
            if (graph != null) graph.ShowTree(data, !CanEdit, true);
            RefreshMode();
        }

        private void RefreshSelection()
        {
            if (CanEdit)
            {
                if (Selection.activeObject is BehaviorTreeData selected) SetData(selected);
                return;
            }
            BehaviorTreeComponent selectedComponent = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<BehaviorTreeComponent>();
            BehaviorTreeController controller = selectedComponent == null ? null : selectedComponent.Controller;
            if (observed == selectedComponent && observedController == controller) return;
            observed = selectedComponent; observedController = controller;
            SetData(controller?.Model.data);
        }

        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { editData = data; observed = null; observedController = null; data = null; }
            else if (state == PlayModeStateChange.EnteredEditMode) { observed = null; observedController = null; SetData(editData); }
            else if (state == PlayModeStateChange.EnteredPlayMode) RefreshSelection();
            Refresh();
        }

        private void UpdateObservation()
        {
            if (CanEdit || graph == null || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.15;
            RefreshSelection();
            graph.ShowExecution(observedController);
            blackboardPanel?.MarkDirtyRepaint(); details?.MarkDirtyRepaint();
            if (statusLabel != null) statusLabel.text = observedController == null ? "Select a GameObject with BehaviorTreeComponent." : $"{observed.name} | Running: {observedController.IsRunning} | Result: {observedController.LastStatus}";
        }

        private void RefreshMode()
        {
            if (modeLabel != null) modeLabel.text = CanEdit ? "Edit mode" : "Play observation (read only)";
            dataField?.SetEnabled(CanEdit);
            newTreeButton?.SetEnabled(CanEdit); newBoardButton?.SetEnabled(CanEdit);
            addButton?.SetEnabled(CanEdit && data != null); saveButton?.SetEnabled(CanEdit && data != null);
            details?.MarkDirtyRepaint(); blackboardPanel?.MarkDirtyRepaint();
        }

        private void Refresh()
        {
            if (graph == null) return;
            dataField.SetValueWithoutNotify(data);
            graph.ShowTree(data, !CanEdit, false);
            RefreshMode();
            if (CanEdit && statusLabel != null)
            {
                if (data == null) statusLabel.text = "Create or select a Behavior Tree asset.";
                else { var errors = data.Validate(); statusLabel.text = errors.Count == 0 ? "Valid tree. Child order is explicit; screen position does not change priority." : string.Join("\n", errors); }
            }
        }

        private void ShowAddMenu(Vector2 position)
        {
            Vector2 anchor = graph.contentViewContainer.LocalToWorld(position);
            ShowAddMenu(position, new Rect(anchor, Vector2.zero), graph);
        }

        private void ShowAddMenu(Vector2 position, Rect anchor, VisualElement target)
        {
            if (!CanEdit || data == null) return;
            GenericDropdownMenu menu = CreateAddMenu(position);
            menu.DropDown(anchor, target, false, false);
        }

        private GenericDropdownMenu CreateAddMenu(Vector2 position)
        {
            GenericDropdownMenu menu = new GenericDropdownMenu();
            menu.AddItem("Selector", false, () => AddNode(new SelectorNode(), position));
            menu.AddItem("Sequence", false, () => AddNode(new SequenceNode(), position));
            menu.AddItem("Utility Selector", false, () => AddNode(new UtilitySelectorNode(), position));
            foreach (Type type in TypeCache.GetTypesDerivedFrom<TaskNodeData>())
                if (CanCreate(type)) { Type selected = type; menu.AddItem("Task/" + type.Name, false, () => AddNode((TaskNodeData)Activator.CreateInstance(selected, true), position)); }
            return menu;
        }

        private static bool CanCreate(Type type)
        {
            if (runtimeAssemblies == null)
            {
                runtimeAssemblies = new HashSet<string>();
                foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player)) runtimeAssemblies.Add(assembly.name);
            }
            return runtimeAssemblies.Contains(type.Assembly.GetName().Name) && !type.IsAbstract && !type.ContainsGenericParameters && Attribute.IsDefined(type, typeof(SerializableAttribute), false) && type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null) != null;
        }

        private void AddNode(BehaviorNode behavior, Vector2 position)
        {
            if (!CanEdit || data == null) return;
            Undo.RecordObject(data, "Add behavior node");
            bool occupied;
            do
            {
                occupied = false;
                foreach (BehaviorNodeData node in data.Nodes)
                    if (new Rect(node.Position, new Vector2(230, 140)).Overlaps(new Rect(position, new Vector2(230, 140)))) { position.y += 160; occupied = true; break; }
            } while (occupied);
            selectedID = data.AddNode(behavior, position).ID;
            EditorUtility.SetDirty(data); Refresh();
        }

        private void DrawBlackboard()
        {
            boardScroll = EditorGUILayout.BeginScrollView(boardScroll);
            EditorGUILayout.LabelField("Blackboard", EditorStyles.boldLabel);
            if (CanEdit && data != null)
            {
                EditorGUI.BeginChangeCheck();
                BlackboardData schema = (BlackboardData)EditorGUILayout.ObjectField("Schema", data.BlackboardData, typeof(BlackboardData), false);
                if (EditorGUI.EndChangeCheck())
                {
                    using (SerializedObject serialized = new SerializedObject(data)) { serialized.FindProperty("blackboard").objectReferenceValue = schema; serialized.ApplyModifiedProperties(); }
                    Refresh();
                }
                BlackboardEditorGUI.DrawDefinition(data.BlackboardData, true);
            }
            else if (observedController != null)
                foreach (BlackboardKeyData key in observedController.Model.data.BlackboardData.Keys)
                    EditorGUILayout.LabelField(key.Name, observedController.Model.blackboard.ReadBoxed(key.ID)?.ToString() ?? "<unset>");
            EditorGUILayout.EndScrollView();
        }

        private void DrawDetails()
        {
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            BehaviorNodeData definition = data != null ? data.FindNode(selectedID) : null;
            if (definition == null) { EditorGUILayout.HelpBox("Select a node to inspect.", MessageType.Info); EditorGUILayout.EndScrollView(); return; }
            using (SerializedObject serialized = new SerializedObject(data))
            {
                serialized.Update();
                int index = 0; while (data.Nodes[index].ID != selectedID) index++;
                SerializedProperty node = serialized.FindProperty("nodes").GetArrayElementAtIndex(index);
                using (new EditorGUI.DisabledScope(!CanEdit))
                {
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("name"));
                    EditorGUILayout.LabelField("Node", definition.Behavior?.GetType().Name ?? "Missing");
                    SerializedProperty behavior = node.FindPropertyRelative("behavior");
                    if (definition.Behavior is TaskNodeData)
                    {
                        if (GUILayout.Button("Change task type")) SerializeReferenceTypeSelectorWindow.Open(typeof(TaskNodeData), serialized.targetObjects, behavior.propertyPath);
                        EditorGUILayout.PropertyField(behavior, new GUIContent("Task settings"), true);
                    }
                    if (definition.Behavior is UtilitySelectorNode) DrawUtility(serialized, behavior, definition);
                    DrawAttachments<Decorator>(node.FindPropertyRelative("decorators"), "Decorators");
                    if (!(definition.Behavior is TaskNodeData)) DrawAttachments<Service>(node.FindPropertyRelative("services"), "Services");
                    EditorGUILayout.LabelField("Child execution order", EditorStyles.boldLabel);
                    for (int child = 0; child < definition.Children.Count; child++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{child + 1}. {data.FindNode(definition.Children[child])?.Name ?? "Missing"}");
                        if (GUILayout.Button("Up", GUILayout.Width(38)) && child > 0)
                            node.FindPropertyRelative("children").MoveArrayElement(child, child - 1);
                        if (GUILayout.Button("Down", GUILayout.Width(46)) && child + 1 < definition.Children.Count)
                            node.FindPropertyRelative("children").MoveArrayElement(child, child + 1);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                if (CanEdit && serialized.ApplyModifiedProperties()) { EditorUtility.SetDirty(data); Refresh(); }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAttachments<T>(SerializedProperty list, string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int index = 0; index < list.arraySize; index++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(index);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (GUILayout.Button("Change type")) SerializeReferenceTypeSelectorWindow.Open(typeof(T), item.serializedObject.targetObjects, item.propertyPath);
                EditorGUILayout.PropertyField(item, new GUIContent(title + " " + (index + 1)), true);
                bool remove = GUILayout.Button("Remove");
                EditorGUILayout.EndVertical();
                if (remove) { list.DeleteArrayElementAtIndex(index); break; }
            }
            if (GUILayout.Button("Add " + typeof(T).Name))
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = null;
            }
        }

        private void Save()
        {
            if (!CanEdit || data == null) return;
            var errors = data.Validate();
            foreach (BehaviorNodeData node in data.Nodes)
            {
                if (node.Behavior != null && !CanCreate(node.Behavior.GetType())) errors.Add($"{node.Name}: behavior type is not available in a player build.");
                foreach (Decorator decorator in node.Decorators) if (decorator != null && !CanCreate(decorator.GetType())) errors.Add($"{node.Name}: decorator type is not available in a player build.");
                foreach (Service service in node.Services) if (service != null && !CanCreate(service.GetType())) errors.Add($"{node.Name}: service type is not available in a player build.");
            }
            statusLabel.text = errors.Count == 0 ? "Valid tree. Saved." : string.Join("\n", errors);
            if (errors.Count != 0) return;
            AssetDatabase.SaveAssetIfDirty(data);
            if (data.BlackboardData != null) AssetDatabase.SaveAssetIfDirty(data.BlackboardData);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.actionKey && evt.keyCode == KeyCode.S) { Save(); evt.StopPropagation(); }
        }
    }
}
