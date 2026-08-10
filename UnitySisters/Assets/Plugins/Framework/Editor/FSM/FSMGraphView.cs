using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.FSM.Editor
{
    internal class FSMGraphView : GraphView
    {
        private static readonly Color DefaultEdgeColor = new Color(0.45f, 0.5f, 0.56f);
        private static readonly Color SuccessEdgeColor = new Color(0.2f, 0.85f, 0.55f);
        private static readonly Color FailedEdgeColor = new Color(1.0f, 0.32f, 0.28f);

        private readonly Dictionary<int, FSMStateNode> stateNodes =
            new Dictionary<int, FSMStateNode>();
        private readonly Dictionary<object, Edge> transitionEdges =
            new Dictionary<object, Edge>();

        private FSMData fsmData;
        private bool isEditable;

        public Func<Vector2, FSMStateData> CreateStateRequested;
        public Action<FSMStateData, Vector2> StateMoved;
        public Action<FSMStateData> StateRemoved;
        public Func<int, int, FSMTransitionData> TransitionCreated;
        public Action<FSMTransitionData> TransitionRemoved;
        public Action<object> ElementSelected;

        /// <summary>
        /// FSM 그래프 탐색과 편집에 필요한 배경, 확대와 이동 기능 구성
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
            RegisterCallback<MouseUpEvent>(_ => schedule.Execute(OnSelectionChanged).ExecuteLater(1));
            RegisterCallback<KeyUpEvent>(_ => schedule.Execute(OnSelectionChanged).ExecuteLater(1));
        }

        /// <summary>
        /// 에디트 모드에서 FSMData의 저장된 구조를 편집 가능한 그래프로 표시
        /// </summary>
        public void SetFSMData(FSMData fsmData)
        {
            ClearGraph();
            this.fsmData = fsmData;
            this.isEditable = fsmData != null;
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

            IReadOnlyList<FSMTransitionData> transitions = fsmData.Transitions;
            for (int i = 0; i < transitions.Count; i++)
                AddDataTransition(transitions[i]);

            schedule.Execute(() => FrameAll()).ExecuteLater(1);
        }

        /// <summary>
        /// 플레이 모드에서 선택한 상태 머신을 읽기 전용 그래프로 표시
        /// </summary>
        public void SetStateMachine(IStateMachine stateMachine)
        {
            ClearGraph();
            this.fsmData = null;
            this.isEditable = false;
            if (stateMachine == null)
                return;

            List<State> states = new List<State>(stateMachine.GetStates().Values);
            states.Sort((left, right) => left.ID.CompareTo(right.ID));

            for (int i = 0; i < states.Count; i++)
            {
                State state = states[i];
                var stateNode = new FSMStateNode(state.ID, state.Name, null, false, false);
                FSMStateData sourceState = stateMachine.GetSourceData()?.FindState(state.ID);
                stateNode.SetPosition(sourceState != null
                    ? new Rect(sourceState.Position, FSMStateNode.NodeSize)
                    : CalculateNodePosition(i, states.Count));
                this.stateNodes.Add(state.ID, stateNode);
                AddElement(stateNode);
            }

            IReadOnlyList<StateTransition> transitions = stateMachine.GetTransitions();
            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition transition = transitions[i];
                Edge edge = CreateEdge(transition.FromStateID, transition.ToStateID, transition.Name, false);
                if (edge != null)
                    this.transitionEdges.Add(transition, edge);
            }

            SetActiveState(stateMachine.GetCurrentStateID());
            schedule.Execute(() => FrameAll()).ExecuteLater(1);
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
        /// 초기 상태가 변경된 뒤 모든 노드의 시작 상태 표시 갱신
        /// </summary>
        public void RefreshInitialState()
        {
            if (this.fsmData == null)
                return;

            foreach (KeyValuePair<int, FSMStateNode> stateNode in this.stateNodes)
                stateNode.Value.SetInitial(stateNode.Key == this.fsmData.InitialStateID);
        }

        /// <summary>
        /// 상세 패널에서 변경한 상태 또는 전이 이름을 그래프에 즉시 반영
        /// </summary>
        public void RefreshElementName(object elementData)
        {
            if (elementData is FSMStateData state &&
                this.stateNodes.TryGetValue(state.ID, out FSMStateNode stateNode))
                stateNode.SetStateName(state.Name);
            else if (elementData is FSMTransitionData transition &&
                this.transitionEdges.TryGetValue(transition, out Edge edge))
                edge.tooltip = transition.Name;
        }

        /// <summary>
        /// 마지막으로 평가된 전이를 성공 또는 실패 색상으로 강조
        /// </summary>
        public void HighlightTransition(StateTransition transition, StateChangeResult result)
        {
            ClearTransitionHighlight();
            if (transition == null || !this.transitionEdges.TryGetValue(transition, out Edge edge))
                return;

            Color color = result == StateChangeResult.Success
                ? SuccessEdgeColor
                : FailedEdgeColor;
            SetEdgeColor(edge, color);
            edge.BringToFront();
        }

        /// <summary>
        /// 전이 연결선 강조 상태 초기화
        /// </summary>
        public void ClearTransitionHighlight()
        {
            foreach (Edge edge in this.transitionEdges.Values)
                SetEdgeColor(edge, DefaultEdgeColor);
        }

        /// <summary>
        /// 에셋 편집 중 빈 공간의 메뉴에서 상태를 생성할 수 있도록 항목 추가
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent menuEvent)
        {
            base.BuildContextualMenu(menuEvent);
            if (!this.isEditable || this.CreateStateRequested == null)
                return;

            Vector2 graphPosition = contentViewContainer.WorldToLocal(menuEvent.mousePosition);
            menuEvent.menu.AppendAction("Create State", _ =>
            {
                FSMStateData state = this.CreateStateRequested.Invoke(graphPosition);
                if (state != null)
                    SetFSMData(this.fsmData);
            });
        }

        /// <summary>
        /// 출력 포트에서 다른 노드의 입력 포트로만 연결 허용
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (port != startPort && port.node != startPort.node && port.direction != startPort.direction)
                    compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        /// <summary>
        /// 노드 이동과 생성·삭제된 연결을 FSMData 변경 요청으로 변환
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (!this.isEditable)
                return change;

            if (change.movedElements != null)
            {
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    if (change.movedElements[i] is FSMStateNode node && node.GetStateData() != null)
                        this.StateMoved?.Invoke(node.GetStateData(), node.GetPosition().position);
                }
            }

            if (change.edgesToCreate != null)
            {
                for (int i = change.edgesToCreate.Count - 1; i >= 0; i--)
                {
                    Edge edge = change.edgesToCreate[i];
                    if (!(edge.output?.node is FSMStateNode fromNode) ||
                        !(edge.input?.node is FSMStateNode toNode))
                    {
                        change.edgesToCreate.RemoveAt(i);
                        continue;
                    }

                    FSMTransitionData transition = this.TransitionCreated?.Invoke(
                        fromNode.GetStateID(),
                        toNode.GetStateID());
                    if (transition == null)
                    {
                        change.edgesToCreate.RemoveAt(i);
                        continue;
                    }

                    edge.userData = transition;
                    edge.tooltip = transition.Name;
                    this.transitionEdges.Add(transition, edge);
                    SetEdgeColor(edge, DefaultEdgeColor);
                }
            }

            if (change.elementsToRemove != null)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    GraphElement element = change.elementsToRemove[i];
                    if (element is FSMStateNode stateNode && stateNode.GetStateData() != null)
                        this.StateRemoved?.Invoke(stateNode.GetStateData());
                    else if (element is Edge edge && edge.userData is FSMTransitionData transition)
                    {
                        this.TransitionRemoved?.Invoke(transition);
                        this.transitionEdges.Remove(transition);
                    }
                }
            }

            return change;
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

                if (selectedElement is Edge edge)
                {
                    this.ElementSelected.Invoke(edge.userData);
                    return;
                }
            }

            this.ElementSelected.Invoke(null);
        }

        private void AddDataTransition(FSMTransitionData transition)
        {
            Edge edge = CreateEdge(
                transition.FromStateID,
                transition.ToStateID,
                transition.Name,
                true);
            if (edge == null)
                return;

            edge.userData = transition;
            this.transitionEdges.Add(transition, edge);
        }

        private Edge CreateEdge(int fromStateID, int toStateID, string transitionName, bool editable)
        {
            if (!this.stateNodes.TryGetValue(fromStateID, out FSMStateNode fromNode) ||
                !this.stateNodes.TryGetValue(toStateID, out FSMStateNode toNode))
                return null;

            var edge = new Edge
            {
                output = fromNode.GetOutputPort(),
                input = toNode.GetInputPort(),
                tooltip = transitionName
            };
            if (!editable)
                edge.capabilities &= ~Capabilities.Deletable;

            edge.output.Connect(edge);
            edge.input.Connect(edge);
            AddElement(edge);
            SetEdgeColor(edge, DefaultEdgeColor);
            return edge;
        }

        private void ClearGraph()
        {
            // 화면을 다시 그릴 때 삭제 콜백이 에셋 데이터까지 지우지 않도록 편집을 먼저 잠근다.
            this.isEditable = false;
            foreach (Edge edge in this.transitionEdges.Values)
                RemoveElement(edge);
            foreach (FSMStateNode stateNode in this.stateNodes.Values)
                RemoveElement(stateNode);

            this.transitionEdges.Clear();
            this.stateNodes.Clear();
            this.ElementSelected?.Invoke(null);
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

        private static void SetEdgeColor(Edge edge, Color color)
        {
            if (edge.edgeControl == null)
                return;

            edge.edgeControl.inputColor = color;
            edge.edgeControl.outputColor = color;
            edge.edgeControl.MarkDirtyRepaint();
        }

        private sealed class FSMStateNode : Node
        {
            private static readonly Color DefaultTitleColor = new Color(0.18f, 0.2f, 0.24f);
            private static readonly Color ActiveTitleColor = new Color(0.12f, 0.56f, 0.35f);

            public static readonly Vector2 NodeSize = new Vector2(190.0f, 105.0f);

            private readonly int stateID;
            private readonly FSMStateData stateData;
            private readonly Port inputPort;
            private readonly Port outputPort;
            private readonly Label stateStatusLabel;
            private bool isInitial;

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
                capabilities &= ~(Capabilities.Copiable | Capabilities.Renamable);
                if (!editable)
                    capabilities &= ~Capabilities.Deletable;
                AddToClassList("fsm-state-node");

                this.inputPort = Port.Create<Edge>(
                    Orientation.Horizontal,
                    Direction.Input,
                    Port.Capacity.Multi,
                    typeof(bool));
                this.inputPort.portName = string.Empty;
                inputContainer.Add(this.inputPort);

                this.outputPort = Port.Create<Edge>(
                    Orientation.Horizontal,
                    Direction.Output,
                    Port.Capacity.Multi,
                    typeof(bool));
                this.outputPort.portName = string.Empty;
                outputContainer.Add(this.outputPort);

                this.stateStatusLabel = new Label();
                this.stateStatusLabel.AddToClassList("fsm-state-id");
                extensionContainer.Add(this.stateStatusLabel);

                RefreshPorts();
                RefreshExpandedState();
                SetActive(false);
            }

            public int GetStateID() => this.stateID;

            public FSMStateData GetStateData() => this.stateData;

            public Port GetInputPort() => this.inputPort;

            public Port GetOutputPort() => this.outputPort;

            public void SetInitial(bool initial)
            {
                this.isInitial = initial;
                UpdateStatusLabel(false);
            }

            public void SetStateName(string stateName)
            {
                title = stateName;
            }

            public void SetActive(bool active)
            {
                titleContainer.style.backgroundColor = active
                    ? ActiveTitleColor
                    : DefaultTitleColor;
                EnableInClassList("fsm-state-node--active", active);
                UpdateStatusLabel(active);
            }

            private void UpdateStatusLabel(bool active)
            {
                string initialText = this.isInitial ? "  INITIAL" : string.Empty;
                string activeText = active ? "  ACTIVE" : string.Empty;
                this.stateStatusLabel.text = $"ID  {this.stateID}{initialText}{activeText}";
            }
        }
    }
}
