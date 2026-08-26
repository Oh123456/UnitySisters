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
        private readonly List<Type> parameterSourceTypes = new List<Type>();
        private readonly List<string> parameterSourceTypeNames = new List<string>();
        private readonly List<string> conditionNames = new List<string>();
        private readonly List<int> conditionIDs = new List<int>();
        private readonly List<FSMParameterData> parameterItems = new List<FSMParameterData>();
        private readonly List<FSMConditionData> selectedConditions = new List<FSMConditionData>();
        private readonly List<string> parameterNames = new List<string>();
        private readonly List<int> parameterIDs = new List<int>();
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
        private Label pendingTransitionValueLabel;
        private Label stateCountValueLabel;
        private Label transitionCountValueLabel;
        private VisualElement editPanel;
        private ScrollView editScroll;
        private Label selectionTypeLabel;
        private DropdownField stateIDTypeField;
        private DropdownField conditionTypeField;
        private DropdownField parameterSourceTypeField;
        private TextField nameField;
        private Toggle initialStateToggle;
        private ListView parameterList;
        private TextField parameterNameField;
        private EnumField parameterTypeField;
        private Toggle parameterBoolDefaultField;
        private IntegerField parameterIntDefaultField;
        private FloatField parameterFloatDefaultField;
        private Button removeParameterButton;
        private EnumField transitionModeField;
        private ListView conditionList;
        private DropdownField conditionParameterField;
        private EnumField conditionComparisonField;
        private Toggle conditionBoolValueField;
        private IntegerField conditionIntValueField;
        private FloatField conditionFloatValueField;
        private DropdownField customConditionField;
        private Toggle customExpectedField;
        private Button addParameterConditionButton;
        private Button addCustomConditionButton;
        private Button removeConditionButton;
        private Label automaticWarningLabel;
        private IntegerField priorityField;
        private FloatField transitionDelayField;
        private Toggle cancelWhenConditionFailsToggle;
        private Label transitionListTitle;
        private ListView transitionList;
        private ListView historyList;
        private FSMGraphView graphView;
        private FSMData selectedData;
        private IStateMachine selectedStateMachine;
        private object selectedElementData;
        private FSMParameterData selectedParameter;
        private FSMConditionData selectedCondition;
        private double nextMachineRefreshTime;
        private double transitionHighlightEndTime;
        private bool isUpdatingFields;
        private bool isUndoRefreshScheduled;
        private bool isAssetRefreshScheduled;

        [MenuItem("Tools/FSM/Editor", false, 0)]
        public static void OpenWindow()
        {
            FSMEditorWindow window = GetWindow<FSMEditorWindow>(
                "FSM Editor",
                true,
                typeof(SceneView));
            window.titleContent = new GUIContent("FSM Editor");
            window.minSize = new Vector2(860.0f, 500.0f);
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
            rootVisualElement.UnregisterCallback<KeyDownEvent>(
                OnRootKeyDown,
                TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnRootKeyDown,
                TrickleDown.TrickleDown);

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.Add(CreateToolbar());
            RefreshStateIDTypes();
            RefreshConditionTypes();
            RefreshParameterSourceTypes();

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
            this.pendingTransitionValueLabel = AddDetailRow(detailPanel, "Pending");
            this.stateCountValueLabel = AddDetailRow(detailPanel, "States");
            this.transitionCountValueLabel = AddDetailRow(detailPanel, "Transitions");

            this.editPanel = CreateEditPanel();
            this.editScroll = new ScrollView(ScrollViewMode.Vertical);
            this.editScroll.AddToClassList("fsm-edit-scroll");
            this.editScroll.Add(this.editPanel);
            detailPanel.Add(this.editScroll);

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

            this.conditionTypeField = new DropdownField("Custom Condition Type");
            this.conditionTypeField.choices = new List<string>(this.conditionTypeNames);
            this.conditionTypeField.RegisterValueChangedCallback(OnConditionTypeChanged);
            panel.Add(this.conditionTypeField);

            this.parameterSourceTypeField = new DropdownField("Parameter Source Type");
            this.parameterSourceTypeField.choices = new List<string>(this.parameterSourceTypeNames);
            this.parameterSourceTypeField.RegisterValueChangedCallback(OnParameterSourceTypeChanged);
            panel.Add(this.parameterSourceTypeField);

            var syncParameterButton = new Button(SyncBoundParameters)
            {
                text = "Sync Bound Parameters"
            };
            panel.Add(syncParameterButton);

            Label parameterTitle = new Label("Parameters");
            parameterTitle.AddToClassList("fsm-section-title");
            panel.Add(parameterTitle);

            var parameterButtons = new VisualElement();
            parameterButtons.AddToClassList("fsm-inline-controls");
            var addParameterButton = new Button(ShowAddParameterMenu) { text = "+ Parameter" };
            parameterButtons.Add(addParameterButton);
            this.removeParameterButton = new Button(RemoveSelectedParameter) { text = "Remove" };
            parameterButtons.Add(this.removeParameterButton);
            panel.Add(parameterButtons);

            this.parameterList = new ListView
            {
                itemsSource = this.parameterItems,
                fixedItemHeight = 24.0f,
                selectionType = SelectionType.Single,
                makeItem = CreateParameterItem,
                bindItem = BindParameterItem
            };
            this.parameterList.selectionChanged += OnParameterSelectionChanged;
            this.parameterList.AddToClassList("fsm-parameter-list");
            panel.Add(this.parameterList);

            this.parameterNameField = new TextField("Name");
            this.parameterNameField.RegisterValueChangedCallback(OnParameterNameChanged);
            panel.Add(this.parameterNameField);

            this.parameterTypeField = new EnumField("Type", FSMParameterType.Bool);
            this.parameterTypeField.RegisterValueChangedCallback(OnParameterTypeChanged);
            panel.Add(this.parameterTypeField);

            this.parameterBoolDefaultField = new Toggle("Default");
            this.parameterBoolDefaultField.RegisterValueChangedCallback(OnParameterBoolDefaultChanged);
            panel.Add(this.parameterBoolDefaultField);

            this.parameterIntDefaultField = new IntegerField("Default");
            this.parameterIntDefaultField.RegisterValueChangedCallback(OnParameterIntDefaultChanged);
            panel.Add(this.parameterIntDefaultField);

            this.parameterFloatDefaultField = new FloatField("Default");
            this.parameterFloatDefaultField.RegisterValueChangedCallback(OnParameterFloatDefaultChanged);
            panel.Add(this.parameterFloatDefaultField);

            this.selectionTypeLabel = new Label("No Selection");
            this.selectionTypeLabel.AddToClassList("fsm-section-title");
            panel.Add(this.selectionTypeLabel);

            this.nameField = new TextField("Name");
            this.nameField.RegisterValueChangedCallback(OnNameChanged);
            panel.Add(this.nameField);

            this.initialStateToggle = new Toggle("Initial State");
            this.initialStateToggle.RegisterValueChangedCallback(OnInitialStateChanged);
            panel.Add(this.initialStateToggle);

            this.transitionModeField = new EnumField("Mode", FSMTransitionMode.Manual);
            this.transitionModeField.tooltip =
                "Manual은 코드에서 전환을 요청할 때 검사하고, Automatic은 매 프레임 조건을 검사합니다.";
            this.transitionModeField.RegisterValueChangedCallback(OnTransitionModeChanged);
            panel.Add(this.transitionModeField);

            this.priorityField = new IntegerField("Priority");
            this.priorityField.tooltip =
                "여러 전이가 동시에 가능할 때 높은 값이 우선합니다. Pending 중에는 더 높은 값만 현재 전이를 중단할 수 있습니다.";
            this.priorityField.RegisterValueChangedCallback(OnPriorityChanged);
            panel.Add(this.priorityField);

            this.transitionDelayField = new FloatField("Delay (Seconds)");
            this.transitionDelayField.tooltip =
                "조건 통과 후 실제 상태 변경까지 기다리는 시간입니다. 0이면 기존처럼 즉시 전환합니다.";
            this.transitionDelayField.RegisterValueChangedCallback(OnTransitionDelayChanged);
            panel.Add(this.transitionDelayField);

            this.cancelWhenConditionFailsToggle = new Toggle("Cancel When Condition Fails");
            this.cancelWhenConditionFailsToggle.tooltip =
                "켜면 Pending 중 조건이 거짓이 될 때 전이를 취소합니다. 끄면 처음 통과한 전이는 Delay가 끝날 때까지 유지됩니다.";
            this.cancelWhenConditionFailsToggle.RegisterValueChangedCallback(
                OnCancelWhenConditionFailsChanged);
            panel.Add(this.cancelWhenConditionFailsToggle);

            this.automaticWarningLabel = new Label(
                "Automatic transition without conditions runs on the next Update.");
            this.automaticWarningLabel.AddToClassList("fsm-warning-label");
            panel.Add(this.automaticWarningLabel);

            Label conditionTitle = new Label("Conditions (AND)");
            conditionTitle.tooltip =
                "등록된 조건을 위에서부터 AND로 검사하며, 모든 조건이 참일 때 전이가 가능합니다.";
            conditionTitle.AddToClassList("fsm-section-title");
            panel.Add(conditionTitle);

            var conditionButtons = new VisualElement();
            conditionButtons.AddToClassList("fsm-inline-controls");
            this.addParameterConditionButton = new Button(AddParameterCondition) { text = "+ Parameter" };
            this.addParameterConditionButton.tooltip =
                "FSM Parameter 값을 비교하는 조건을 추가합니다.";
            conditionButtons.Add(this.addParameterConditionButton);
            this.addCustomConditionButton = new Button(AddCustomCondition) { text = "+ Custom" };
            this.addCustomConditionButton.tooltip =
                "게임 코드에서 제공하는 Custom Condition 함수 조건을 추가합니다.";
            conditionButtons.Add(this.addCustomConditionButton);
            this.removeConditionButton = new Button(RemoveSelectedCondition) { text = "Remove" };
            this.removeConditionButton.tooltip = "선택한 전이 조건을 제거합니다.";
            conditionButtons.Add(this.removeConditionButton);
            panel.Add(conditionButtons);

            this.conditionList = new ListView
            {
                itemsSource = this.selectedConditions,
                fixedItemHeight = 24.0f,
                selectionType = SelectionType.Single,
                makeItem = CreateConditionItem,
                bindItem = BindConditionItem
            };
            this.conditionList.selectionChanged += OnConditionSelectionChanged;
            this.conditionList.AddToClassList("fsm-condition-list");
            panel.Add(this.conditionList);

            this.conditionParameterField = new DropdownField("Parameter");
            this.conditionParameterField.tooltip =
                "이 조건에서 검사할 FSM Parameter를 선택합니다.";
            this.conditionParameterField.RegisterValueChangedCallback(OnConditionParameterChanged);
            panel.Add(this.conditionParameterField);

            this.conditionComparisonField = new EnumField("Comparison", FSMParameterComparison.Equal);
            this.conditionComparisonField.tooltip =
                "현재 Parameter 값과 아래 비교값을 어떤 방식으로 비교할지 선택합니다.";
            this.conditionComparisonField.RegisterValueChangedCallback(OnConditionComparisonChanged);
            panel.Add(this.conditionComparisonField);

            this.conditionBoolValueField = new Toggle("Value");
            this.conditionBoolValueField.tooltip = "전이가 통과할 때 필요한 bool 값을 설정합니다.";
            this.conditionBoolValueField.RegisterValueChangedCallback(OnConditionBoolValueChanged);
            panel.Add(this.conditionBoolValueField);

            this.conditionIntValueField = new IntegerField("Value");
            this.conditionIntValueField.tooltip = "전이가 통과할 때 비교할 int 값을 설정합니다.";
            this.conditionIntValueField.RegisterValueChangedCallback(OnConditionIntValueChanged);
            panel.Add(this.conditionIntValueField);

            this.conditionFloatValueField = new FloatField("Value");
            this.conditionFloatValueField.tooltip = "전이가 통과할 때 비교할 float 값을 설정합니다.";
            this.conditionFloatValueField.RegisterValueChangedCallback(OnConditionFloatValueChanged);
            panel.Add(this.conditionFloatValueField);

            this.customConditionField = new DropdownField("Custom Condition");
            this.customConditionField.tooltip =
                "게임 코드의 Condition Factory에서 평가할 조건 함수를 선택합니다.";
            this.customConditionField.RegisterValueChangedCallback(OnCustomConditionChanged);
            panel.Add(this.customConditionField);

            this.customExpectedField = new Toggle("Expected Result");
            this.customExpectedField.tooltip =
                "Custom Condition 반환값이 이 값과 같을 때 조건을 통과합니다.";
            this.customExpectedField.RegisterValueChangedCallback(OnCustomExpectedChanged);
            panel.Add(this.customExpectedField);

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
            string modeLabel = transition.GetMode() == FSMTransitionMode.Automatic ? "A" : "M";
            label.text = string.IsNullOrEmpty(transition.Name)
                ? $"Transition {index + 1}"
                : $"[{modeLabel}] {transition.Name}";
        }

        private static VisualElement CreateParameterItem()
        {
            var label = new Label();
            label.AddToClassList("fsm-list-item");
            return label;
        }

        private void BindParameterItem(VisualElement element, int index)
        {
            if (!(element is Label label) || index < 0 || index >= this.parameterItems.Count)
                return;

            FSMParameterData parameter = this.parameterItems[index];
            string bindingLabel = parameter.GetIsFieldBound() ? "  Bound" : string.Empty;
            label.text = $"{parameter.GetName()}  [{parameter.GetParameterType()}]{bindingLabel}";
        }

        private static VisualElement CreateConditionItem()
        {
            var label = new Label();
            label.AddToClassList("fsm-list-item");
            return label;
        }

        private void BindConditionItem(VisualElement element, int index)
        {
            if (!(element is Label label) || index < 0 || index >= this.selectedConditions.Count)
                return;

            label.text = CreateConditionLabel(this.selectedConditions[index]);
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
            SetDisplay(this.editScroll, mode == ViewMode.AssetEdit);
            SetDisplay(this.historyList, mode == ViewMode.LiveDebug);

            if (mode == ViewMode.AssetEdit)
            {
                SetSelectedStateMachine(null);
                this.dataField?.SetValueWithoutNotify(this.selectedData);
                RefreshAssetView();
                ScheduleAssetViewRefresh();
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
            {
                RefreshAssetView();
                // ObjectField 이벤트 처리가 끝난 뒤 GraphView 조작 상태를 한 번 더 확정한다.
                ScheduleAssetViewRefresh();
            }
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
            SetDisplay(this.transitionModeField, isTransition);
            SetDisplay(this.priorityField, isTransition);
            SetDisplay(this.transitionDelayField, isTransition);
            SetDisplay(this.cancelWhenConditionFailsToggle, isTransition);
            SetDisplay(this.automaticWarningLabel, false);
            SetDisplay(this.conditionList, isTransition);
            SetDisplay(this.addParameterConditionButton, isTransition);
            SetDisplay(this.addCustomConditionButton, isTransition);
            SetDisplay(this.removeConditionButton, isTransition);
            UpdateConditionEditorVisibility(isTransition ? this.selectedCondition : null);
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
            this.transitionModeField.SetValueWithoutNotify(transition.GetMode());
            this.priorityField.SetValueWithoutNotify(transition.Priority);
            this.transitionDelayField.SetValueWithoutNotify(transition.Delay);
            this.cancelWhenConditionFailsToggle.SetValueWithoutNotify(
                transition.CancelWhenConditionFails);
            RefreshConditionList(transition);
            UpdateAutomaticWarning(transition);
            UpdateSelectedTransitionGroup(transition);
        }

        private void ShowNoSelection()
        {
            if (this.selectionTypeLabel != null)
                this.selectionTypeLabel.text = "No Selection";
            this.nameField?.SetValueWithoutNotify(string.Empty);
            RefreshConditionList(null);
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

            if (HasConfiguredCustomCondition(this.selectedData) &&
                !EditorUtility.DisplayDialog(
                    "Change Condition Type",
                    "Changing the condition type clears every Custom Condition.",
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
        /// FSMData가 자동 동기화할 Attribute 필드의 소유 타입 변경
        /// </summary>
        private void OnParameterSourceTypeChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null)
                return;

            int selectedIndex = this.parameterSourceTypeNames.IndexOf(changeEvent.newValue);
            Type nextSourceType = selectedIndex > 0
                ? this.parameterSourceTypes[selectedIndex - 1]
                : null;
            string nextTypeID = FSMParameterBindingGenerator.GetTypeID(nextSourceType);
            if (this.selectedData.GetParameterSourceTypeID() == nextTypeID)
                return;

            if (this.selectedData.GetBoundParameterCount() > 0 &&
                !EditorUtility.DisplayDialog(
                    "Change Parameter Source",
                    "Changing the source removes field-bound Parameters that no longer exist " +
                    "and their transition conditions. Manual Parameters are preserved.",
                    "Change",
                    "Cancel"))
            {
                UpdateParameterSourceTypeField();
                return;
            }

            ApplyParameterSource(nextSourceType, "Change FSM Parameter Source");
        }

        private void SyncBoundParameters()
        {
            if (this.selectedData == null)
                return;

            Type sourceType = FindSelectedParameterSourceType();
            if (sourceType == null)
            {
                EditorUtility.DisplayDialog(
                    "FSM Parameter Binding",
                    "Select a valid Parameter Source Type first.",
                    "OK");
                return;
            }

            ApplyParameterSource(sourceType, "Sync FSM Bound Parameters");
        }

        private void ApplyParameterSource(Type sourceType, string undoName)
        {
            try
            {
                if (sourceType != null)
                    FSMParameterBindingGenerator.Generate(sourceType);
                Undo.RecordObject(this.selectedData, undoName);
                FSMParameterBindingGenerator.SyncData(this.selectedData, sourceType);
                SaveSelectedData();
                this.selectedParameter = null;
                RefreshParameterList();
                UpdateParameterSourceTypeField();
                if (this.selectedElementData is FSMTransitionData transition)
                    RefreshConditionList(transition);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "FSM Parameter Binding Failed",
                    exception.Message,
                    "OK");
                UpdateParameterSourceTypeField();
            }
        }

        private void ShowAddParameterMenu()
        {
            if (this.selectedData == null)
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Bool"), false, () => AddParameter(FSMParameterType.Bool));
            menu.AddItem(new GUIContent("Int"), false, () => AddParameter(FSMParameterType.Int));
            menu.AddItem(new GUIContent("Float"), false, () => AddParameter(FSMParameterType.Float));
            menu.AddItem(new GUIContent("Trigger"), false, () => AddParameter(FSMParameterType.Trigger));
            menu.ShowAsContext();
        }

        private void AddParameter(FSMParameterType type)
        {
            Undo.RecordObject(this.selectedData, "Add FSM Parameter");
            this.selectedParameter = this.selectedData.AddParameter(
                CreateUniqueParameterName(type.ToString()),
                type);
            SaveSelectedData();
            RefreshParameterList();
        }

        private void RemoveSelectedParameter()
        {
            if (this.selectedData == null || this.selectedParameter == null ||
                this.selectedParameter.GetIsFieldBound())
                return;

            Undo.RecordObject(this.selectedData, "Remove FSM Parameter");
            this.selectedData.RemoveParameter(this.selectedParameter);
            this.selectedParameter = null;
            SaveSelectedData();
            RefreshParameterList();
            if (this.selectedElementData is FSMTransitionData transition)
                RefreshConditionList(transition);
        }

        private void OnParameterSelectionChanged(IEnumerable<object> selection)
        {
            this.selectedParameter = null;
            foreach (object item in selection)
            {
                this.selectedParameter = item as FSMParameterData;
                break;
            }

            UpdateParameterFields();
        }

        private void OnParameterNameChanged(ChangeEvent<string> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null || this.selectedParameter == null ||
                this.selectedParameter.GetIsFieldBound())
                return;

            string nextName = changeEvent.newValue?.Trim();
            if (IsParameterNameUsed(nextName, this.selectedParameter))
            {
                UpdateParameterFields();
                return;
            }

            Undo.RecordObject(this.selectedData, "Rename FSM Parameter");
            this.selectedParameter.SetName(nextName);
            SaveSelectedData();
            RefreshParameterList();
            this.conditionList?.RefreshItems();
        }

        private void OnParameterTypeChanged(ChangeEvent<Enum> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null || this.selectedParameter == null ||
                this.selectedParameter.GetIsFieldBound())
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Parameter Type");
            this.selectedParameter.SetParameterType((FSMParameterType)changeEvent.newValue);
            SaveSelectedData();
            RefreshParameterList();
            UpdateParameterFields();
            UpdateConditionFields();
        }

        private void OnParameterBoolDefaultChanged(ChangeEvent<bool> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null || this.selectedParameter == null)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Parameter Default");
            this.selectedParameter.SetDefaultBoolValue(changeEvent.newValue);
            SaveSelectedData();
        }

        private void OnParameterIntDefaultChanged(ChangeEvent<int> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null || this.selectedParameter == null)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Parameter Default");
            this.selectedParameter.SetDefaultIntValue(changeEvent.newValue);
            SaveSelectedData();
        }

        private void OnParameterFloatDefaultChanged(ChangeEvent<float> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null || this.selectedParameter == null)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Parameter Default");
            this.selectedParameter.SetDefaultFloatValue(changeEvent.newValue);
            SaveSelectedData();
        }

        private void OnTransitionModeChanged(ChangeEvent<Enum> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Transition Mode");
            transition.SetMode((FSMTransitionMode)changeEvent.newValue);
            SaveSelectedData();
            this.transitionList?.RefreshItems();
            this.graphView?.RefreshElementName(transition);
            UpdateAutomaticWarning(transition);
        }

        private void AddParameterCondition()
        {
            if (this.selectedData == null || this.parameterItems.Count == 0 ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Add FSM Parameter Condition");
            this.selectedCondition = transition.AddParameterCondition(this.parameterItems[0].GetID());
            if (this.parameterItems[0].GetParameterType() == FSMParameterType.Trigger)
                this.selectedCondition.SetBoolValue(true);
            SaveSelectedData();
            RefreshConditionList(transition);
        }

        private void AddCustomCondition()
        {
            if (this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            BuildConditionChoices(FindSelectedConditionType(), null);
            if (this.conditionIDs.Count == 0)
                return;

            Undo.RecordObject(this.selectedData, "Add FSM Custom Condition");
            this.selectedCondition = transition.AddCustomCondition(this.conditionIDs[0]);
            SaveSelectedData();
            RefreshConditionList(transition);
        }

        private void RemoveSelectedCondition()
        {
            if (this.selectedData == null || this.selectedCondition == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Remove FSM Condition");
            transition.RemoveCondition(this.selectedCondition);
            this.selectedCondition = null;
            SaveSelectedData();
            RefreshConditionList(transition);
        }

        private void OnConditionSelectionChanged(IEnumerable<object> selection)
        {
            this.selectedCondition = null;
            foreach (object item in selection)
            {
                this.selectedCondition = item as FSMConditionData;
                break;
            }

            UpdateConditionFields();
        }

        private void UpdateAutomaticWarning(FSMTransitionData transition)
        {
            SetDisplay(
                this.automaticWarningLabel,
                transition != null &&
                transition.GetMode() == FSMTransitionMode.Automatic &&
                transition.GetConditions().Count == 0);
        }

        private void OnConditionParameterChanged(ChangeEvent<string> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Parameter))
                return;

            int selectedIndex = this.parameterNames.IndexOf(changeEvent.newValue);
            if (selectedIndex < 0 || selectedIndex >= this.parameterIDs.Count)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Condition Parameter");
            this.selectedCondition.SetParameter(this.parameterIDs[selectedIndex]);
            SaveSelectedData();
            UpdateConditionFields();
            this.conditionList.RefreshItems();
        }

        private void OnConditionComparisonChanged(ChangeEvent<Enum> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Parameter))
                return;

            FSMParameterComparison comparison = (FSMParameterComparison)changeEvent.newValue;
            FSMParameterData parameter = FindConditionParameter(this.selectedCondition);
            if (parameter != null &&
                (parameter.GetParameterType() == FSMParameterType.Bool ||
                 parameter.GetParameterType() == FSMParameterType.Trigger) &&
                comparison != FSMParameterComparison.Equal &&
                comparison != FSMParameterComparison.NotEqual)
            {
                UpdateConditionFields();
                return;
            }

            Undo.RecordObject(this.selectedData, "Change FSM Condition Comparison");
            this.selectedCondition.SetComparison(comparison);
            SaveSelectedData();
            this.conditionList.RefreshItems();
        }

        private void OnConditionBoolValueChanged(ChangeEvent<bool> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Parameter))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Condition Value");
            this.selectedCondition.SetBoolValue(changeEvent.newValue);
            SaveSelectedData();
            this.conditionList.RefreshItems();
        }

        private void OnConditionIntValueChanged(ChangeEvent<int> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Parameter))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Condition Value");
            this.selectedCondition.SetIntValue(changeEvent.newValue);
            SaveSelectedData();
            this.conditionList.RefreshItems();
        }

        private void OnConditionFloatValueChanged(ChangeEvent<float> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Parameter))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Condition Value");
            this.selectedCondition.SetFloatValue(changeEvent.newValue);
            SaveSelectedData();
            this.conditionList.RefreshItems();
        }

        private void OnCustomConditionChanged(ChangeEvent<string> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Custom))
                return;

            int selectedIndex = this.conditionNames.IndexOf(changeEvent.newValue);
            if (selectedIndex < 0 || selectedIndex >= this.conditionIDs.Count)
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Custom Condition");
            this.selectedCondition.SetCustomCondition(this.conditionIDs[selectedIndex]);
            SaveSelectedData();
            this.conditionList.RefreshItems();
        }

        private void OnCustomExpectedChanged(ChangeEvent<bool> changeEvent)
        {
            if (!CanEditSelectedCondition(FSMConditionKind.Custom))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Custom Condition Result");
            this.selectedCondition.SetCustomExpectedResult(changeEvent.newValue);
            SaveSelectedData();
            this.conditionList.RefreshItems();
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
            BuildConditionChoices(FindSelectedConditionType(), null);
            this.addCustomConditionButton?.SetEnabled(this.conditionIDs.Count > 0);
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
        /// FSMParameter Attribute가 선언된 필드 소유 타입 목록 갱신
        /// </summary>
        private void RefreshParameterSourceTypes()
        {
            FSMParameterBindingGenerator.GetSourceTypes(this.parameterSourceTypes);
            this.parameterSourceTypeNames.Clear();
            this.parameterSourceTypeNames.Add("None");
            for (int i = 0; i < this.parameterSourceTypes.Count; i++)
            {
                Type sourceType = this.parameterSourceTypes[i];
                this.parameterSourceTypeNames.Add(
                    $"{sourceType.FullName} [{sourceType.Assembly.GetName().Name}]");
            }
        }

        private void UpdateParameterSourceTypeField()
        {
            if (this.parameterSourceTypeField == null)
                return;

            bool previousUpdating = this.isUpdatingFields;
            this.isUpdatingFields = true;
            this.parameterSourceTypeField.SetEnabled(this.selectedData != null);
            var choices = new List<string>(this.parameterSourceTypeNames);
            string selectedName = "None";

            if (this.selectedData != null &&
                !string.IsNullOrWhiteSpace(this.selectedData.GetParameterSourceTypeID()))
            {
                Type sourceType = FindSelectedParameterSourceType();
                if (sourceType != null)
                {
                    int typeIndex = this.parameterSourceTypes.IndexOf(sourceType);
                    selectedName = this.parameterSourceTypeNames[typeIndex + 1];
                }
                else
                {
                    selectedName = $"Missing: {this.selectedData.GetParameterSourceTypeID()}";
                    choices.Add(selectedName);
                }
            }

            this.parameterSourceTypeField.choices = choices;
            this.parameterSourceTypeField.SetValueWithoutNotify(selectedName);
            this.isUpdatingFields = previousUpdating;
        }

        private Type FindSelectedParameterSourceType()
        {
            if (this.selectedData == null)
                return null;

            return FSMParameterBindingGenerator.FindSourceType(
                this.selectedData.GetParameterSourceTypeID(),
                this.parameterSourceTypes);
        }

        /// <summary>
        /// 선택한 enum의 이름과 숫자 값을 전이 조건 드롭다운용 목록으로 변환
        /// </summary>
        private void BuildConditionChoices(Type conditionType, FSMConditionData selectedCustomCondition)
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

            if (selectedCustomCondition != null &&
                selectedCustomCondition.GetConditionKind() == FSMConditionKind.Custom &&
                !this.conditionIDs.Contains(selectedCustomCondition.GetCustomConditionID()))
            {
                int missingID = selectedCustomCondition.GetCustomConditionID();
                this.conditionNames.Add($"Missing ({missingID})");
                this.conditionIDs.Add(missingID);
            }
        }

        /// <summary>
        /// FSMData의 Parameter 목록과 선택된 기본값을 다시 표시
        /// </summary>
        private void RefreshParameterList()
        {
            this.parameterItems.Clear();
            this.parameterNames.Clear();
            this.parameterIDs.Clear();
            int selectedIndex = -1;
            if (this.selectedData != null)
            {
                IReadOnlyList<FSMParameterData> parameters = this.selectedData.GetParameters();
                for (int i = 0; i < parameters.Count; i++)
                {
                    FSMParameterData parameter = parameters[i];
                    if (parameter == null)
                        continue;

                    if (ReferenceEquals(parameter, this.selectedParameter))
                        selectedIndex = this.parameterItems.Count;
                    this.parameterItems.Add(parameter);
                    this.parameterNames.Add(parameter.GetName());
                    this.parameterIDs.Add(parameter.GetID());
                }
            }

            if (selectedIndex < 0)
            {
                this.selectedParameter = this.parameterItems.Count > 0 ? this.parameterItems[0] : null;
                selectedIndex = this.selectedParameter != null ? 0 : -1;
            }

            this.parameterList?.RefreshItems();
            if (this.parameterList != null)
            {
                if (selectedIndex >= 0)
                    this.parameterList.SetSelectionWithoutNotify(new[] { selectedIndex });
                else
                    this.parameterList.SetSelectionWithoutNotify(Array.Empty<int>());
            }

            UpdateParameterFields();
            this.addParameterConditionButton?.SetEnabled(this.parameterItems.Count > 0);
        }

        private void UpdateParameterFields()
        {
            bool hasParameter = this.selectedParameter != null;
            bool canEditDefinition = hasParameter && !this.selectedParameter.GetIsFieldBound();
            this.parameterNameField?.SetEnabled(canEditDefinition);
            this.parameterTypeField?.SetEnabled(canEditDefinition);
            this.removeParameterButton?.SetEnabled(canEditDefinition);

            if (!hasParameter)
            {
                this.parameterNameField?.SetValueWithoutNotify(string.Empty);
                SetDisplay(this.parameterBoolDefaultField, false);
                SetDisplay(this.parameterIntDefaultField, false);
                SetDisplay(this.parameterFloatDefaultField, false);
                return;
            }

            this.parameterNameField.SetValueWithoutNotify(this.selectedParameter.GetName());
            this.parameterTypeField.SetValueWithoutNotify(this.selectedParameter.GetParameterType());
            FSMParameterType type = this.selectedParameter.GetParameterType();
            SetDisplay(this.parameterBoolDefaultField, type == FSMParameterType.Bool);
            SetDisplay(this.parameterIntDefaultField, type == FSMParameterType.Int);
            SetDisplay(this.parameterFloatDefaultField, type == FSMParameterType.Float);
            this.parameterBoolDefaultField.SetValueWithoutNotify(
                this.selectedParameter.GetDefaultBoolValue());
            this.parameterIntDefaultField.SetValueWithoutNotify(
                this.selectedParameter.GetDefaultIntValue());
            this.parameterFloatDefaultField.SetValueWithoutNotify(
                this.selectedParameter.GetDefaultFloatValue());
            this.parameterBoolDefaultField.SetEnabled(canEditDefinition);
            this.parameterIntDefaultField.SetEnabled(canEditDefinition);
            this.parameterFloatDefaultField.SetEnabled(canEditDefinition);
        }

        private void RefreshConditionList(FSMTransitionData transition)
        {
            this.selectedConditions.Clear();
            int selectedIndex = -1;
            if (transition != null)
            {
                IReadOnlyList<FSMConditionData> conditions = transition.GetConditions();
                for (int i = 0; i < conditions.Count; i++)
                {
                    FSMConditionData condition = conditions[i];
                    if (condition == null)
                        continue;

                    if (ReferenceEquals(condition, this.selectedCondition))
                        selectedIndex = this.selectedConditions.Count;
                    this.selectedConditions.Add(condition);
                }
            }

            if (selectedIndex < 0)
            {
                this.selectedCondition = this.selectedConditions.Count > 0
                    ? this.selectedConditions[0]
                    : null;
                selectedIndex = this.selectedCondition != null ? 0 : -1;
            }

            this.conditionList?.RefreshItems();
            if (this.conditionList != null)
            {
                if (selectedIndex >= 0)
                    this.conditionList.SetSelectionWithoutNotify(new[] { selectedIndex });
                else
                    this.conditionList.SetSelectionWithoutNotify(Array.Empty<int>());
            }

            UpdateConditionFields();
            UpdateAutomaticWarning(transition);
        }

        /// <summary>
        /// 선택된 조건 종류와 Parameter 타입에 필요한 입력 필드만 노출한다.
        /// </summary>
        private void UpdateConditionFields()
        {
            UpdateConditionEditorVisibility(this.selectedCondition);
            this.removeConditionButton?.SetEnabled(this.selectedCondition != null);
            if (this.selectedCondition == null)
                return;

            if (this.selectedCondition.GetConditionKind() == FSMConditionKind.Custom)
            {
                BuildConditionChoices(FindSelectedConditionType(), this.selectedCondition);
                this.customConditionField.choices = new List<string>(this.conditionNames);
                int customIndex = this.conditionIDs.IndexOf(
                    this.selectedCondition.GetCustomConditionID());
                this.customConditionField.SetValueWithoutNotify(
                    customIndex >= 0 ? this.conditionNames[customIndex] : string.Empty);
                this.customExpectedField.SetValueWithoutNotify(
                    this.selectedCondition.GetCustomExpectedResult());
                return;
            }

            this.conditionParameterField.choices = new List<string>(this.parameterNames);
            int parameterIndex = this.parameterIDs.IndexOf(this.selectedCondition.GetParameterID());
            if (parameterIndex < 0)
            {
                this.conditionParameterField.choices.Add(
                    $"Missing ({this.selectedCondition.GetParameterID()})");
                this.conditionParameterField.SetValueWithoutNotify(
                    this.conditionParameterField.choices[
                        this.conditionParameterField.choices.Count - 1]);
            }
            else
            {
                this.conditionParameterField.SetValueWithoutNotify(this.parameterNames[parameterIndex]);
            }

            this.conditionComparisonField.SetValueWithoutNotify(
                this.selectedCondition.GetComparison());
            this.conditionBoolValueField.SetValueWithoutNotify(this.selectedCondition.GetBoolValue());
            this.conditionIntValueField.SetValueWithoutNotify(this.selectedCondition.GetIntValue());
            this.conditionFloatValueField.SetValueWithoutNotify(this.selectedCondition.GetFloatValue());
            UpdateConditionValueVisibility(FindConditionParameter(this.selectedCondition));
        }

        private void UpdateConditionEditorVisibility(FSMConditionData condition)
        {
            bool isParameter = condition != null &&
                condition.GetConditionKind() == FSMConditionKind.Parameter;
            bool isCustom = condition != null &&
                condition.GetConditionKind() == FSMConditionKind.Custom;
            SetDisplay(this.conditionParameterField, isParameter);
            SetDisplay(this.conditionComparisonField, isParameter);
            SetDisplay(this.customConditionField, isCustom);
            SetDisplay(this.customExpectedField, isCustom);
            if (!isParameter)
                UpdateConditionValueVisibility(null);
        }

        private void UpdateConditionValueVisibility(FSMParameterData parameter)
        {
            FSMParameterType? parameterType = parameter?.GetParameterType();
            SetDisplay(this.conditionBoolValueField,
                parameterType == FSMParameterType.Bool || parameterType == FSMParameterType.Trigger);
            SetDisplay(this.conditionIntValueField, parameterType == FSMParameterType.Int);
            SetDisplay(this.conditionFloatValueField, parameterType == FSMParameterType.Float);
        }

        private FSMParameterData FindConditionParameter(FSMConditionData condition)
        {
            return this.selectedData != null && condition != null
                ? this.selectedData.FindParameter(condition.GetParameterID())
                : null;
        }

        private bool CanEditSelectedCondition(FSMConditionKind expectedKind)
        {
            return !this.isUpdatingFields && this.selectedData != null &&
                this.selectedCondition != null &&
                this.selectedCondition.GetConditionKind() == expectedKind;
        }

        private bool IsParameterNameUsed(string name, FSMParameterData except)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            for (int i = 0; i < this.parameterItems.Count; i++)
            {
                FSMParameterData parameter = this.parameterItems[i];
                if (!ReferenceEquals(parameter, except) &&
                    string.Equals(parameter.GetName(), name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private string CreateUniqueParameterName(string baseName)
        {
            string candidate = baseName;
            int suffix = 1;
            while (IsParameterNameUsed(candidate, null))
                candidate = $"{baseName} {suffix++}";

            return candidate;
        }

        private string CreateConditionLabel(FSMConditionData condition)
        {
            if (condition.GetConditionKind() == FSMConditionKind.Custom)
            {
                BuildConditionChoices(FindSelectedConditionType(), condition);
                int customIndex = this.conditionIDs.IndexOf(condition.GetCustomConditionID());
                string customName = customIndex >= 0
                    ? this.conditionNames[customIndex]
                    : $"Missing ({condition.GetCustomConditionID()})";
                return $"Custom: {customName} == {condition.GetCustomExpectedResult()}";
            }

            FSMParameterData parameter = FindConditionParameter(condition);
            string parameterName = parameter != null
                ? parameter.GetName()
                : $"Missing ({condition.GetParameterID()})";
            return $"{parameterName} {GetComparisonSymbol(condition.GetComparison())} " +
                GetConditionValueText(condition, parameter);
        }

        private static string GetComparisonSymbol(FSMParameterComparison comparison)
        {
            switch (comparison)
            {
                case FSMParameterComparison.Equal: return "==";
                case FSMParameterComparison.NotEqual: return "!=";
                case FSMParameterComparison.Greater: return ">";
                case FSMParameterComparison.Less: return "<";
                case FSMParameterComparison.GreaterOrEqual: return ">=";
                case FSMParameterComparison.LessOrEqual: return "<=";
                default: return "?";
            }
        }

        private static string GetConditionValueText(
            FSMConditionData condition,
            FSMParameterData parameter)
        {
            if (parameter == null)
                return "?";

            switch (parameter.GetParameterType())
            {
                case FSMParameterType.Bool:
                case FSMParameterType.Trigger:
                    return condition.GetBoolValue().ToString();
                case FSMParameterType.Int:
                    return condition.GetIntValue().ToString();
                case FSMParameterType.Float:
                    return condition.GetFloatValue().ToString("0.###");
                default:
                    return "?";
            }
        }

        private static bool HasConfiguredCustomCondition(FSMData fsmData)
        {
            IReadOnlyList<FSMTransitionData> transitions = fsmData.Transitions;
            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i] == null)
                    continue;

                IReadOnlyList<FSMConditionData> conditions = transitions[i].GetConditions();
                for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
                {
                    if (conditions[conditionIndex] != null &&
                        conditions[conditionIndex].GetConditionKind() == FSMConditionKind.Custom)
                        return true;
                }
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

        private void OnTransitionDelayChanged(ChangeEvent<float> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            float delay = changeEvent.newValue;
            if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0.0f)
            {
                delay = 0.0f;
                this.transitionDelayField.SetValueWithoutNotify(delay);
            }

            Undo.RecordObject(this.selectedData, "Change FSM Transition Delay");
            transition.SetDelay(delay);
            SaveSelectedData();
        }

        private void OnCancelWhenConditionFailsChanged(ChangeEvent<bool> changeEvent)
        {
            if (this.isUpdatingFields || this.selectedData == null ||
                !(this.selectedElementData is FSMTransitionData transition))
                return;

            Undo.RecordObject(this.selectedData, "Change FSM Transition Cancellation");
            transition.SetCancelWhenConditionFails(changeEvent.newValue);
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
                StateTransition pendingTransition =
                    this.selectedStateMachine?.GetPendingTransition();
                if (pendingTransition != null)
                {
                    this.graphView?.HighlightTransition(
                        pendingTransition,
                        StateChangeResult.Pending);
                    this.transitionHighlightEndTime =
                        currentTime + TransitionHighlightDuration;
                }
                else
                {
                    this.transitionHighlightEndTime = 0.0d;
                    this.graphView?.ClearTransitionHighlight();
                }
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
            RefreshAssetView();
        }

        private void RefreshCurrentView()
        {
            if (this.viewMode == ViewMode.AssetEdit)
                RefreshAssetView();
            else
                RefreshStateMachineList(true);
        }

        /// <summary>
        /// FSM 에디터에 포커스가 있을 때 F5 입력을 현재 화면 새로고침으로 처리
        /// </summary>
        private void OnRootKeyDown(KeyDownEvent keyDownEvent)
        {
            if (keyDownEvent.keyCode != KeyCode.F5)
                return;

            RefreshCurrentView();
            keyDownEvent.StopImmediatePropagation();
        }

        private void ScheduleAssetViewRefresh()
        {
            if (this.isAssetRefreshScheduled)
                return;

            this.isAssetRefreshScheduled = true;
            rootVisualElement.schedule.Execute(RefreshScheduledAssetView).ExecuteLater(1);
        }

        private void RefreshScheduledAssetView()
        {
            this.isAssetRefreshScheduled = false;
            if (this.viewMode == ViewMode.AssetEdit)
                RefreshAssetView();
        }

        private void RefreshAssetView()
        {
            this.graphView?.SetFSMData(this.selectedData);
            UpdateStateIDTypeField();
            UpdateConditionTypeField();
            UpdateParameterSourceTypeField();
            RefreshParameterList();
            SetSelectedElementData(null);
            UpdateDetailPanel();
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
                FSMData sourceData = this.selectedStateMachine.GetSourceData();
                if (sourceData != null)
                    this.selectedData = sourceData;

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
                this.pendingTransitionValueLabel.text = "-";
                this.stateCountValueLabel.text = this.selectedData?.States.Count.ToString() ?? "0";
                this.transitionCountValueLabel.text = this.selectedData?.Transitions.Count.ToString() ?? "0";
                return;
            }

            if (this.selectedStateMachine == null)
            {
                this.ownerValueLabel.text = "-";
                this.runningValueLabel.text = "-";
                this.currentStateValueLabel.text = "-";
                this.pendingTransitionValueLabel.text = "-";
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
            StateTransition pendingTransition = this.selectedStateMachine.GetPendingTransition();
            this.pendingTransitionValueLabel.text = pendingTransition != null
                ? $"{pendingTransition.Name} ({this.selectedStateMachine.GetPendingTransitionRemainingTime():0.000}s)"
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
            if (this.selectedStateMachine != null)
            {
                FSMStateData sourceState = this.selectedStateMachine.GetSourceData()?.FindState(stateID);
                if (sourceState != null && !string.IsNullOrWhiteSpace(sourceState.Name))
                    return sourceState.Name;

                if (this.selectedStateMachine.GetStates().TryGetValue(stateID, out State state))
                    return state.Name;
            }

            return stateID.ToString();
        }
    }
}
