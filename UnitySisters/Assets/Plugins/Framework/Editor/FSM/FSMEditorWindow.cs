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
        private readonly List<Type> stateIDTypes = new List<Type>();
        private readonly List<string> stateIDTypeNames = new List<string>();
        private readonly List<Type> conditionTypes = new List<Type>();
        private readonly List<string> conditionTypeNames = new List<string>();
        private readonly List<string> conditionNames = new List<string>();
        private readonly List<int> conditionIDs = new List<int>();
        private readonly List<FSMTransitionData> selectedTransitionGroup =
            new List<FSMTransitionData>();

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
        private DropdownField stateIDTypeField;
        private DropdownField conditionTypeField;
        private TextField nameField;
        private Toggle initialStateToggle;
        private Toggle hasConditionToggle;
        private DropdownField conditionField;
        private IntegerField priorityField;
        private Label transitionListTitle;
        private ListView transitionList;
        private ListView historyList;
        private FSMGraphView graphView;
        private FSMData selectedData;
        private IStateMachine selectedStateMachine;
        private object selectedElementData;
        private double nextMachineRefreshTime;
        private double transitionHighlightEndTime;
        private bool isUpdatingFields;
        private bool isUndoRefreshScheduled;

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
            RefreshStateIDTypes();
            RefreshConditionTypes();

            var splitView = new TwoPaneSplitView(1, 300.0f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1.0f;
            rootVisualElement.Add(splitView);

            this.graphView = new FSMGraphView();
            this.graphView.CreateStateMenuRequested = BuildCreateStateMenu;
            this.graphView.StateMoveStarted = BeginMoveState;
            this.graphView.StateMoved = MoveState;
            this.graphView.StateRemoved = RemoveState;
            this.graphView.InitialStateRequested = SetInitialState;
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

            this.stateIDTypeField = new DropdownField("State ID Type");
            this.stateIDTypeField.choices = new List<string>(this.stateIDTypeNames);
            this.stateIDTypeField.RegisterValueChangedCallback(OnStateIDTypeChanged);
            panel.Add(this.stateIDTypeField);

            this.conditionTypeField = new DropdownField("Condition Type");
            this.conditionTypeField.choices = new List<string>(this.conditionTypeNames);
            this.conditionTypeField.RegisterValueChangedCallback(OnConditionTypeChanged);
            panel.Add(this.conditionTypeField);

            this.selectionTypeLabel = new Label("No Selection");
            this.selectionTypeLabel.AddToClassList("fsm-section-title");
            panel.Add(this.selectionTypeLabel);

            this.nameField = new TextField("Name");
            this.nameField.RegisterValueChangedCallback(OnNameChanged);
            panel.Add(this.nameField);

            this.initialStateToggle = new Toggle("Initial State");
            this.initialStateToggle.RegisterValueChangedCallback(OnInitialStateChanged);
            panel.Add(this.initialStateToggle);

            this.hasConditionToggle = new Toggle("Has Condition");
            this.hasConditionToggle.RegisterValueChangedCallback(OnHasConditionChanged);
            panel.Add(this.hasConditionToggle);

            this.conditionField = new DropdownField("Condition");
            this.conditionField.RegisterValueChangedCallback(OnConditionChanged);
            panel.Add(this.conditionField);

            this.priorityField = new IntegerField("Priority");
            this.priorityField.RegisterValueChangedCallback(OnPriorityChanged);
            panel.Add(this.priorityField);

            this.transitionListTitle = new Label("Transitions");
            this.transitionListTitle.AddToClassList("fsm-section-title");
            panel.Add(this.transitionListTitle);

            this.transitionList = new ListView
            {
                itemsSource = this.selectedTransitionGroup,
                fixedItemHeight = 24.0f,
                selectionType = SelectionType.Single,
                makeItem = CreateTransitionItem,
                bindItem = BindTransitionItem
            };
            this.transitionList.selectionChanged += OnTransitionListSelectionChanged;
            this.transitionList.AddToClassList("fsm-transition-list");
            panel.Add(this.transitionList);
            return panel;
        }

        private static VisualElement CreateTransitionItem()
        {
            var label = new Label();
            label.AddToClassList("fsm-transition-list-item");
            return label;
        }

        private void BindTransitionItem(VisualElement element, int index)
        {
            if (!(element is Label label) || index < 0 ||
                index >= this.selectedTransitionGroup.Count)
                return;

            FSMTransitionData transition = this.selectedTransitionGroup[index];
            label.text = string.IsNullOrEmpty(transition.Name)
                ? $"Transition {index + 1}"
                : transition.Name;
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
                UpdateStateIDTypeField();
                UpdateConditionTypeField();
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
            UpdateStateIDTypeField();
            UpdateConditionTypeField();
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

        /// <summary>
        /// State ID 타입 연결 여부에 따라 자동 ID 또는 enum 항목 생성 메뉴 구성
        /// </summary>
        private void BuildCreateStateMenu(DropdownMenu menu, Vector2 position)
        {
            if (this.selectedData == null)
                return;

            Type stateIDType = FindSelectedStateIDType();
            if (stateIDType == null)
            {
                if (!string.IsNullOrWhiteSpace(this.selectedData.StateIDTypeID))
                {
                    menu.AppendAction(
                        "Create State/Missing State ID Type",
                        null,
                        DropdownMenuAction.Status.Disabled);
                    return;
                }

                menu.AppendAction("Create State", _ => CreateAutomaticState(position));
                return;
            }

            bool hasAvailableID = false;
            Array values = Enum.GetValues(stateIDType);
            for (int i = 0; i < values.Length; i++)
            {
                int stateID = (int)values.GetValue(i);
                if (this.selectedData.FindState(stateID) != null)
                    continue;

                string stateName = Enum.GetName(stateIDType, stateID);
                if (string.IsNullOrWhiteSpace(stateName))
                    continue;

                hasAvailableID = true;
                menu.AppendAction(
                    $"Create State/{stateName} ({stateID})",
                    _ => CreateBoundState(position, stateID, stateName));
            }

            if (!hasAvailableID)
            {
                menu.AppendAction(
                    "Create State/No Available IDs",
                    null,
                    DropdownMenuAction.Status.Disabled);
            }
        }

        private void CreateAutomaticState(Vector2 position)
        {
            if (this.selectedData == null)
                return;

            Undo.RecordObject(this.selectedData, "Create FSM State");
            this.selectedData.AddState("New State", position);
            SaveSelectedData();
            this.graphView.SetFSMData(this.selectedData);
            UpdateDetailPanel();
        }

        private void CreateBoundState(Vector2 position, int stateID, string stateName)
        {
            if (this.selectedData == null)
                return;

            Undo.RecordObject(this.selectedData, "Create FSM State");
            this.selectedData.AddState(stateID, stateName, position);
            SaveSelectedData();
            this.graphView.SetFSMData(this.selectedData);
            UpdateDetailPanel();
        }

        private void BeginMoveState()
        {
            if (this.selectedData != null)
                Undo.RecordObject(this.selectedData, "Move FSM State");
        }

        private void MoveState(FSMStateData state, Vector2 position)
        {
            if (this.selectedData == null || state == null || state.Position == position)
                return;

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
            transition.SetName(GetDefaultTransitionName(fromStateID, toStateID));
            SaveSelectedData();
            UpdateDetailPanel();
            return transition;
        }

        /// <summary>
        /// State ID enum이 연결된 경우 숫자 대신 enum 항목으로 기본 전이 이름 생성
        /// </summary>
        private string GetDefaultTransitionName(int fromStateID, int toStateID)
        {
            Type stateIDType = FindSelectedStateIDType();
            if (stateIDType != null)
            {
                string fromStateName = Enum.GetName(stateIDType, fromStateID);
                string toStateName = Enum.GetName(stateIDType, toStateID);
                if (!string.IsNullOrWhiteSpace(fromStateName) &&
                    !string.IsNullOrWhiteSpace(toStateName))
                {
                    return $"{fromStateName} To {toStateName}";
                }
            }

            return $"{fromStateID} To {toStateID}";
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
            try
            {
                bool isState = elementData is FSMStateData;
                bool isTransition = elementData is FSMTransitionData;
                UpdateSelectionFieldVisibility(isState, isTransition);

                if (elementData is FSMStateData state)
                    ShowSelectedState(state);
                else if (elementData is FSMTransitionData transition)
                    ShowSelectedTransition(transition);
                else
                    ShowNoSelection();
            }
            finally
            {
                this.isUpdatingFields = false;
            }
        }

        private void UpdateSelectionFieldVisibility(bool isState, bool isTransition)
        {
            this.nameField?.SetEnabled(isState || isTransition);
            SetDisplay(this.initialStateToggle, isState);
            SetDisplay(this.hasConditionToggle, isTransition);
            SetDisplay(this.conditionField, isTransition);
            SetDisplay(this.priorityField, isTransition);
        }

        private void ShowSelectedState(FSMStateData state)
        {
            this.selectionTypeLabel.text = $"State {state.ID}";
            this.nameField.SetValueWithoutNotify(state.Name);
            this.initialStateToggle.SetValueWithoutNotify(
                this.selectedData != null && this.selectedData.InitialStateID == state.ID);
            UpdateSelectedTransitionGroup(null);
        }

        private void ShowSelectedTransition(FSMTransitionData transition)
        {
            this.selectionTypeLabel.text =
                $"Transition {transition.FromStateID} > {transition.ToStateID}";
            this.nameField.SetValueWithoutNotify(transition.Name);
            UpdateTransitionConditionFields(transition);
            this.priorityField.SetValueWithoutNotify(transition.Priority);
            UpdateSelectedTransitionGroup(transition);
        }

        private void ShowNoSelection()
        {
            if (this.selectionTypeLabel != null)
                this.selectionTypeLabel.text = "No Selection";
            this.nameField?.SetValueWithoutNotify(string.Empty);
            UpdateSelectedTransitionGroup(null);
        }

        /// <summary>
        /// 같은 방향으로 연결된 전이들을 목록에 모아 개별 조건을 선택할 수 있게 표시
        /// </summary>
        private void UpdateSelectedTransitionGroup(FSMTransitionData selectedTransition)
        {
            this.selectedTransitionGroup.Clear();
            int selectedIndex = -1;
            if (selectedTransition != null && this.selectedData != null)
            {
                IReadOnlyList<FSMTransitionData> transitions = this.selectedData.Transitions;
                for (int i = 0; i < transitions.Count; i++)
                {
                    FSMTransitionData transition = transitions[i];
                    if (transition.FromStateID == selectedTransition.FromStateID &&
                        transition.ToStateID == selectedTransition.ToStateID)
                    {
                        if (ReferenceEquals(transition, selectedTransition))
                            selectedIndex = this.selectedTransitionGroup.Count;
                        this.selectedTransitionGroup.Add(transition);
                    }
                }
            }

            bool showList = this.selectedTransitionGroup.Count > 1;
            SetDisplay(this.transitionListTitle, showList);
            SetDisplay(this.transitionList, showList);
            if (this.transitionList == null)
                return;

            this.transitionList.style.height = Mathf.Clamp(
                this.selectedTransitionGroup.Count * 24.0f,
                48.0f,
                120.0f);
            this.transitionList.Rebuild();
            this.transitionList.selectedIndex = showList ? selectedIndex : -1;
        }

        private void OnTransitionListSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (this.isUpdatingFields)
                return;

            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is FSMTransitionData transition)
                {
                    this.graphView?.SelectTransition(transition);
                    return;
                }
            }
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

            SetInitialState(state);
        }

        private void SetInitialState(FSMStateData state)
        {
            if (this.selectedData == null || state == null ||
                this.selectedData.InitialStateID == state.ID)
                return;

            Undo.RecordObject(this.selectedData, "Set Initial FSM State");
            this.selectedData.SetInitialStateID(state.ID);
            SaveSelectedData();
            this.graphView.RefreshInitialState();
            UpdateDetailPanel();
        }

        /// <summary>
        /// 상태 ID의 숫자는 유지하면서 에디터에서 사용할 enum 타입 연결 변경
        /// </summary>
        private void OnStateIDTypeChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null)
                return;

            int selectedIndex = this.stateIDTypeNames.IndexOf(changeEvent.newValue);
            Type nextStateIDType = selectedIndex > 0
                ? this.stateIDTypes[selectedIndex - 1]
                : null;
            string nextTypeID = FSMStateIDType.GetID(nextStateIDType);
            if (this.selectedData.StateIDTypeID == nextTypeID)
                return;

            int undefinedStateID;
            if (nextStateIDType != null &&
                TryFindUndefinedStateID(this.selectedData, nextStateIDType, out undefinedStateID))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Bind State ID Type",
                    $"State ID {undefinedStateID} is not defined in {nextStateIDType.FullName}.",
                    "OK");
                UpdateStateIDTypeField();
                return;
            }

            Undo.RecordObject(this.selectedData, "Change FSM State ID Type");
            this.selectedData.SetStateIDType(nextStateIDType);
            SaveSelectedData();
            UpdateStateIDTypeField();
        }

        /// <summary>
        /// FSMData 전체에서 사용할 조건 enum 타입 변경
        /// </summary>
        private void OnConditionTypeChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null)
                return;

            int selectedIndex = this.conditionTypeNames.IndexOf(changeEvent.newValue);
            Type nextConditionType = selectedIndex > 0
                ? this.conditionTypes[selectedIndex - 1]
                : null;
            string nextTypeID = FSMConditionType.GetID(nextConditionType);
            if (this.selectedData.ConditionTypeID == nextTypeID)
                return;

            if (HasConfiguredCondition(this.selectedData) &&
                !EditorUtility.DisplayDialog(
                    "Change Condition Type",
                    "Changing the condition type clears every transition condition.",
                    "Change",
                    "Cancel"))
            {
                UpdateConditionTypeField();
                return;
            }

            Undo.RecordObject(this.selectedData, "Change FSM Condition Type");
            this.selectedData.SetConditionType(nextConditionType);
            SaveSelectedData();
            UpdateConditionTypeField();
            SetSelectedElementData(this.selectedElementData);
        }

        /// <summary>
        /// 선택한 전이에서 조건 사용 여부 변경
        /// </summary>
        private void OnHasConditionChanged(ChangeEvent<bool> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Type conditionType = FindSelectedConditionType();
            if (changeEvent.newValue && conditionType == null)
            {
                UpdateTransitionConditionFields(transition);
                return;
            }

            Undo.RecordObject(this.selectedData, "Change FSM Transition Condition");
            if (!changeEvent.newValue)
            {
                transition.ClearCondition();
            }
            else
            {
                BuildConditionChoices(conditionType, null);
                if (this.conditionIDs.Count > 0)
                    transition.SetCondition(this.conditionIDs[0]);
            }

            SaveSelectedData();
            UpdateTransitionConditionFields(transition);
        }

        /// <summary>
        /// 드롭다운에서 선택한 enum 값을 전이 조건으로 저장
        /// </summary>
        private void OnConditionChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            int selectedIndex = this.conditionNames.IndexOf(changeEvent.newValue);
            if (selectedIndex < 0 || selectedIndex >= this.conditionIDs.Count)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Transition Condition");
            transition.SetCondition(this.conditionIDs[selectedIndex]);
            SaveSelectedData();
            UpdateTransitionConditionFields(transition);
        }

        /// <summary>
        /// Attribute로 등록된 State ID enum을 Unity 타입 캐시에서 검색
        /// </summary>
        private void RefreshStateIDTypes()
        {
            this.stateIDTypes.Clear();
            foreach (Type stateIDType in TypeCache.GetTypesWithAttribute<FSMStateIDAttribute>())
            {
                if (FSMStateIDType.IsValid(stateIDType))
                    this.stateIDTypes.Add(stateIDType);
            }

            this.stateIDTypes.Sort((left, right) =>
                string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            this.stateIDTypeNames.Clear();
            this.stateIDTypeNames.Add("None");

            for (int i = 0; i < this.stateIDTypes.Count; i++)
            {
                Type stateIDType = this.stateIDTypes[i];
                this.stateIDTypeNames.Add(
                    $"{stateIDType.FullName} [{stateIDType.Assembly.GetName().Name}]");
            }
        }

        /// <summary>
        /// FSMData에 저장된 타입 ID를 State ID Type 드롭다운에 반영
        /// </summary>
        private void UpdateStateIDTypeField()
        {
            if (this.stateIDTypeField == null)
                return;

            bool previousUpdating = this.isUpdatingFields;
            this.isUpdatingFields = true;
            this.stateIDTypeField.SetEnabled(this.selectedData != null);
            var choices = new List<string>(this.stateIDTypeNames);
            string selectedName = "None";

            if (this.selectedData != null &&
                !string.IsNullOrWhiteSpace(this.selectedData.StateIDTypeID))
            {
                Type stateIDType = FindSelectedStateIDType();
                if (stateIDType != null)
                {
                    int typeIndex = this.stateIDTypes.IndexOf(stateIDType);
                    selectedName = this.stateIDTypeNames[typeIndex + 1];
                }
                else
                {
                    selectedName = $"Missing: {this.selectedData.StateIDTypeID}";
                    choices.Add(selectedName);
                }
            }

            this.stateIDTypeField.choices = choices;
            this.stateIDTypeField.SetValueWithoutNotify(selectedName);
            this.isUpdatingFields = previousUpdating;
        }

        private Type FindSelectedStateIDType()
        {
            if (this.selectedData == null)
                return null;

            for (int i = 0; i < this.stateIDTypes.Count; i++)
            {
                Type stateIDType = this.stateIDTypes[i];
                if (FSMStateIDType.GetID(stateIDType) == this.selectedData.StateIDTypeID)
                    return stateIDType;
            }

            return null;
        }

        private static bool TryFindUndefinedStateID(
            FSMData fsmData,
            Type stateIDType,
            out int undefinedStateID)
        {
            IReadOnlyList<FSMStateData> states = fsmData.States;
            for (int i = 0; i < states.Count; i++)
            {
                if (!Enum.IsDefined(stateIDType, states[i].ID))
                {
                    undefinedStateID = states[i].ID;
                    return true;
                }
            }

            undefinedStateID = 0;
            return false;
        }

        /// <summary>
        /// Attribute로 등록된 조건 enum을 Unity 타입 캐시에서 검색
        /// </summary>
        private void RefreshConditionTypes()
        {
            this.conditionTypes.Clear();
            foreach (Type conditionType in TypeCache.GetTypesWithAttribute<FSMConditionAttribute>())
            {
                if (FSMConditionType.IsValid(conditionType))
                    this.conditionTypes.Add(conditionType);
            }

            this.conditionTypes.Sort((left, right) =>
                string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            this.conditionTypeNames.Clear();
            this.conditionTypeNames.Add("None");

            for (int i = 0; i < this.conditionTypes.Count; i++)
            {
                Type conditionType = this.conditionTypes[i];
                this.conditionTypeNames.Add(
                    $"{conditionType.FullName} [{conditionType.Assembly.GetName().Name}]");
            }
        }

        /// <summary>
        /// FSMData에 저장된 타입 ID를 Condition Type 드롭다운에 반영
        /// </summary>
        private void UpdateConditionTypeField()
        {
            if (this.conditionTypeField == null)
                return;

            bool previousUpdating = this.isUpdatingFields;
            this.isUpdatingFields = true;
            this.conditionTypeField.SetEnabled(this.selectedData != null);
            var choices = new List<string>(this.conditionTypeNames);
            string selectedName = "None";

            if (this.selectedData != null &&
                !string.IsNullOrWhiteSpace(this.selectedData.ConditionTypeID))
            {
                Type conditionType = FindSelectedConditionType();
                if (conditionType != null)
                {
                    int typeIndex = this.conditionTypes.IndexOf(conditionType);
                    selectedName = this.conditionTypeNames[typeIndex + 1];
                }
                else
                {
                    selectedName = $"Missing: {this.selectedData.ConditionTypeID}";
                    choices.Add(selectedName);
                }
            }

            this.conditionTypeField.choices = choices;
            this.conditionTypeField.SetValueWithoutNotify(selectedName);
            this.isUpdatingFields = previousUpdating;
        }

        private Type FindSelectedConditionType()
        {
            if (this.selectedData == null)
                return null;

            for (int i = 0; i < this.conditionTypes.Count; i++)
            {
                Type conditionType = this.conditionTypes[i];
                if (FSMConditionType.GetID(conditionType) == this.selectedData.ConditionTypeID)
                    return conditionType;
            }

            return null;
        }

        /// <summary>
        /// 선택한 enum의 이름과 숫자 값을 전이 조건 드롭다운용 목록으로 변환
        /// </summary>
        private void BuildConditionChoices(Type conditionType, FSMTransitionData transition)
        {
            this.conditionNames.Clear();
            this.conditionIDs.Clear();
            if (conditionType == null)
                return;

            Array values = Enum.GetValues(conditionType);
            for (int i = 0; i < values.Length; i++)
            {
                object enumValue = values.GetValue(i);
                int conditionID = Convert.ToInt32(enumValue);

                string enumName = Enum.GetName(conditionType, enumValue);
                if (string.IsNullOrWhiteSpace(enumName))
                    continue;

                this.conditionNames.Add(enumName);
                this.conditionIDs.Add(conditionID);
            }

            if (transition != null && transition.HasCondition &&
                !this.conditionIDs.Contains(transition.ConditionID))
            {
                this.conditionNames.Add($"Missing ({transition.ConditionID})");
                this.conditionIDs.Add(transition.ConditionID);
            }
        }

        /// <summary>
        /// 선택한 전이의 조건 사용 여부와 enum 값을 상세 패널에 표시
        /// </summary>
        private void UpdateTransitionConditionFields(FSMTransitionData transition)
        {
            Type conditionType = FindSelectedConditionType();
            BuildConditionChoices(conditionType, transition);

            this.hasConditionToggle.SetEnabled(conditionType != null || transition.HasCondition);
            this.hasConditionToggle.SetValueWithoutNotify(transition.HasCondition);
            this.conditionField.choices = new List<string>(this.conditionNames);
            this.conditionField.SetEnabled(conditionType != null && transition.HasCondition);

            string selectedName = string.Empty;
            if (transition.HasCondition)
            {
                int conditionIndex = this.conditionIDs.IndexOf(transition.ConditionID);
                if (conditionIndex >= 0)
                    selectedName = this.conditionNames[conditionIndex];
            }
            else if (conditionType == null)
            {
                selectedName = "Select Condition Type";
            }
            else if (this.conditionNames.Count > 0)
            {
                selectedName = this.conditionNames[0];
            }

            this.conditionField.SetValueWithoutNotify(selectedName);
        }

        private static bool HasConfiguredCondition(FSMData fsmData)
        {
            IReadOnlyList<FSMTransitionData> transitions = fsmData.Transitions;
            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] != null && transitions[i].HasCondition)
                    return true;
            }

            return false;
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
            if (this.viewMode != ViewMode.AssetEdit || this.isUndoRefreshScheduled)
                return;

            // Undo 이벤트 처리 중 VisualTree를 교체하면 GraphView의 선택/포인터 캡처가 남을 수 있다.
            this.isUndoRefreshScheduled = true;
            rootVisualElement.schedule.Execute(RefreshAfterUndo).ExecuteLater(1);
        }

        private void RefreshAfterUndo()
        {
            this.isUndoRefreshScheduled = false;
            this.graphView?.SetFSMData(this.selectedData);
            UpdateStateIDTypeField();
            UpdateConditionTypeField();
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
