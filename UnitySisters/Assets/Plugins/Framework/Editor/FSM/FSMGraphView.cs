using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.FSM.Editor
{
    internal class FSMGraphView : GraphView
    {
        private static readonly Color DefaultEdgeColor = new Color(0.68f, 0.69f, 0.71f);
        private static readonly Color EntryEdgeColor = new Color(0.82f, 0.52f, 0.08f);
        private static readonly Color SuccessEdgeColor = new Color(0.2f, 0.85f, 0.55f);
        private static readonly Color PendingEdgeColor = new Color(0.95f, 0.68f, 0.18f);
        private static readonly Color FailedEdgeColor = new Color(1.0f, 0.32f, 0.28f);
        private const float ReverseTransitionOffset = 5.0f;

        private readonly Dictionary<int, FSMStateNode> stateNodes =
            new Dictionary<int, FSMStateNode>();
        private readonly Dictionary<object, FSMTransitionEdge> transitionEdges =
            new Dictionary<object, FSMTransitionEdge>();
        private readonly List<FSMStateNode> movingStateNodes = new List<FSMStateNode>();
        private readonly List<Vector2> movingStatePositions = new List<Vector2>();

        private FSMData fsmData;
        private FSMEntryNode entryNode;
        private FSMTransitionEdge entryEdge;
        private FSMStateNode transitionSourceNode;
        private bool isEditable;
        private bool isDataRefreshScheduled;
        private bool isMoveUpdateScheduled;
        private bool isStateMoveActive;
        private Vector2 stateMoveStartPosition;

        public Action<DropdownMenu, Vector2> CreateStateMenuRequested;
        public Action StateMoveStarted;
        public Action<FSMStateData, Vector2> StateMoved;
        public Action<FSMStateData> StateRemoved;
        public Action<FSMStateData> InitialStateRequested;
        public Func<int, int, FSMTransitionData> TransitionCreated;
        public Action<FSMTransitionData> TransitionRemoved;
        public Action<object> ElementSelected;

        /// <summary>
        /// Animator처럼 상태를 중심으로 탐색할 수 있는 배경, 확대와 선택 기능 구성
        /// </summary>
        public FSMGraphView()
        {
            name = "FSM Graph";
            style.flexGrow = 1.0f;

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            SetupZoom(0.2f, 2.0f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var miniMap = new MiniMap
            {
                anchored = true
            };
            miniMap.SetPosition(new Rect(12.0f, 32.0f, 190.0f, 120.0f));
            Add(miniMap);

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            // SelectionDragger가 이벤트를 소비하더라도 드래그 종료 상태를 반드시 정리한다.
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
        }

        /// <summary>
        /// 에디트 모드에서 FSMData를 Animator형 상태 그래프로 표시
        /// </summary>
        public void SetFSMData(FSMData fsmData)
        {
            SetFSMData(fsmData, true);
        }

        private void SetFSMData(FSMData fsmData, bool frameGraph)
        {
            ClearGraph();
            this.fsmData = fsmData;
            this.isEditable = fsmData != null;
            this.isDataRefreshScheduled = false;
            if (fsmData == null)
                return;

            IReadOnlyList<FSMStateData> states = fsmData.States;
            for (int i = 0; i < states.Count; i++)
            {
                FSMStateData state = states[i];
                var stateNode = new FSMStateNode(
                    state.ID,
                    state.Name,
                    state,
                    state.ID == fsmData.InitialStateID,
                    true);
                stateNode.SetPosition(new Rect(state.Position, FSMStateNode.NodeSize));
                this.stateNodes.Add(state.ID, stateNode);
                AddElement(stateNode);
            }

            AddEntryVisual(fsmData.InitialStateID);

            IReadOnlyList<FSMTransitionData> transitions = fsmData.Transitions;
            for (int i = 0; i < transitions.Count; i++)
            {
                FSMTransitionData transition = transitions[i];
                AddTransitionVisual(
                    transition,
                    transition,
                    transition.FromStateID,
                    transition.ToStateID,
                    CreateTransitionTooltip(transition.Name, transition.GetMode()),
                    CalculateCurveOffset(transitions, i),
                    CalculateMarkerTime(transitions, i),
                    true);
            }

            schedule.Execute(UpdateAllTransitionMarkers).ExecuteLater(1);
            if (frameGraph)
                schedule.Execute(FrameGraph).ExecuteLater(1);
        }

        /// <summary>
        /// 플레이 모드에서 선택한 상태 머신을 읽기 전용 그래프로 표시
        /// </summary>
        public void SetStateMachine(IStateMachine stateMachine)
        {
            ClearGraph();
            this.fsmData = null;
            this.isEditable = false;
            this.isDataRefreshScheduled = false;
            if (stateMachine == null)
                return;

            List<State> states = new List<State>(stateMachine.GetStates().Values);
            states.Sort((left, right) => left.ID.CompareTo(right.ID));
            FSMData sourceData = stateMachine.GetSourceData();

            for (int i = 0; i < states.Count; i++)
            {
                State state = states[i];
                FSMStateData sourceState = sourceData?.FindState(state.ID);
                bool isInitial = sourceData != null && sourceData.InitialStateID == state.ID;
                string stateName = sourceState != null && !string.IsNullOrWhiteSpace(sourceState.Name)
                    ? sourceState.Name
                    : state.Name;
                var stateNode = new FSMStateNode(state.ID, stateName, null, isInitial, false);
                stateNode.SetPosition(sourceState != null
                    ? new Rect(sourceState.Position, FSMStateNode.NodeSize)
                    : CalculateNodePosition(i, states.Count));
                this.stateNodes.Add(state.ID, stateNode);
                AddElement(stateNode);
            }

            int? entryStateID = sourceData != null
                ? sourceData.InitialStateID
                : stateMachine.GetCurrentStateID();
            if (entryStateID.HasValue)
                AddEntryVisual(entryStateID.Value);

            IReadOnlyList<StateTransition> transitions = stateMachine.GetTransitions();
            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition transition = transitions[i];
                AddTransitionVisual(
                    transition,
                    null,
                    transition.FromStateID,
                    transition.ToStateID,
                    CreateTransitionTooltip(transition.Name, transition.GetMode()),
                    CalculateCurveOffset(transitions, i),
                    CalculateMarkerTime(transitions, i),
                    false);
            }

            SetActiveState(stateMachine.GetCurrentStateID());
            schedule.Execute(UpdateAllTransitionMarkers).ExecuteLater(1);
            schedule.Execute(FrameGraph).ExecuteLater(1);
        }

        /// <summary>
        /// 현재 실행 중인 상태 노드 강조
        /// </summary>
        public void SetActiveState(int? stateID)
        {
            foreach (KeyValuePair<int, FSMStateNode> stateNode in this.stateNodes)
                stateNode.Value.SetActive(stateID.HasValue && stateNode.Key == stateID.Value);
        }

        /// <summary>
        /// 초기 상태 변경을 노드 색상과 Entry 화살표에 반영
        /// </summary>
        public void RefreshInitialState()
        {
            if (this.fsmData == null)
                return;

            foreach (KeyValuePair<int, FSMStateNode> stateNode in this.stateNodes)
                stateNode.Value.SetInitial(stateNode.Key == this.fsmData.InitialStateID);
            RebuildEntryVisual(this.fsmData.InitialStateID);
        }

        /// <summary>
        /// 상세 패널에서 변경한 상태 또는 전이 이름을 그래프에 즉시 반영
        /// </summary>
        public void RefreshElementName(object elementData)
        {
            if (elementData is FSMStateData state &&
                this.stateNodes.TryGetValue(state.ID, out FSMStateNode stateNode))
            {
                stateNode.SetStateName(state.Name);
                return;
            }

            if (elementData is FSMTransitionData transition &&
                this.transitionEdges.TryGetValue(transition, out FSMTransitionEdge edge))
                edge.SetTransitionName(CreateTransitionTooltip(
                    transition.Name,
                    transition.GetMode()));
        }

        private static string CreateTransitionTooltip(
            string transitionName,
            FSMTransitionMode mode)
        {
            return $"[{mode}] {transitionName}";
        }

        public void SelectTransition(FSMTransitionData transition)
        {
            if (transition != null &&
                this.transitionEdges.TryGetValue(transition, out FSMTransitionEdge edge))
            {
                SelectTransition(edge);
            }
        }

        /// <summary>
        /// 마지막으로 평가된 전이선 강조
        /// </summary>
        public void HighlightTransition(StateTransition transition, StateChangeResult result)
        {
            ClearTransitionHighlight();
            if (transition == null ||
                !this.transitionEdges.TryGetValue(transition, out FSMTransitionEdge edge))
                return;

            Color color = result == StateChangeResult.Success
                ? SuccessEdgeColor
                : result == StateChangeResult.Pending
                    ? PendingEdgeColor
                    : FailedEdgeColor;
            SetEdgeColor(edge, color);
        }

        public void ClearTransitionHighlight()
        {
            foreach (FSMTransitionEdge edge in this.transitionEdges.Values)
                SetEdgeColor(edge, DefaultEdgeColor);
        }

        /// <summary>
        /// 상태에서는 Animator식 전이 생성 메뉴를, 빈 공간에서는 상태 생성 메뉴를 표시
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent menuEvent)
        {
            base.BuildContextualMenu(menuEvent);
            if (!this.isEditable)
                return;

            VisualElement target = menuEvent.target as VisualElement;
            FSMStateNode stateNode = target as FSMStateNode ??
                target?.GetFirstAncestorOfType<FSMStateNode>();
            if (stateNode != null && stateNode.GetStateData() != null)
            {
                menuEvent.menu.AppendSeparator();
                menuEvent.menu.AppendAction("Make Transition", _ => BeginTransitionCreation(stateNode));
                menuEvent.menu.AppendAction(
                    "Set as Initial State",
                    _ => this.InitialStateRequested?.Invoke(stateNode.GetStateData()),
                    stateNode.GetStateID() == this.fsmData.InitialStateID
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
                return;
            }

            GraphElement graphElement = target as GraphElement ??
                target?.GetFirstAncestorOfType<GraphElement>();
            if (graphElement != null)
                return;

            if (this.transitionSourceNode != null)
            {
                menuEvent.menu.AppendAction("Cancel Transition", _ => CancelTransitionCreation());
                return;
            }

            Vector2 graphPosition = contentViewContainer.WorldToLocal(menuEvent.mousePosition);
            this.CreateStateMenuRequested?.Invoke(menuEvent.menu, graphPosition);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // 런타임 디버그 화면도 배치는 가능해야 하므로 이동 처리는 편집 가능 여부와 분리한다.
            HandleMovedElements(change.movedElements);
            if (!this.isEditable)
                return change;

            HandleRemovedElements(change.elementsToRemove);
            return change;
        }

        private void HandleMovedElements(List<GraphElement> movedElements)
        {
            if (movedElements == null || movedElements.Count == 0)
                return;

            // GraphViewChange는 노드에 새 좌표가 적용되기 전에 호출될 수 있다.
            // 다음 UI 틱에서 실제 좌표를 읽어 데이터와 연결 표시를 함께 갱신한다.
            ScheduleMovedElementsUpdate();
        }

        private void OnMouseUp(MouseUpEvent mouseEvent)
        {
            EndStateMove();
            schedule.Execute(OnSelectionChanged).ExecuteLater(1);
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent captureEvent)
        {
            EndStateMove();
        }

        /// <summary>
        /// GraphView 기본 SelectionDragger의 내부 상태와 무관하게 선택된 상태 노드를 이동
        /// </summary>
        private void BeginStateMove(FSMStateNode targetNode, Vector2 mousePosition)
        {
            if (!selection.Contains(targetNode))
            {
                ClearSelection();
                AddToSelection(targetNode);
            }

            this.movingStateNodes.Clear();
            this.movingStatePositions.Clear();
            foreach (ISelectable selectedElement in selection)
            {
                if (!(selectedElement is FSMStateNode stateNode) ||
                    (stateNode.capabilities & Capabilities.Movable) == 0)
                    continue;

                this.movingStateNodes.Add(stateNode);
                this.movingStatePositions.Add(stateNode.GetPosition().position);
            }

            if (this.movingStateNodes.Count == 0)
                return;

            this.stateMoveStartPosition = contentViewContainer.WorldToLocal(mousePosition);
            this.isStateMoveActive = true;
            this.CaptureMouse();
        }

        private void OnMouseMove(MouseMoveEvent mouseEvent)
        {
            if (!this.isStateMoveActive)
                return;

            Vector2 currentPosition = contentViewContainer.WorldToLocal(mouseEvent.mousePosition);
            Vector2 moveDelta = currentPosition - this.stateMoveStartPosition;
            for (int i = 0; i < this.movingStateNodes.Count; i++)
            {
                FSMStateNode stateNode = this.movingStateNodes[i];
                Rect position = stateNode.GetPosition();
                position.position = this.movingStatePositions[i] + moveDelta;
                stateNode.SetPosition(position);
            }

            UpdateAllTransitionMarkers();
            mouseEvent.StopImmediatePropagation();
        }

        /// <summary>
        /// 드래그 중에는 시각 요소만 옮기고, 종료 시 최종 좌표만 데이터에 기록한다.
        /// </summary>
        private void EndStateMove()
        {
            if (!this.isStateMoveActive)
                return;

            this.isStateMoveActive = false;
            if (this.HasMouseCapture())
                this.ReleaseMouse();

            bool hasRecordedUndo = false;
            for (int i = 0; i < this.movingStateNodes.Count; i++)
            {
                FSMStateNode stateNode = this.movingStateNodes[i];
                FSMStateData stateData = stateNode.GetStateData();
                Vector2 position = stateNode.GetPosition().position;
                if (stateData == null || stateData.Position == position)
                    continue;

                if (this.isEditable && !hasRecordedUndo)
                {
                    hasRecordedUndo = true;
                    this.StateMoveStarted?.Invoke();
                }
                this.StateMoved?.Invoke(stateData, position);
            }

            this.movingStateNodes.Clear();
            this.movingStatePositions.Clear();
        }

        private void OnKeyUp(KeyUpEvent keyEvent)
        {
            schedule.Execute(OnSelectionChanged).ExecuteLater(1);
        }

        private void FrameGraph()
        {
            FrameAll();
        }

        private void ScheduleMovedElementsUpdate()
        {
            if (this.isMoveUpdateScheduled)
                return;

            this.isMoveUpdateScheduled = true;
            schedule.Execute(ApplyMovedElements).ExecuteLater(1);
        }

        private void ApplyMovedElements()
        {
            this.isMoveUpdateScheduled = false;
            bool hasRecordedUndo = false;
            foreach (FSMStateNode stateNode in this.stateNodes.Values)
            {
                FSMStateData stateData = stateNode.GetStateData();
                Vector2 position = stateNode.GetPosition().position;
                if (stateData == null || stateData.Position == position)
                    continue;

                if (this.isEditable && !hasRecordedUndo)
                {
                    hasRecordedUndo = true;
                    this.StateMoveStarted?.Invoke();
                }
                this.StateMoved?.Invoke(stateData, position);
            }

            UpdateAllTransitionMarkers();
        }

        /// <summary>
        /// 상태 삭제에 딸려온 선은 RemoveState가 함께 정리하므로 전이를 중복 삭제하지 않는다.
        /// </summary>
        private void HandleRemovedElements(List<GraphElement> elementsToRemove)
        {
            if (elementsToRemove == null)
                return;

            var removedStateIDs = new HashSet<int>();
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                if (elementsToRemove[i] is FSMStateNode stateNode && stateNode.GetStateData() != null)
                    removedStateIDs.Add(stateNode.GetStateID());
            }

            bool requiresRefresh = false;
            for (int i = 0; i < elementsToRemove.Count; i++)
            {
                GraphElement element = elementsToRemove[i];
                if (element is FSMStateNode stateNode && stateNode.GetStateData() != null)
                {
                    this.StateRemoved?.Invoke(stateNode.GetStateData());
                    requiresRefresh = true;
                }
                else if (element is FSMTransitionEdge edge &&
                    edge.GetTransitionData() != null &&
                    !removedStateIDs.Contains(edge.GetFromStateID()) &&
                    !removedStateIDs.Contains(edge.GetToStateID()))
                {
                    this.TransitionRemoved?.Invoke(edge.GetTransitionData());
                    requiresRefresh = true;
                }
            }

            if (requiresRefresh)
                ScheduleDataRefresh();
        }

        private void OnMouseDown(MouseDownEvent mouseEvent)
        {
            if (mouseEvent.button != 0)
                return;

            VisualElement target = mouseEvent.target as VisualElement;
            FSMStateNode targetNode = target as FSMStateNode ??
                target?.GetFirstAncestorOfType<FSMStateNode>();
            if (targetNode == null)
                return;

            if (this.transitionSourceNode == null)
            {
                BeginStateMove(targetNode, mouseEvent.mousePosition);
                mouseEvent.StopImmediatePropagation();
                return;
            }

            if (!this.isEditable || targetNode.GetStateData() == null)
                return;

            FSMTransitionData transition = this.TransitionCreated?.Invoke(
                this.transitionSourceNode.GetStateID(),
                targetNode.GetStateID());
            CancelTransitionCreation();

            if (transition != null)
                SetFSMData(this.fsmData);
            mouseEvent.StopImmediatePropagation();
        }

        private void OnKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.Escape || this.transitionSourceNode == null)
                return;

            CancelTransitionCreation();
            keyEvent.StopImmediatePropagation();
        }

        private void BeginTransitionCreation(FSMStateNode stateNode)
        {
            CancelTransitionCreation();
            this.transitionSourceNode = stateNode;
            this.transitionSourceNode.SetTransitionSource(true);
        }

        private void CancelTransitionCreation()
        {
            if (this.transitionSourceNode != null)
                this.transitionSourceNode.SetTransitionSource(false);
            this.transitionSourceNode = null;
        }

        /// <summary>
        /// 우측 상세 패널이 표시할 상태 또는 전이 데이터 전달
        /// </summary>
        private void OnSelectionChanged()
        {
            if (this.ElementSelected == null)
                return;

            foreach (ISelectable selectedElement in selection)
            {
                if (selectedElement is FSMStateNode stateNode)
                {
                    this.ElementSelected.Invoke(stateNode.GetStateData());
                    return;
                }

                if (selectedElement is FSMTransitionEdge edge)
                {
                    this.ElementSelected.Invoke(edge.userData);
                    return;
                }
            }

            this.ElementSelected.Invoke(null);
        }

        private void AddEntryVisual(int initialStateID)
        {
            if (!this.stateNodes.TryGetValue(initialStateID, out FSMStateNode initialNode))
                return;

            Rect stateRect = initialNode.GetPosition();
            Vector2 entryPosition = new Vector2(
                stateRect.center.x - FSMEntryNode.NodeSize.x * 0.5f,
                stateRect.yMin - 105.0f);
            this.entryNode = new FSMEntryNode();
            this.entryNode.SetPosition(new Rect(entryPosition, FSMEntryNode.NodeSize));
            AddElement(this.entryNode);

            this.entryEdge = CreateTransitionEdge(
                null,
                null,
                this.entryNode.GetOutputPort(),
                initialNode.CreateCenterPort(Direction.Input),
                -1,
                initialStateID,
                string.Empty,
                0.0f,
                0.56f,
                false);
            this.entryEdge.pickingMode = PickingMode.Ignore;
            this.entryEdge.capabilities &= ~(Capabilities.Selectable | Capabilities.Deletable);
            FSMTransitionMarker marker = CreateTransitionMarker(
                this.entryNode.GetPosition().center,
                initialNode.GetPosition().center,
                0.0f,
                0.56f,
                false,
                EntryEdgeColor);
            this.entryEdge.SetTransitionMarker(marker);
            SetEdgeColor(this.entryEdge, EntryEdgeColor);
        }

        private void RebuildEntryVisual(int initialStateID)
        {
            RemoveTransitionVisual(this.entryEdge);
            this.entryEdge = null;
            if (this.entryNode != null)
            {
                RemoveElement(this.entryNode);
                this.entryNode = null;
            }

            AddEntryVisual(initialStateID);
        }

        private void AddTransitionVisual(
            object transitionKey,
            FSMTransitionData transitionData,
            int fromStateID,
            int toStateID,
            string transitionName,
            float curveOffset,
            float markerTime,
            bool editable)
        {
            if (!this.stateNodes.TryGetValue(fromStateID, out FSMStateNode fromNode) ||
                !this.stateNodes.TryGetValue(toStateID, out FSMStateNode toNode))
                return;

            bool isSelfTransition = fromStateID == toStateID;
            Port outputPort;
            Port inputPort;
            if (isSelfTransition)
            {
                outputPort = fromNode.CreateSelfPort(Direction.Output, -10.0f);
                inputPort = toNode.CreateSelfPort(Direction.Input, 10.0f);
            }
            else
            {
                outputPort = fromNode.CreateCenterPort(Direction.Output);
                inputPort = toNode.CreateCenterPort(Direction.Input);
            }

            FSMTransitionEdge edge = CreateTransitionEdge(
                transitionKey,
                transitionData,
                outputPort,
                inputPort,
                fromStateID,
                toStateID,
                transitionName,
                curveOffset,
                markerTime,
                isSelfTransition);
            if (!editable)
                edge.capabilities &= ~Capabilities.Deletable;

            this.transitionEdges.Add(transitionKey, edge);
            FSMTransitionMarker marker = CreateTransitionMarker(
                fromNode.GetPosition().center,
                toNode.GetPosition().center,
                curveOffset,
                markerTime,
                isSelfTransition,
                DefaultEdgeColor);
            edge.SetTransitionMarker(marker);
            SetEdgeColor(edge, DefaultEdgeColor);
        }

        private FSMTransitionEdge CreateTransitionEdge(
            object transitionKey,
            FSMTransitionData transitionData,
            Port outputPort,
            Port inputPort,
            int fromStateID,
            int toStateID,
            string transitionName,
            float curveOffset,
            float markerTime,
            bool isSelfTransition)
        {
            var edge = new FSMTransitionEdge(
                transitionKey,
                transitionData,
                fromStateID,
                toStateID,
                transitionName,
                curveOffset,
                markerTime,
                isSelfTransition)
            {
                output = outputPort,
                input = inputPort,
                userData = transitionData ?? transitionKey
            };
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            edge.SelectionRequested = SelectTransition;
            AddElement(edge);
            edge.edgeControl.edgeWidth = 2;
            edge.edgeControl.interceptWidth = 14;
            edge.edgeControl.drawFromCap = false;
            edge.edgeControl.drawToCap = false;
            return edge;
        }

        /// <summary>
        /// 가느다란 선의 기본 선택 이벤트에 의존하지 않고 클릭한 전이 데이터를 상세 패널에 전달
        /// </summary>
        private void SelectTransition(FSMTransitionEdge edge)
        {
            if (edge == null)
                return;

            ClearSelection();
            AddToSelection(edge);
            this.ElementSelected?.Invoke(edge.GetTransitionData() ?? edge.GetTransitionKey());
        }

        private FSMTransitionMarker CreateTransitionMarker(
            Vector2 start,
            Vector2 end,
            float curveOffset,
            float markerTime,
            bool isSelfTransition,
            Color color)
        {
            CalculateCurvePointAndTangent(
                start,
                end,
                curveOffset,
                isSelfTransition,
                markerTime,
                out Vector2 point,
                out Vector2 tangent);
            var marker = new FSMTransitionMarker();
            marker.SetDirection(point, tangent);
            marker.SetColor(color);
            AddElement(marker);
            marker.BringToFront();
            return marker;
        }

        /// <summary>
        /// 노드를 다시 만들지 않고 드래그 중인 선의 방향 마커 위치만 갱신
        /// </summary>
        private void UpdateAllTransitionMarkers()
        {
            foreach (FSMTransitionEdge edge in this.transitionEdges.Values)
            {
                if (!this.stateNodes.TryGetValue(edge.GetFromStateID(), out FSMStateNode fromNode) ||
                    !this.stateNodes.TryGetValue(edge.GetToStateID(), out FSMStateNode toNode))
                    continue;

                edge.UpdateTransitionMarker(
                    fromNode.GetPosition().center,
                    toNode.GetPosition().center);
            }

            if (this.entryEdge != null && this.entryNode != null &&
                this.stateNodes.TryGetValue(this.entryEdge.GetToStateID(), out FSMStateNode initialNode))
            {
                this.entryEdge.UpdateTransitionMarker(
                    this.entryNode.GetPosition().center,
                    initialNode.GetPosition().center);
            }
        }

        private void ScheduleDataRefresh()
        {
            if (this.isDataRefreshScheduled)
                return;

            this.isDataRefreshScheduled = true;
            schedule.Execute(RefreshDataAfterGraphChange).ExecuteLater(1);
        }

        private void RefreshDataAfterGraphChange()
        {
            this.isDataRefreshScheduled = false;
            if (this.isEditable)
                SetFSMData(this.fsmData, false);
        }

        private void RemoveTransitionVisual(FSMTransitionEdge edge)
        {
            if (edge == null)
                return;

            FSMTransitionMarker marker = edge.GetTransitionMarker();
            if (marker != null)
                RemoveElement(marker);
            RemoveElement(edge);
        }

        private void ClearGraph()
        {
            this.isEditable = false;
            this.isDataRefreshScheduled = false;
            this.isMoveUpdateScheduled = false;
            this.isStateMoveActive = false;
            CancelTransitionCreation();
            ClearSelection();

            RemoveTransitionVisual(this.entryEdge);
            foreach (FSMTransitionEdge edge in this.transitionEdges.Values)
                RemoveTransitionVisual(edge);
            if (this.entryNode != null)
                RemoveElement(this.entryNode);
            foreach (FSMStateNode stateNode in this.stateNodes.Values)
                RemoveElement(stateNode);

            this.entryEdge = null;
            this.entryNode = null;
            this.transitionEdges.Clear();
            this.stateNodes.Clear();
            this.ElementSelected?.Invoke(null);
        }

        private static float CalculateCurveOffset(
            IReadOnlyList<FSMTransitionData> transitions,
            int transitionIndex)
        {
            FSMTransitionData current = transitions[transitionIndex];
            bool hasReverse = false;

            for (int i = 0; i < transitions.Count; i++)
            {
                FSMTransitionData transition = transitions[i];
                if (transition.FromStateID == current.ToStateID &&
                    transition.ToStateID == current.FromStateID)
                {
                    hasReverse = true;
                }
            }

            return hasReverse && current.FromStateID != current.ToStateID
                ? ReverseTransitionOffset
                : 0.0f;
        }

        private static float CalculateCurveOffset(
            IReadOnlyList<StateTransition> transitions,
            int transitionIndex)
        {
            StateTransition current = transitions[transitionIndex];
            bool hasReverse = false;

            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition transition = transitions[i];
                if (transition.FromStateID == current.ToStateID &&
                    transition.ToStateID == current.FromStateID)
                {
                    hasReverse = true;
                }
            }

            return hasReverse && current.FromStateID != current.ToStateID
                ? ReverseTransitionOffset
                : 0.0f;
        }

        private static float CalculateMarkerTime(
            IReadOnlyList<FSMTransitionData> transitions,
            int transitionIndex)
        {
            FSMTransitionData current = transitions[transitionIndex];
            int sameCount = 0;
            int sameIndex = 0;
            for (int i = 0; i < transitions.Count; i++)
            {
                FSMTransitionData transition = transitions[i];
                if (transition.FromStateID != current.FromStateID ||
                    transition.ToStateID != current.ToStateID)
                    continue;

                if (i < transitionIndex)
                    sameIndex++;
                sameCount++;
            }

            return CalculateMarkerTime(sameIndex, sameCount);
        }

        private static float CalculateMarkerTime(
            IReadOnlyList<StateTransition> transitions,
            int transitionIndex)
        {
            StateTransition current = transitions[transitionIndex];
            int sameCount = 0;
            int sameIndex = 0;
            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition transition = transitions[i];
                if (transition.FromStateID != current.FromStateID ||
                    transition.ToStateID != current.ToStateID)
                    continue;

                if (i < transitionIndex)
                    sameIndex++;
                sameCount++;
            }

            return CalculateMarkerTime(sameIndex, sameCount);
        }

        private static float CalculateMarkerTime(int sameIndex, int sameCount)
        {
            float centeredIndex = sameIndex - (sameCount - 1) * 0.5f;
            return Mathf.Clamp(0.5f + centeredIndex * 0.07f, 0.25f, 0.75f);
        }

        /// <summary>
        /// 저장 위치가 없는 런타임 FSM을 원형으로 자동 배치
        /// </summary>
        private static Rect CalculateNodePosition(int index, int stateCount)
        {
            Vector2 center = new Vector2(420.0f, 310.0f);
            if (stateCount <= 1)
                return new Rect(center - FSMStateNode.NodeSize * 0.5f, FSMStateNode.NodeSize);

            float radius = Mathf.Max(210.0f, stateCount * 42.0f);
            float angle = -Mathf.PI * 0.5f + Mathf.PI * 2.0f * index / stateCount;
            Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            position -= FSMStateNode.NodeSize * 0.5f;
            return new Rect(position, FSMStateNode.NodeSize);
        }

        /// <summary>
        /// 선 렌더링과 중앙 방향 마커가 같은 베지어 곡선을 사용하도록 제어점 계산 공유
        /// </summary>
        private static void CalculateCurveControlPoints(
            Vector2 start,
            Vector2 end,
            float curveOffset,
            bool isSelfTransition,
            out Vector2 point0,
            out Vector2 point1,
            out Vector2 point2,
            out Vector2 point3)
        {
            point0 = start;
            point3 = end;

            if (isSelfTransition)
            {
                float loopWidth = 85.0f + Mathf.Abs(curveOffset);
                point1 = start + new Vector2(loopWidth, -55.0f);
                point2 = end + new Vector2(loopWidth, 55.0f);
                return;
            }

            Vector2 distance = end - start;
            Vector2 normal = distance.sqrMagnitude > 0.001f
                ? new Vector2(-distance.y, distance.x).normalized
                : Vector2.up;
            Vector2 offset = normal * curveOffset;
            point0 += offset;
            point3 += offset;
            point1 = Vector2.Lerp(point0, point3, 0.33f);
            point2 = Vector2.Lerp(point0, point3, 0.67f);
        }

        private static void CalculateCurvePointAndTangent(
            Vector2 start,
            Vector2 end,
            float curveOffset,
            bool isSelfTransition,
            float time,
            out Vector2 point,
            out Vector2 tangent)
        {
            CalculateCurveControlPoints(
                start,
                end,
                curveOffset,
                isSelfTransition,
                out Vector2 point0,
                out Vector2 point1,
                out Vector2 point2,
                out Vector2 point3);

            float inverseTime = 1.0f - time;
            point = inverseTime * inverseTime * inverseTime * point0 +
                3.0f * inverseTime * inverseTime * time * point1 +
                3.0f * inverseTime * time * time * point2 +
                time * time * time * point3;
            tangent = 3.0f * inverseTime * inverseTime * (point1 - point0) +
                6.0f * inverseTime * time * (point2 - point1) +
                3.0f * time * time * (point3 - point2);
        }

        private static void SetEdgeColor(Edge edge, Color color)
        {
            if (edge.edgeControl == null)
                return;

            edge.edgeControl.inputColor = color;
            edge.edgeControl.outputColor = color;
            if (edge is FSMTransitionEdge transitionEdge)
                transitionEdge.SetArrowColor(color);
            edge.edgeControl.MarkDirtyRepaint();
        }

        private sealed class FSMTransitionEdge : Edge
        {
            private readonly object transitionKey;
            private readonly FSMTransitionData transitionData;
            private readonly int fromStateID;
            private readonly int toStateID;
            private readonly float curveOffset;
            private readonly float markerTime;
            private readonly bool isSelfTransition;
            private FSMTransitionMarker transitionMarker;

            public Action<FSMTransitionEdge> SelectionRequested;

            public FSMTransitionEdge(
                object transitionKey,
                FSMTransitionData transitionData,
                int fromStateID,
                int toStateID,
                string transitionName,
                float curveOffset,
                float markerTime,
                bool isSelfTransition)
            {
                this.transitionKey = transitionKey;
                this.transitionData = transitionData;
                this.fromStateID = fromStateID;
                this.toStateID = toStateID;
                this.curveOffset = curveOffset;
                this.markerTime = markerTime;
                this.isSelfTransition = isSelfTransition;
                tooltip = transitionName;
                ((FSMAnimatorEdgeControl)edgeControl).SetCurve(curveOffset, isSelfTransition);
                RegisterCallback<MouseDownEvent>(OnMouseDown);
            }

            private void OnMouseDown(MouseDownEvent mouseEvent)
            {
                if (mouseEvent.button != 0)
                    return;

                RequestSelection();
                mouseEvent.StopPropagation();
            }

            protected override EdgeControl CreateEdgeControl()
            {
                return new FSMAnimatorEdgeControl();
            }

            public object GetTransitionKey() => this.transitionKey;

            public FSMTransitionData GetTransitionData() => this.transitionData;

            public FSMTransitionMarker GetTransitionMarker() => this.transitionMarker;

            public int GetFromStateID() => this.fromStateID;

            public int GetToStateID() => this.toStateID;

            public void RequestSelection()
            {
                this.SelectionRequested?.Invoke(this);
            }

            public void SetTransitionName(string transitionName)
            {
                tooltip = transitionName;
            }

            public void SetTransitionMarker(FSMTransitionMarker transitionMarker)
            {
                this.transitionMarker = transitionMarker;
                this.transitionMarker?.SetTransitionEdge(this);
            }

            public void SetArrowColor(Color color)
            {
                this.transitionMarker?.SetColor(color);
            }

            public void UpdateTransitionMarker(Vector2 start, Vector2 end)
            {
                if (this.transitionMarker == null)
                    return;

                CalculateCurvePointAndTangent(
                    start,
                    end,
                    this.curveOffset,
                    this.isSelfTransition,
                    this.markerTime,
                    out Vector2 point,
                    out Vector2 tangent);
                this.transitionMarker.SetDirection(point, tangent);
                this.transitionMarker.BringToFront();
            }
        }

        private sealed class FSMTransitionMarker : GraphElement
        {
            private static readonly Vector2 MarkerSize = new Vector2(18.0f, 18.0f);

            private readonly VisualElement arrowVisual;
            private readonly VisualElement arrowHead;
            private FSMTransitionEdge transitionEdge;

            public FSMTransitionMarker()
            {
                capabilities &= ~(
                    Capabilities.Copiable |
                    Capabilities.Deletable |
                    Capabilities.Movable |
                    Capabilities.Renamable |
                    Capabilities.Selectable);
                pickingMode = PickingMode.Position;
                AddToClassList("fsm-transition-marker");

                this.arrowVisual = new VisualElement();
                this.arrowVisual.pickingMode = PickingMode.Ignore;
                this.arrowVisual.AddToClassList("fsm-transition-arrow");
                this.arrowHead = new VisualElement();
                this.arrowHead.pickingMode = PickingMode.Ignore;
                this.arrowHead.AddToClassList("fsm-transition-arrow-head");
                this.arrowHead.style.rotate = new Rotate(new Angle(45.0f, AngleUnit.Degree));
                this.arrowVisual.Add(this.arrowHead);
                Add(this.arrowVisual);
                RegisterCallback<MouseDownEvent>(OnMouseDown);
            }

            public void SetTransitionEdge(FSMTransitionEdge transitionEdge)
            {
                this.transitionEdge = transitionEdge;
            }

            public void SetDirection(Vector2 position, Vector2 tangent)
            {
                SetPosition(new Rect(position - MarkerSize * 0.5f, MarkerSize));
                float angle = tangent.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg
                    : 0.0f;
                this.arrowVisual.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }

            public void SetColor(Color color)
            {
                this.arrowHead.style.borderTopColor = color;
                this.arrowHead.style.borderRightColor = color;
            }

            private void OnMouseDown(MouseDownEvent mouseEvent)
            {
                if (mouseEvent.button != 0 || this.transitionEdge == null)
                    return;

                this.transitionEdge.RequestSelection();
                mouseEvent.StopPropagation();
            }
        }

        private sealed class FSMAnimatorEdgeControl : EdgeControl
        {
            private float curveOffset;
            private bool isSelfTransition;

            public void SetCurve(float curveOffset, bool isSelfTransition)
            {
                this.curveOffset = curveOffset;
                this.isSelfTransition = isSelfTransition;
            }

            protected override void ComputeControlPoints()
            {
                base.ComputeControlPoints();
                Vector2[] points = controlPoints;
                if (points == null || points.Length < 4)
                    return;

                CalculateControlPoints(from, to, points);
            }

            private void CalculateControlPoints(Vector2 start, Vector2 end, Vector2[] points)
            {
                CalculateCurveControlPoints(
                    start,
                    end,
                    this.curveOffset,
                    this.isSelfTransition,
                    out points[0],
                    out points[1],
                    out points[2],
                    out points[3]);
            }
        }

        private sealed class FSMEntryNode : Node
        {
            public static readonly Vector2 NodeSize = new Vector2(150.0f, 38.0f);

            private readonly Port outputPort;

            public FSMEntryNode()
            {
                title = "Entry";
                capabilities &= ~(
                    Capabilities.Copiable |
                    Capabilities.Deletable |
                    Capabilities.Movable |
                    Capabilities.Renamable |
                    Capabilities.Selectable);
                AddToClassList("fsm-entry-node");

                this.outputPort = CreateHiddenPort(Direction.Output);
                this.outputPort.style.left = NodeSize.x * 0.5f;
                this.outputPort.style.top = NodeSize.y * 0.5f;
                Add(this.outputPort);
                inputContainer.style.display = DisplayStyle.None;
                outputContainer.style.display = DisplayStyle.None;
                extensionContainer.style.display = DisplayStyle.None;
                titleButtonContainer.style.display = DisplayStyle.None;
                RefreshExpandedState();
            }

            public Port GetOutputPort() => this.outputPort;
        }

        private sealed class FSMStateNode : Node
        {
            public static readonly Vector2 NodeSize = new Vector2(200.0f, 58.0f);

            private readonly int stateID;
            private readonly FSMStateData stateData;
            private bool isInitial;
            private bool isActive;

            public FSMStateNode(
                int stateID,
                string stateName,
                FSMStateData stateData,
                bool isInitial,
                bool editable)
            {
                this.stateID = stateID;
                this.stateData = stateData;
                this.isInitial = isInitial;
                title = stateName;
                tooltip = $"State ID: {stateID}";
                // 상태 노드의 핵심 조작은 Unity GraphView의 기본 capability 값에 의존하지 않는다.
                capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Ascendable;
                capabilities &= ~(Capabilities.Copiable | Capabilities.Renamable);
                if (!editable)
                    capabilities &= ~Capabilities.Deletable;
                AddToClassList("fsm-state-node");

                inputContainer.style.display = DisplayStyle.None;
                outputContainer.style.display = DisplayStyle.None;
                extensionContainer.style.display = DisplayStyle.None;
                titleButtonContainer.style.display = DisplayStyle.None;
                RefreshExpandedState();
                RefreshVisualState();
            }

            public int GetStateID() => this.stateID;

            public FSMStateData GetStateData() => this.stateData;

            public Port CreateCenterPort(Direction direction)
            {
                Port port = CreateHiddenPort(direction);
                port.style.left = NodeSize.x * 0.5f;
                port.style.top = NodeSize.y * 0.5f;
                Add(port);
                return port;
            }

            public Port CreateSelfPort(Direction direction, float verticalOffset)
            {
                Port port = CreateHiddenPort(direction);
                port.style.left = NodeSize.x * 0.5f;
                port.style.top = NodeSize.y * 0.5f + verticalOffset;
                Add(port);
                return port;
            }

            public void SetInitial(bool initial)
            {
                this.isInitial = initial;
                RefreshVisualState();
            }

            public void SetStateName(string stateName)
            {
                title = stateName;
            }

            public void SetActive(bool active)
            {
                this.isActive = active;
                RefreshVisualState();
            }

            public void SetTransitionSource(bool transitionSource)
            {
                EnableInClassList("fsm-state-node--transition-source", transitionSource);
            }

            private void RefreshVisualState()
            {
                EnableInClassList("fsm-state-node--initial", this.isInitial);
                EnableInClassList("fsm-state-node--active", this.isActive);
            }
        }

        private static Port CreateHiddenPort(Direction direction)
        {
            Port port = Port.Create<Edge>(
                Orientation.Horizontal,
                direction,
                Port.Capacity.Multi,
                typeof(bool));
            port.portName = string.Empty;
            port.pickingMode = PickingMode.Ignore;
            port.AddToClassList("fsm-hidden-port");
            return port;
        }
    }
}
