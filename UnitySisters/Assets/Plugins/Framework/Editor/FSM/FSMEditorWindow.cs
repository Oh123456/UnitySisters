using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.FSM.Editor
{
    public class FSMEditorWindow : EditorWindow
    {
        private enum ViewMode
        {
            AssetEdit,
            LiveDebug
        }

        private const string StylePath = "Assets/Plugins/Framework/Editor/FSM/FSMEditor.uss";
        private const double MachineRefreshInterval = 0.25d;
        private const double TransitionHighlightDuration = 1.25d;
        private const int MaxHistoryCount = 50;

        private readonly List<IStateMachine> registeredStateMachines = new List<IStateMachine>();
        private readonly List<IStateMachine> scanResults = new List<IStateMachine>();
        private readonly List<string> machineNames = new List<string>();
        private readonly List<string> transitionHistory = new List<string>();

        private ViewMode viewMode;
        private ToolbarToggle assetModeToggle;
        private ToolbarToggle liveModeToggle;
        private ObjectField dataField;
        private DropdownField machineDropdown;
        private Label playModeStatusLabel;
        private Label ownerValueLabel;
        private Label runningValueLabel;
        private Label currentStateValueLabel;
        private Label stateCountValueLabel;
        private Label transitionCountValueLabel;
        private VisualElement editPanel;
        private Label selectionTypeLabel;
        private TextField nameField;
        private Toggle initialStateToggle;
        private TextField conditionKeyField;
        private IntegerField priorityField;
        private ListView historyList;
        private FSMGraphView graphView;
        private FSMData selectedData;
        private IStateMachine selectedStateMachine;
        private object selectedElementData;
        private double nextMachineRefreshTime;
        private double transitionHighlightEndTime;
        private bool isUpdatingFields;

        [MenuItem("Tools/FSM/Editor")]
        public static void OpenWindow()
        {
            FSMEditorWindow window = GetWindow<FSMEditorWindow>();
            window.titleContent = new GUIContent("FSM Editor");
            window.minSize = new Vector2(860.0f, 500.0f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            SetSelectedStateMachine(null);
        }

        private void OnSelectionChange()
        {
            if (this.viewMode == ViewMode.AssetEdit && Selection.activeObject is FSMData fsmData)
                SetSelectedData(fsmData);
        }

        /// <summary>
        /// 에셋 편집과 라이브 디버깅에서 함께 사용할 툴바, 그래프와 상세 패널 구성
        /// </summary>
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.Add(CreateToolbar());

            var splitView = new TwoPaneSplitView(1, 300.0f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1.0f;
            rootVisualElement.Add(splitView);

            this.graphView = new FSMGraphView();
            this.graphView.CreateStateRequested = CreateState;
            this.graphView.StateMoved = MoveState;
            this.graphView.StateRemoved = RemoveState;
            this.graphView.TransitionCreated = CreateTransition;
            this.graphView.TransitionRemoved = RemoveTransition;
            this.graphView.ElementSelected = SetSelectedElementData;
            splitView.Add(this.graphView);
            splitView.Add(CreateDetailPanel());

            if (Selection.activeObject is FSMData selectedFSMData)
                this.selectedData = selectedFSMData;

            SetViewMode(EditorApplication.isPlaying ? ViewMode.LiveDebug : ViewMode.AssetEdit);
        }

        private Toolbar CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("fsm-toolbar");

            this.assetModeToggle = new ToolbarToggle { text = "Asset Edit" };
            this.assetModeToggle.RegisterValueChangedCallback(changeEvent =>
            {
                if (changeEvent.newValue)
                    SetViewMode(ViewMode.AssetEdit);
                else if (this.viewMode == ViewMode.AssetEdit)
                    this.assetModeToggle.SetValueWithoutNotify(true);
            });
            toolbar.Add(this.assetModeToggle);

            this.liveModeToggle = new ToolbarToggle { text = "Live Debug" };
            this.liveModeToggle.RegisterValueChangedCallback(changeEvent =>
            {
                if (changeEvent.newValue)
                    SetViewMode(ViewMode.LiveDebug);
                else if (this.viewMode == ViewMode.LiveDebug)
                    this.liveModeToggle.SetValueWithoutNotify(true);
            });
            toolbar.Add(this.liveModeToggle);

            this.dataField = new ObjectField
            {
                objectType = typeof(FSMData),
                allowSceneObjects = false
            };
            this.dataField.AddToClassList("fsm-data-field");
            this.dataField.RegisterValueChangedCallback(changeEvent =>
                SetSelectedData(changeEvent.newValue as FSMData));
            toolbar.Add(this.dataField);

            var createButton = new ToolbarButton(CreateFSMDataAsset) { text = "New Asset" };
            createButton.AddToClassList("fsm-asset-control");
            toolbar.Add(createButton);

            this.machineDropdown = new DropdownField("Machine");
            this.machineDropdown.AddToClassList("fsm-machine-dropdown");
            this.machineDropdown.RegisterValueChangedCallback(OnMachineDropdownChanged);
            toolbar.Add(this.machineDropdown);

            var refreshButton = new ToolbarButton(RefreshCurrentView) { text = "Refresh" };
            toolbar.Add(refreshButton);

            var frameButton = new ToolbarButton(() => this.graphView?.FrameAll()) { text = "Frame All" };
            toolbar.Add(frameButton);

            this.playModeStatusLabel = new Label();
            this.playModeStatusLabel.AddToClassList("fsm-play-status");
            toolbar.Add(this.playModeStatusLabel);
            return toolbar;
        }

        private VisualElement CreateDetailPanel()
        {
            var detailPanel = new VisualElement();
            detailPanel.AddToClassList("fsm-detail-panel");

            Label machineTitle = new Label("Machine");
            machineTitle.AddToClassList("fsm-section-title");
            detailPanel.Add(machineTitle);

            this.ownerValueLabel = AddDetailRow(detailPanel, "Source");
            this.runningValueLabel = AddDetailRow(detailPanel, "Running");
            this.currentStateValueLabel = AddDetailRow(detailPanel, "Current State");
            this.stateCountValueLabel = AddDetailRow(detailPanel, "States");
            this.transitionCountValueLabel = AddDetailRow(detailPanel, "Transitions");

            this.editPanel = CreateEditPanel();
            detailPanel.Add(this.editPanel);

            Label historyTitle = new Label("Transition History");
            historyTitle.AddToClassList("fsm-section-title");
            detailPanel.Add(historyTitle);

            this.historyList = new ListView
            {
                itemsSource = this.transitionHistory,
                fixedItemHeight = 42.0f,
                selectionType = SelectionType.None,
                makeItem = CreateHistoryItem,
                bindItem = BindHistoryItem
            };
            this.historyList.AddToClassList("fsm-history-list");
            detailPanel.Add(this.historyList);
            return detailPanel;
        }

        /// <summary>
        /// 선택한 상태 또는 전이의 직렬화 값을 수정할 상세 패널 구성
        /// </summary>
        private VisualElement CreateEditPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("fsm-edit-panel");

            this.selectionTypeLabel = new Label("No Selection");
            this.selectionTypeLabel.AddToClassList("fsm-section-title");
            panel.Add(this.selectionTypeLabel);

            this.nameField = new TextField("Name");
            this.nameField.RegisterValueChangedCallback(OnNameChanged);
            panel.Add(this.nameField);

            this.initialStateToggle = new Toggle("Initial State");
            this.initialStateToggle.RegisterValueChangedCallback(OnInitialStateChanged);
            panel.Add(this.initialStateToggle);

            this.conditionKeyField = new TextField("Condition Key");
            this.conditionKeyField.RegisterValueChangedCallback(OnConditionKeyChanged);
            panel.Add(this.conditionKeyField);

            this.priorityField = new IntegerField("Priority");
            this.priorityField.RegisterValueChangedCallback(OnPriorityChanged);
            panel.Add(this.priorityField);
            return panel;
        }

        private static Label AddDetailRow(VisualElement parent, string title)
        {
            var row = new VisualElement();
            row.AddToClassList("fsm-detail-row");

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("fsm-detail-name");
            row.Add(titleLabel);

            Label valueLabel = new Label("-");
            valueLabel.AddToClassList("fsm-detail-value");
            row.Add(valueLabel);
            parent.Add(row);
            return valueLabel;
        }

        private static VisualElement CreateHistoryItem()
        {
            var label = new Label();
            label.AddToClassList("fsm-history-item");
            return label;
        }

        private void BindHistoryItem(VisualElement element, int index)
        {
            if (element is Label label && index >= 0 && index < this.transitionHistory.Count)
                label.text = this.transitionHistory[index];
        }

        /// <summary>
        /// 에셋 편집 또는 라이브 디버깅 모드로 전환하고 관련 컨트롤만 표시
        /// </summary>
        private void SetViewMode(ViewMode mode)
        {
            this.viewMode = mode;
            this.assetModeToggle?.SetValueWithoutNotify(mode == ViewMode.AssetEdit);
            this.liveModeToggle?.SetValueWithoutNotify(mode == ViewMode.LiveDebug);
            SetDisplay(this.dataField, mode == ViewMode.AssetEdit);
            SetDisplay(rootVisualElement.Q<ToolbarButton>(className: "fsm-asset-control"),
                mode == ViewMode.AssetEdit);
            SetDisplay(this.machineDropdown, mode == ViewMode.LiveDebug);
            SetDisplay(this.editPanel, mode == ViewMode.AssetEdit);
            SetDisplay(this.historyList, mode == ViewMode.LiveDebug);

            if (mode == ViewMode.AssetEdit)
            {
                SetSelectedStateMachine(null);
                this.dataField?.SetValueWithoutNotify(this.selectedData);
                this.graphView?.SetFSMData(this.selectedData);
            }
            else
            {
                RefreshStateMachineList(true);
            }

            UpdateDetailPanel();
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetSelectedData(FSMData fsmData)
        {
            this.selectedData = fsmData;
            this.dataField?.SetValueWithoutNotify(fsmData);
            if (this.viewMode == ViewMode.AssetEdit)
                this.graphView?.SetFSMData(fsmData);
            SetSelectedElementData(null);
            UpdateDetailPanel();
        }

        private void CreateFSMDataAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create FSM Data",
                "FSMData",
                "asset",
                "Select a location for the FSM data asset.");
            if (string.IsNullOrWhiteSpace(path))
                return;

            var fsmData = CreateInstance<FSMData>();
            AssetDatabase.CreateAsset(fsmData, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = fsmData;
            SetSelectedData(fsmData);
        }

        private FSMStateData CreateState(Vector2 position)
        {
            if (this.selectedData == null)
                return null;

            Undo.RecordObject(this.selectedData, "Create FSM State");
            FSMStateData state = this.selectedData.AddState("New State", position);
            SaveSelectedData();
            UpdateDetailPanel();
            return state;
        }

        private void MoveState(FSMStateData state, Vector2 position)
        {
            if (this.selectedData == null || state == null || state.Position == position)
                return;

            Undo.RecordObject(this.selectedData, "Move FSM State");
            state.SetPosition(position);
            SaveSelectedData();
        }

        private void RemoveState(FSMStateData state)
        {
            if (this.selectedData == null || state == null)
                return;

            Undo.RecordObject(this.selectedData, "Remove FSM State");
            this.selectedData.RemoveState(state.ID);
            SaveSelectedData();
            SetSelectedElementData(null);
            UpdateDetailPanel();
        }

        private FSMTransitionData CreateTransition(int fromStateID, int toStateID)
        {
            if (this.selectedData == null)
                return null;

            Undo.RecordObject(this.selectedData, "Create FSM Transition");
            FSMTransitionData transition = this.selectedData.AddTransition(fromStateID, toStateID);
            SaveSelectedData();
            UpdateDetailPanel();
            return transition;
        }

        private void RemoveTransition(FSMTransitionData transition)
        {
            if (this.selectedData == null || transition == null)
                return;

            Undo.RecordObject(this.selectedData, "Remove FSM Transition");
            this.selectedData.RemoveTransition(transition);
            SaveSelectedData();
            SetSelectedElementData(null);
            UpdateDetailPanel();
        }

        private void SetSelectedElementData(object elementData)
        {
            this.selectedElementData = elementData;
            this.isUpdatingFields = true;

            bool isState = elementData is FSMStateData;
            bool isTransition = elementData is FSMTransitionData;
            this.nameField?.SetEnabled(isState || isTransition);
            SetDisplay(this.initialStateToggle, isState);
            SetDisplay(this.conditionKeyField, isTransition);
            SetDisplay(this.priorityField, isTransition);

            if (isState)
            {
                var state = (FSMStateData)elementData;
                this.selectionTypeLabel.text = $"State {state.ID}";
                this.nameField.SetValueWithoutNotify(state.Name);
                this.initialStateToggle.SetValueWithoutNotify(
                    this.selectedData != null && this.selectedData.InitialStateID == state.ID);
            }
            else if (isTransition)
            {
                var transition = (FSMTransitionData)elementData;
                this.selectionTypeLabel.text =
                    $"Transition {transition.FromStateID} > {transition.ToStateID}";
                this.nameField.SetValueWithoutNotify(transition.Name);
                this.conditionKeyField.SetValueWithoutNotify(transition.ConditionKey);
                this.priorityField.SetValueWithoutNotify(transition.Priority);
            }
            else if (this.selectionTypeLabel != null)
            {
                this.selectionTypeLabel.text = "No Selection";
                this.nameField.SetValueWithoutNotify(string.Empty);
            }

            this.isUpdatingFields = false;
        }

        private void OnNameChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null)
                return;

            Undo.RecordObject(this.selectedData, "Rename FSM Element");
            if (this.selectedElementData is FSMStateData state)
                state.SetName(changeEvent.newValue);
            else if (this.selectedElementData is FSMTransitionData transition)
                transition.SetName(changeEvent.newValue);
            else
                return;

            SaveSelectedData();
            this.nameField.SetValueWithoutNotify(
                this.selectedElementData is FSMStateData currentState
                    ? currentState.Name
                    : ((FSMTransitionData)this.selectedElementData).Name);
            this.graphView.RefreshElementName(this.selectedElementData);
        }

        private void OnInitialStateChanged(ChangeEvent<bool> changeEvent)
        {
            if (this.isUpdatingFields || !changeEvent.newValue || this.selectedData == null ||
                !(this.selectedElementData is FSMStateData state))
                return;

            Undo.RecordObject(this.selectedData, "Set Initial FSM State");
            this.selectedData.SetInitialStateID(state.ID);
            SaveSelectedData();
            this.graphView.RefreshInitialState();
        }

        private void OnConditionKeyChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Condition Key");
            transition.SetConditionKey(changeEvent.newValue);
            SaveSelectedData();
        }

        private void OnPriorityChanged(ChangeEvent<int> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Transition Priority");
            transition.SetPriority(changeEvent.newValue);
            SaveSelectedData();
        }

        private void SaveSelectedData()
        {
            if (this.selectedData != null)
                EditorUtility.SetDirty(this.selectedData);
        }

        private void OnEditorUpdate()
        {
            if (this.viewMode != ViewMode.LiveDebug)
                return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime >= this.nextMachineRefreshTime)
            {
                this.nextMachineRefreshTime = currentTime + MachineRefreshInterval;
                RefreshStateMachineList(false);
                UpdateDetailPanel();
            }

            if (this.transitionHighlightEndTime > 0.0d && currentTime >= this.transitionHighlightEndTime)
            {
                this.transitionHighlightEndTime = 0.0d;
                this.graphView?.ClearTransitionHighlight();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingPlayMode ||
                stateChange == PlayModeStateChange.EnteredEditMode)
                SetSelectedStateMachine(null);

            this.nextMachineRefreshTime = 0.0d;
            UpdateDetailPanel();
        }

        /// <summary>
        /// FSMData의 Undo 또는 Redo 결과를 그래프와 상세 패널에 다시 반영
        /// </summary>
        private void OnUndoRedoPerformed()
        {
            if (this.viewMode != ViewMode.AssetEdit)
                return;

            this.graphView?.SetFSMData(this.selectedData);
            SetSelectedElementData(null);
            UpdateDetailPanel();
        }

        private void RefreshCurrentView()
        {
            if (this.viewMode == ViewMode.AssetEdit)
                this.graphView?.SetFSMData(this.selectedData);
            else
                RefreshStateMachineList(true);
        }

        private void RefreshStateMachineList(bool forceRefresh)
        {
            FSMDebugRegistry.GetStateMachines(this.scanResults);
            if (!forceRefresh && IsSameStateMachineList(this.registeredStateMachines, this.scanResults))
                return;

            this.registeredStateMachines.Clear();
            this.registeredStateMachines.AddRange(this.scanResults);
            this.machineNames.Clear();

            for (int i = 0; i < this.registeredStateMachines.Count; i++)
                this.machineNames.Add(CreateStateMachineName(this.registeredStateMachines[i], i));

            if (this.machineDropdown != null)
                this.machineDropdown.choices = new List<string>(this.machineNames);

            int selectedIndex = FindStateMachineIndex(this.selectedStateMachine);
            if (selectedIndex < 0)
            {
                IStateMachine nextStateMachine = this.registeredStateMachines.Count > 0
                    ? this.registeredStateMachines[0]
                    : null;
                SetSelectedStateMachine(nextStateMachine);
                selectedIndex = FindStateMachineIndex(this.selectedStateMachine);
            }

            if (this.machineDropdown != null)
            {
                string selectedName = selectedIndex >= 0 ? this.machineNames[selectedIndex] : "No running FSM";
                this.machineDropdown.SetValueWithoutNotify(selectedName);
            }
        }

        private void OnMachineDropdownChanged(ChangeEvent<string> changeEvent)
        {
            int selectedIndex = this.machineNames.IndexOf(changeEvent.newValue);
            if (selectedIndex < 0 || selectedIndex >= this.registeredStateMachines.Count)
                return;

            SetSelectedStateMachine(this.registeredStateMachines[selectedIndex]);
        }

        private void SetSelectedStateMachine(IStateMachine stateMachine)
        {
            if (ReferenceEquals(this.selectedStateMachine, stateMachine))
                return;

            if (this.selectedStateMachine != null)
            {
                this.selectedStateMachine.StateChanged -= OnStateChanged;
                this.selectedStateMachine.TransitionEvaluated -= OnTransitionEvaluated;
            }

            this.selectedStateMachine = stateMachine;
            this.transitionHistory.Clear();
            this.historyList?.Rebuild();

            if (this.selectedStateMachine != null)
            {
                this.selectedStateMachine.StateChanged += OnStateChanged;
                this.selectedStateMachine.TransitionEvaluated += OnTransitionEvaluated;
            }

            if (this.viewMode == ViewMode.LiveDebug)
                this.graphView?.SetStateMachine(this.selectedStateMachine);
            UpdateDetailPanel();
        }

        private void OnStateChanged(StateChangedEvent stateChangedEvent)
        {
            this.graphView?.SetActiveState(stateChangedEvent.CurrentStateID);
            UpdateDetailPanel();
        }

        private void OnTransitionEvaluated(TransitionEvaluatedEvent transitionEvent)
        {
            this.graphView?.HighlightTransition(transitionEvent.Transition, transitionEvent.Result);
            this.transitionHighlightEndTime =
                EditorApplication.timeSinceStartup + TransitionHighlightDuration;

            string fromState = transitionEvent.FromStateID.HasValue
                ? GetStateName(transitionEvent.FromStateID.Value)
                : "None";
            string targetState = GetStateName(transitionEvent.RequestedStateID);
            string transitionName = transitionEvent.Transition != null
                ? transitionEvent.Transition.Name
                : "No Transition";
            string history = $"{DateTime.Now:HH:mm:ss.fff}  {transitionEvent.Result}\n" +
                $"{fromState} > {targetState}  [{transitionName}]";

            this.transitionHistory.Insert(0, history);
            if (this.transitionHistory.Count > MaxHistoryCount)
                this.transitionHistory.RemoveAt(this.transitionHistory.Count - 1);
            this.historyList?.Rebuild();
        }

        private void UpdateDetailPanel()
        {
            if (this.playModeStatusLabel != null)
                this.playModeStatusLabel.text = EditorApplication.isPlaying ? "PLAY MODE" : "EDIT MODE";
            if (this.ownerValueLabel == null)
                return;

            if (this.viewMode == ViewMode.AssetEdit)
            {
                this.ownerValueLabel.text = this.selectedData != null ? this.selectedData.name : "-";
                this.runningValueLabel.text = "No";
                this.currentStateValueLabel.text = GetInitialStateName();
                this.stateCountValueLabel.text = this.selectedData?.States.Count.ToString() ?? "0";
                this.transitionCountValueLabel.text = this.selectedData?.Transitions.Count.ToString() ?? "0";
                return;
            }

            if (this.selectedStateMachine == null)
            {
                this.ownerValueLabel.text = "-";
                this.runningValueLabel.text = "-";
                this.currentStateValueLabel.text = "-";
                this.stateCountValueLabel.text = "0";
                this.transitionCountValueLabel.text = "0";
                this.graphView?.SetActiveState(null);
                return;
            }

            this.ownerValueLabel.text = CreateOwnerName(this.selectedStateMachine.GetOwner());
            this.runningValueLabel.text = this.selectedStateMachine.GetIsRunning() ? "Yes" : "No";
            int? currentStateID = this.selectedStateMachine.GetCurrentStateID();
            this.currentStateValueLabel.text = currentStateID.HasValue
                ? $"{GetStateName(currentStateID.Value)} ({currentStateID.Value})"
                : "None";
            this.stateCountValueLabel.text = this.selectedStateMachine.GetStates().Count.ToString();
            this.transitionCountValueLabel.text = this.selectedStateMachine.GetTransitions().Count.ToString();
            this.graphView?.SetActiveState(currentStateID);
        }

        private string GetInitialStateName()
        {
            if (this.selectedData == null || this.selectedData.States.Count == 0)
                return "None";

            FSMStateData state = this.selectedData.FindState(this.selectedData.InitialStateID);
            return state != null ? $"{state.Name} ({state.ID})" : "Invalid";
        }

        private static bool IsSameStateMachineList(
            List<IStateMachine> left,
            List<IStateMachine> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private int FindStateMachineIndex(IStateMachine stateMachine)
        {
            for (int i = 0; i < this.registeredStateMachines.Count; i++)
            {
                if (ReferenceEquals(this.registeredStateMachines[i], stateMachine))
                    return i;
            }

            return -1;
        }

        private static string CreateStateMachineName(IStateMachine stateMachine, int index)
        {
            return $"{index + 1}. {CreateOwnerName(stateMachine.GetOwner())}";
        }

        private static string CreateOwnerName(object owner)
        {
            if (owner == null)
                return "None";
            if (owner is UnityEngine.Object unityObject)
                return unityObject != null
                    ? $"{unityObject.name} ({owner.GetType().Name})"
                    : "Destroyed Object";

            return owner.GetType().Name;
        }

        private string GetStateName(int stateID)
        {
            if (this.selectedStateMachine != null &&
                this.selectedStateMachine.GetStates().TryGetValue(stateID, out State state))
                return state.Name;

            return stateID.ToString();
        }
    }
}
