using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.BehaviorTree.Editor
{
    internal sealed class BehaviorTreeGraphView : GraphView
    {
        private sealed class TreeNode : Node
        {
            internal readonly string id;
            internal readonly Port input;
            internal readonly Port output;
            internal TreeNode(BehaviorNodeData data, int order, Action<string> selected)
            {
                id = data?.ID;
                title = data == null ? "Root" : $"{(order < 0 ? "—" : order.ToString())}  {data.Name}";
                AddToClassList(data == null ? "bt-root-node" : "bt-tree-node");
                inputContainer.style.display = DisplayStyle.None;
                outputContainer.style.display = DisplayStyle.None;
                titleButtonContainer.style.display = DisplayStyle.None;
                if (data != null)
                {
                    input = CreateCenterPort(Direction.Input, Port.Capacity.Single);
                }
                if (data == null || !(data.Behavior is TaskNodeData))
                {
                    output = CreateCenterPort(Direction.Output, data == null ? Port.Capacity.Single : Port.Capacity.Multi);
                }
                if (data == null)
                {
                    capabilities &= ~(Capabilities.Deletable | Capabilities.Movable);
                    extensionContainer.style.display = DisplayStyle.None;
                }
                else
                {
                    RegisterCallback<MouseDownEvent>(_ => selected(id));
                    extensionContainer.Add(new Label(data.Behavior?.GetType().Name ?? "Missing behavior"));
                    if (data.Decorators.Count > 0) extensionContainer.Add(new Label($"Conditions: {data.Decorators.Count}"));
                    if (data.Services.Count > 0) extensionContainer.Add(new Label($"Services: {data.Services.Count}"));
                }
                RefreshExpandedState();
                RefreshPorts();
            }

            private Port CreateCenterPort(Direction direction, Port.Capacity capacity)
            {
                Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(bool));
                port.portName = string.Empty;
                port.pickingMode = PickingMode.Ignore;
                port.AddToClassList("bt-hidden-port");
                hierarchy.Add(port);
                return port;
            }
        }

        private sealed class ConnectionArrow : GraphElement
        {
            private readonly VisualElement head;
            internal ConnectionArrow()
            {
                capabilities = 0;
                pickingMode = PickingMode.Ignore;
                AddToClassList("bt-connection-arrow");
                head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList("bt-connection-arrow-head");
                Add(head);
            }
            internal void SetDirection(Vector2 start, Vector2 end)
            {
                SetPosition(new Rect((start + end) * 0.5f - Vector2.one * 9, Vector2.one * 18));
                Vector2 direction = end - start;
                style.rotate = new Rotate(new Angle(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, AngleUnit.Degree));
            }
            internal void SetColor(Color color) { head.style.borderTopColor = head.style.borderRightColor = color; }
        }

        private sealed class StraightEdgeControl : EdgeControl
        {
            internal Vector2 sourceSize;
            internal Vector2 targetSize;
            protected override void ComputeControlPoints()
            {
                base.ComputeControlPoints();
                if (controlPoints == null || controlPoints.Length < 4) return;
                TryGetConnectionGeometry(new Rect(from - sourceSize * 0.5f, sourceSize), new Rect(to - targetSize * 0.5f, targetSize), out Vector2 start, out Vector2 end);
                controlPoints[0] = start;
                controlPoints[1] = Vector2.Lerp(start, end, 1.0f / 3);
                controlPoints[2] = Vector2.Lerp(start, end, 2.0f / 3);
                controlPoints[3] = end;
            }
        }

        private sealed class TreeEdge : Edge
        {
            public TreeEdge() { }
            private Color? color;
            internal readonly ConnectionArrow arrow = new ConnectionArrow();
            internal void SetColor(Color? value) { color = value; ApplyColor(); }
            protected override EdgeControl CreateEdgeControl() { return new StraightEdgeControl(); }
            public override bool UpdateEdgeControl()
            {
                StraightEdgeControl control = (StraightEdgeControl)edgeControl;
                control.sourceSize = output?.node.GetPosition().size ?? Vector2.zero;
                control.targetSize = input?.node.GetPosition().size ?? Vector2.zero;
                bool result = base.UpdateEdgeControl();
                if (result) { ApplyColor(); UpdateArrow(); }
                return result;
            }
            protected override void OnCustomStyleResolved(ICustomStyle styles) { base.OnCustomStyleResolved(styles); ApplyColor(); }
            public override void OnSelected() { base.OnSelected(); ApplyColor(); }
            public override void OnUnselected() { base.OnUnselected(); ApplyColor(); }
            private void ApplyColor()
            {
                if (input == null || output == null) return;
                edgeControl.inputColor = color ?? input.portColor;
                edgeControl.outputColor = color ?? output.portColor;
                edgeControl.edgeWidth = color.HasValue ? 4 : 2;
                arrow.SetColor(color ?? input.portColor);
                edgeControl.MarkDirtyRepaint();
            }

            internal void UpdateArrow()
            {
                if (input == null || output == null || arrow.parent == null) return;
                bool visible = TryGetConnectionGeometry(output.node.GetPosition(), input.node.GetPosition(), out Vector2 start, out Vector2 end);
                Vector2[] points = edgeControl.controlPoints;
                if (visible && panel != null && points != null && points.Length >= 4)
                {
                    start = this.ChangeCoordinatesTo(arrow.parent, points[0]);
                    end = this.ChangeCoordinatesTo(arrow.parent, points[3]);
                    visible = (end - start).sqrMagnitude > 0.0001f;
                }
                arrow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible) arrow.SetDirection(start, end);
            }
        }

        private BehaviorTreeData data;
        private bool rebuilding;
        private bool readOnly;
        private TreeNode rootNode;
        private TreeNode connectionSource;
        private TreeEdge preview;
        private readonly Dictionary<string, TreeNode> treeNodes = new Dictionary<string, TreeNode>();
        private readonly List<TreeEdge> treeEdges = new List<TreeEdge>();
        internal Action<string> NodeSelected;
        internal Action Changed;
        internal Action<Vector2> AddRequested;
        internal Action<string> ConnectionMessage;
        internal bool IsConnecting { get => connectionSource != null; }

        internal BehaviorTreeGraphView()
        {
            style.flexGrow = 1;
            focusable = true;
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Plugins/Framework/Editor/BehaviorTree/BehaviorTreeGraph.uss");
            if (sheet != null) styleSheets.Add(sheet);
            Insert(0, new GridBackground());
            SetupZoom(0.2f, 2);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            graphViewChanged = OnChanged;
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        internal void ShowTree(BehaviorTreeData value, bool observing, bool frame)
        {
            rebuilding = true;
            try
            {
                CancelConnection();
                foreach (GraphElement element in new List<GraphElement>(graphElements)) RemoveElement(element);
                treeNodes.Clear(); treeEdges.Clear(); rootNode = null; data = value; readOnly = observing;
                if (data == null) return;
                TreeNode root = new TreeNode(null, 0, Select);
                rootNode = root;
                AddElement(root); root.SetPosition(new Rect(40, 30, 150, 42));
                Dictionary<string, int> orders = new Dictionary<string, int>();
                CollectOrder(data.RootID, orders);
                foreach (BehaviorNodeData definition in data.Nodes)
                {
                    TreeNode node = new TreeNode(definition, orders.TryGetValue(definition.ID, out int order) ? order : -1, Select);
                    if (readOnly) node.capabilities &= ~Capabilities.Deletable;
                    treeNodes.Add(definition.ID, node); AddElement(node);
                    node.SetPosition(new Rect(definition.Position, new Vector2(210, 120)));
                }
                if (!string.IsNullOrEmpty(data.RootID) && treeNodes.TryGetValue(data.RootID, out TreeNode first)) ConnectVisual(root.output, first.input, null, first.id);
                foreach (BehaviorNodeData definition in data.Nodes)
                    foreach (string child in definition.Children)
                        if (treeNodes.TryGetValue(child, out TreeNode target) && treeNodes[definition.ID].output != null) ConnectVisual(treeNodes[definition.ID].output, target.input, definition.ID, child);
                schedule.Execute(UpdateConnections).ExecuteLater(1);
                if (frame) schedule.Execute(() => FrameAll()).ExecuteLater(1);
            }
            finally { rebuilding = false; }
        }

        private void CollectOrder(string id, Dictionary<string, int> orders)
        {
            if (string.IsNullOrEmpty(id) || orders.ContainsKey(id)) return;
            BehaviorNodeData node = data.FindNode(id);
            if (node == null) return;
            orders.Add(id, orders.Count + 1);
            foreach (string child in node.Children) CollectOrder(child, orders);
        }

        private void Select(string id) { NodeSelected?.Invoke(id); }
        private void ConnectVisual(Port output, Port input, string parent, string child)
        {
            TreeEdge edge = output.ConnectTo<TreeEdge>(input);
            edge.userData = new string[] { parent, child };
            if (readOnly) edge.capabilities &= ~Capabilities.Deletable;
            treeEdges.Add(edge); AddElement(edge);
            AddElement(edge.arrow);
            edge.UpdateArrow();
        }

        internal static bool TryGetConnectionGeometry(Rect source, Rect target, out Vector2 start, out Vector2 end)
        {
            Vector2 direction = target.center - source.center;
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y);
            if (direction.sqrMagnitude < 0.0001f) { start = end = source.center; return false; }
            float sourceRatio = Mathf.Min(x > 0.0001f ? source.width * 0.5f / x : float.PositiveInfinity, y > 0.0001f ? source.height * 0.5f / y : float.PositiveInfinity);
            float targetRatio = Mathf.Min(x > 0.0001f ? target.width * 0.5f / x : float.PositiveInfinity, y > 0.0001f ? target.height * 0.5f / y : float.PositiveInfinity);
            if (sourceRatio + targetRatio >= 1) { start = end = (source.center + target.center) * 0.5f; return false; }
            start = source.center + direction * sourceRatio;
            end = target.center - direction * targetRatio;
            return true;
        }

        internal bool BeginConnection(string parentID)
        {
            if (readOnly || data == null) return false;
            TreeNode source = string.IsNullOrEmpty(parentID) ? rootNode : treeNodes.TryGetValue(parentID, out TreeNode node) ? node : null;
            if (source == null || source.output == null) return false;
            CancelConnection();
            connectionSource = source;
            Focus();
            source.AddToClassList("bt-connection-source");
            preview = new TreeEdge { output = source.output, pickingMode = PickingMode.Ignore, isGhostEdge = true };
            preview.capabilities = 0;
            AddElement(preview);
            preview.candidatePosition = contentViewContainer.LocalToWorld(source.GetPosition().center + Vector2.down * 100);
            ConnectionMessage?.Invoke("Select a child node. Esc or right-click cancels.");
            return true;
        }

        internal bool CompleteConnection(string childID)
        {
            if (readOnly || data == null || connectionSource == null) return false;
            if (string.IsNullOrEmpty(childID) || !treeNodes.ContainsKey(childID) || childID == connectionSource.id)
            { ConnectionMessage?.Invoke("Choose a different child node."); return false; }
            Undo.RecordObject(data, "Connect behavior nodes");
            try { data.Connect(connectionSource.id, childID); }
            catch (InvalidOperationException error) { ConnectionMessage?.Invoke(error.Message); return false; }
            catch (ArgumentException error) { ConnectionMessage?.Invoke(error.Message); return false; }
            CancelConnection();
            EditorUtility.SetDirty(data);
            Select(childID);
            Changed?.Invoke();
            return true;
        }

        internal void CancelConnection()
        {
            connectionSource?.RemoveFromClassList("bt-connection-source");
            connectionSource = null;
            if (preview != null)
            {
                preview.output = null;
                RemoveElement(preview);
                preview = null;
            }
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (!IsConnecting || readOnly) return;
            if (evt.button == 1) { CancelConnection(); ConnectionMessage?.Invoke("Connection cancelled."); evt.StopPropagation(); return; }
            if (evt.button != 0) return;
            VisualElement target = evt.target as VisualElement;
            TreeNode node = target as TreeNode ?? target?.GetFirstAncestorOfType<TreeNode>();
            if (node != null) CompleteConnection(node.id);
            else { CancelConnection(); ConnectionMessage?.Invoke("Connection cancelled."); }
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) { if (preview != null) preview.candidatePosition = evt.mousePosition; }
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || !IsConnecting) return;
            CancelConnection(); ConnectionMessage?.Invoke("Connection cancelled."); evt.StopPropagation();
        }

        private void UpdateConnections()
        {
            foreach (TreeEdge edge in treeEdges) { edge.UpdateEdgeControl(); edge.UpdateArrow(); }
        }

        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter)
        {
            List<Port> result = new List<Port>();
            if (readOnly) return result;
            foreach (Port port in ports) if (port.direction != start.direction && port.node != start.node) result.Add(port);
            return result;
        }

        private GraphViewChange OnChanged(GraphViewChange change)
        {
            if (rebuilding || data == null) return change;
            if (readOnly) { change.edgesToCreate?.Clear(); change.elementsToRemove?.Clear(); return change; }
            Undo.RecordObject(data, "Edit behavior tree");
            if (change.elementsToRemove != null)
                foreach (GraphElement element in change.elementsToRemove)
                {
                    if (element is TreeNode node && node.id != null) data.RemoveNode(node.id);
                    else if (element is Edge edge) Disconnect(edge);
                }
            if (change.edgesToCreate != null)
                foreach (Edge edge in change.edgesToCreate)
                    try { data.Connect(((TreeNode)edge.output.node).id, ((TreeNode)edge.input.node).id); }
                    catch (Exception error) { Debug.LogWarning(error.Message, data); }
            if (change.movedElements != null)
            {
                List<GraphElement> moved = new List<GraphElement>(change.movedElements);
                BehaviorTreeData movedData = data;
                schedule.Execute(() =>
                {
                    if (readOnly || data == null || data != movedData) return;
                    Undo.RecordObject(data, "Move behavior nodes");
                    bool updated = false;
                    foreach (GraphElement element in moved)
                        if (element is TreeNode node && node.id != null && treeNodes.TryGetValue(node.id, out TreeNode current) && ReferenceEquals(current, node) && data.FindNode(node.id) != null)
                        { data.FindNode(node.id).Position = node.GetPosition().position; updated = true; }
                    if (updated) EditorUtility.SetDirty(data);
                    UpdateConnections();
                }).ExecuteLater(1);
            }
            EditorUtility.SetDirty(data);
            if (change.elementsToRemove != null || change.edgesToCreate != null) schedule.Execute(() => Changed?.Invoke()).ExecuteLater(1);
            return change;
        }

        private void Disconnect(Edge edge)
        {
            string parent = (edge.output.node as TreeNode)?.id;
            string child = (edge.input.node as TreeNode)?.id;
            if (parent == null) { if (data.RootID == child) data.RootID = null; }
            else data.FindNode(parent)?.Children.Remove(child);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (readOnly || data == null) return;
            if (IsConnecting) { evt.menu.AppendAction("Cancel connection", _ => CancelConnection()); return; }
            VisualElement target = evt.target as VisualElement;
            TreeNode node = target as TreeNode ?? target?.GetFirstAncestorOfType<TreeNode>();
            if (node != null && node.output != null)
                evt.menu.AppendAction(node.id == null ? "Set root / select node" : "Connect child / select node", _ => BeginConnection(node.id));
            Vector2 position = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Add node", _ => AddRequested?.Invoke(position));
            base.BuildContextualMenu(evt);
        }

        internal void ShowExecution(BehaviorTreeController controller)
        {
            if (controller == null) return;
            foreach (BehaviorNodeMemory memory in controller.Model.nodes)
            {
                if (!treeNodes.TryGetValue(memory.definition.ID, out TreeNode node)) continue;
                node.title = memory.definition.Name;
                Color? color = GetColor(memory);
                VisualElement border = node.Q("node-border") ?? node;
                StyleColor styleColor = color.HasValue ? new StyleColor(color.Value) : new StyleColor(StyleKeyword.Null);
                StyleFloat width = color.HasValue ? new StyleFloat(3) : new StyleFloat(StyleKeyword.Null);
                border.style.borderTopColor = border.style.borderBottomColor = border.style.borderLeftColor = border.style.borderRightColor = styleColor;
                border.style.borderTopWidth = border.style.borderBottomWidth = border.style.borderLeftWidth = border.style.borderRightWidth = width;
                foreach (TreeEdge edge in treeEdges) if (((string[])edge.userData)[1] == node.id) edge.SetColor(color);
            }
            foreach (BehaviorNodeMemory memory in controller.Model.nodes)
            {
                if (memory.utility == null) continue;
                for (int i = 0; i < memory.children.Length; i++)
                {
                    var child = controller.Model.nodes[memory.children[i]].definition;
                    if (!treeNodes.TryGetValue(child.ID, out TreeNode visual)) continue;
                    visual.title = $"{child.Name} [{memory.utility.scores[i]:0.###}]";
                    visual.tooltip = $"{memory.utility.reasons[i]}" + (memory.utility.selected == i ?
                        (controller.Model.nodes[memory.children[i]].active ? " | EXECUTING" : " | LAST SELECTED") : "");
                }
            }
        }

        private static Color? GetColor(BehaviorNodeMemory node)
        {
            if (node.active) return new Color(1, 0.65f, 0.15f);
            if (node.exitReason == BehaviorExitReason.Aborted || node.exitReason == BehaviorExitReason.Stopped) return new Color(0.8f, 0.4f, 1);
            if (!node.status.HasValue) return null;
            return node.status == BehaviorStatus.Success ? new Color(0.25f, 1, 0.55f) : new Color(1, 0.35f, 0.35f);
        }
    }
}
