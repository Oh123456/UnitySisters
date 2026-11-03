using System;
using System.Collections.Generic;

using UnityEditor.Experimental.GraphView;

using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.CommandTree.Editor
{
    internal sealed class CommandNodeItem
    {
        public string identity;
        public string parentIdentity;
        public readonly string propertyPath;
        public readonly string parentChildrenPath;
        public readonly int index;
        public readonly string keyName;
        public readonly string behaviorName;
        public readonly List<CommandNodeItem> children = new List<CommandNodeItem>();

        public CommandNodeItem(string identity, string parentIdentity, string propertyPath, string parentChildrenPath, int index, string keyName, string behaviorName)
        {
            this.identity = identity;
            this.parentIdentity = parentIdentity;
            this.propertyPath = propertyPath;
            this.parentChildrenPath = parentChildrenPath;
            this.index = index;
            this.keyName = keyName;
            this.behaviorName = behaviorName;
        }
    }

    internal sealed class CommandTreeGraphView : GraphView
    {
        private sealed class CommandTraceEdge : Edge
        {
            private bool highlighted;
            private Color traceColor;

            public CommandTraceEdge() { }

            public void SetTrace(bool value, Color color)
            {
                highlighted = value;
                traceColor = color;
                ApplyTraceStyle();
            }

            public override bool UpdateEdgeControl()
            {
                bool updated = base.UpdateEdgeControl();
                if (updated)
                    ApplyTraceStyle();
                return updated;
            }

            protected override void OnCustomStyleResolved(ICustomStyle styles)
            {
                base.OnCustomStyleResolved(styles);
                ApplyTraceStyle();
            }

            public override void OnSelected()
            {
                base.OnSelected();
                ApplyTraceStyle();
            }

            public override void OnUnselected()
            {
                base.OnUnselected();
                ApplyTraceStyle();
            }

            private void ApplyTraceStyle()
            {
                if (input == null || output == null)
                    return;
                edgeControl.inputColor = highlighted ? traceColor : input.portColor;
                edgeControl.outputColor = highlighted ? traceColor : output.portColor;
                edgeControl.edgeWidth = highlighted ? 4 : 2;
                edgeControl.MarkDirtyRepaint();
            }
        }

        private sealed class CommandGraphNode : Node
        {
            private readonly Action<CommandNodeItem> selected;
            private readonly Label behaviorLabel;

            public readonly CommandNodeItem item;
            public readonly Port inputPort;
            public readonly Port outputPort;

            public CommandGraphNode(CommandNodeItem item, Action<CommandNodeItem> selected)
            {
                this.item = item;
                this.selected = selected;
                title = item.keyName;
                capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Ascendable;
                capabilities &= ~(Capabilities.Copiable | Capabilities.Deletable | Capabilities.Renamable);

                inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
                inputPort.portName = string.Empty;
                inputContainer.Add(inputPort);

                outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                outputPort.portName = string.Empty;
                outputContainer.Add(outputPort);

                behaviorLabel = new Label(item.behaviorName);
                behaviorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                behaviorLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                extensionContainer.Add(behaviorLabel);
                RefreshExpandedState();
                SetPosition(new Rect(Vector2.zero, new Vector2(210.0f, 78.0f)));
            }

            public override void OnSelected()
            {
                base.OnSelected();
                selected?.Invoke(item);
            }

            public void RefreshContent(string keyName, string behaviorName)
            {
                title = keyName;
                behaviorLabel.text = behaviorName;
            }
        }

        private sealed class CommandRootNode : Node
        {
            public readonly Port outputPort;

            public CommandRootNode()
            {
                title = "Root";
                capabilities &= ~(Capabilities.Copiable | Capabilities.Deletable | Capabilities.Movable | Capabilities.Renamable);
                outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                outputPort.portName = string.Empty;
                outputContainer.Add(outputPort);
                inputContainer.style.display = DisplayStyle.None;
                extensionContainer.style.display = DisplayStyle.None;
                RefreshExpandedState();
                SetPosition(new Rect(new Vector2(40.0f, 40.0f), new Vector2(150.0f, 58.0f)));
            }
        }

        private const float horizontalSpacing = 270.0f;
        private const float verticalSpacing = 110.0f;
        private static readonly Vector2 rootPosition = new Vector2(40.0f, 40.0f);
        private static readonly Vector2 rootSize = new Vector2(150.0f, 58.0f);
        private static readonly Vector2 nodeSize = new Vector2(210.0f, 78.0f);
        private readonly Dictionary<string, CommandGraphNode> nodes = new Dictionary<string, CommandGraphNode>();
        private readonly Dictionary<string, Dictionary<string, Vector2>> positionCaches = new Dictionary<string, Dictionary<string, Vector2>>();
        private readonly List<Rect> occupiedPositions = new List<Rect>();
        private readonly List<Edge> edges = new List<Edge>();
        private Dictionary<string, Vector2> nodePositions;
        private string contextKey;
        private CommandRootNode rootNode;
        private bool readOnly;

        public Action AddRootRequested;
        public Action<CommandNodeItem> AddChildRequested;
        public Action<CommandNodeItem> DeleteRequested;
        public Action<CommandNodeItem> NodeSelected;

        public CommandTreeGraphView()
        {
            name = "Command Tree Graph";
            style.flexGrow = 1.0f;

            GridBackground gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            SetupZoom(0.2f, 2.0f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            graphViewChanged = OnGraphViewChanged;
        }

        public void SetTree(string contextKey, IReadOnlyList<CommandNodeItem> rootItems, string selectedPropertyPath, bool frameGraph)
        {
            StoreNodePositions();
            ClearGraph();
            SetContext(contextKey);

            if (rootItems == null)
                return;

            rootNode = new CommandRootNode();
            AddElement(rootNode);
            rootNode.SetPosition(new Rect(rootPosition, rootSize));
            CollectOccupiedPositions(rootItems);

            for (int index = 0; index < rootItems.Count; index++)
                AddNodeRecursive(rootItems[index], rootNode.outputPort, rootPosition);

            if (!string.IsNullOrEmpty(selectedPropertyPath) && nodes.TryGetValue(selectedPropertyPath, out CommandGraphNode selectedNode))
                AddToSelection(selectedNode);

            if (frameGraph)
                schedule.Execute(() => FrameAll()).ExecuteLater(1);

            StoreNodePositions();
        }

        public void SavePositions()
        {
            StoreNodePositions();
        }

        public void SetReadOnly(bool value)
        {
            readOnly = value;
        }

        public void SetTrace(IReadOnlyList<string> paths, string commandPath)
        {
            Color visitedColor = new Color(1.0f, 0.65f, 0.15f);
            Color commandColor = new Color(0.25f, 1.0f, 0.55f);
            foreach (KeyValuePair<string, CommandGraphNode> pair in nodes)
            {
                bool visited = false;
                for (int index = 0; index < paths.Count; index++)
                    if (paths[index] == pair.Key) { visited = true; break; }
                SetOutline(pair.Value, visited, pair.Key == commandPath ? commandColor : visitedColor);
            }
            if (rootNode != null)
                SetOutline(rootNode, paths.Count > 0, visitedColor);

            foreach (Edge edge in edges)
            {
                bool visited = false;
                string path = edge.userData as string;
                for (int index = 0; index < paths.Count; index++)
                    if (paths[index] == path) { visited = true; break; }
                Color color = path == commandPath ? commandColor : visitedColor;
                ((CommandTraceEdge)edge).SetTrace(visited, color);
            }
        }

        private static void SetOutline(Node node, bool highlighted, Color color)
        {
            VisualElement border = node.Q("node-border") ?? node;
            StyleColor borderColor = highlighted ? new StyleColor(color) : new StyleColor(StyleKeyword.Null);
            StyleFloat width = highlighted ? new StyleFloat(3) : new StyleFloat(StyleKeyword.Null);
            border.style.borderTopColor = border.style.borderBottomColor = border.style.borderLeftColor = border.style.borderRightColor = borderColor;
            border.style.borderTopWidth = border.style.borderBottomWidth = border.style.borderLeftWidth = border.style.borderRightWidth = width;
        }

        public void RefreshNode(string propertyPath, string identity, string keyName, string behaviorName)
        {
            if (string.IsNullOrEmpty(propertyPath) || !nodes.TryGetValue(propertyPath, out CommandGraphNode node))
                return;

            ReplaceIdentity(node.item.identity, identity);
            node.RefreshContent(keyName, behaviorName);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent menuEvent)
        {
            if (readOnly)
                return;
            VisualElement target = menuEvent.target as VisualElement;
            CommandGraphNode commandNode = target as CommandGraphNode ?? target?.GetFirstAncestorOfType<CommandGraphNode>();
            if (commandNode != null)
            {
                ClearSelection();
                AddToSelection(commandNode);
                menuEvent.menu.AppendAction("Add Child", _ => AddChildRequested?.Invoke(commandNode.item));
                menuEvent.menu.AppendSeparator();
                menuEvent.menu.AppendAction("Delete", _ => DeleteRequested?.Invoke(commandNode.item));
                return;
            }

            CommandRootNode commandRootNode = target as CommandRootNode ?? target?.GetFirstAncestorOfType<CommandRootNode>();
            if (commandRootNode != null)
            {
                menuEvent.menu.AppendAction("Add Root", _ => AddRootRequested?.Invoke());
                return;
            }

            GraphElement graphElement = target as GraphElement ?? target?.GetFirstAncestorOfType<GraphElement>();
            if (graphElement == null)
                menuEvent.menu.AppendAction("Add Root", _ => AddRootRequested?.Invoke());
        }

        private void AddNodeRecursive(CommandNodeItem item, Port parentPort, Vector2 parentPosition)
        {
            CommandGraphNode node = new CommandGraphNode(item, OnNodeSelected);
            Vector2 position = nodePositions != null && nodePositions.TryGetValue(item.identity, out Vector2 storedPosition) ? storedPosition : FindAvailablePosition(parentPosition);
            nodes.Add(item.propertyPath, node);
            AddElement(node);
            node.SetPosition(new Rect(position, nodeSize));
            AddOccupiedPosition(position);

            Edge edge = parentPort.ConnectTo<CommandTraceEdge>(node.inputPort);
            edge.userData = item.propertyPath;
            edge.capabilities &= ~Capabilities.Deletable;
            edges.Add(edge);
            AddElement(edge);

            for (int index = 0; index < item.children.Count; index++)
                AddNodeRecursive(item.children[index], node.outputPort, position);
        }

        private void OnNodeSelected(CommandNodeItem item)
        {
            NodeSelected?.Invoke(item);
        }

        private void OnKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.Delete && keyEvent.keyCode != KeyCode.Backspace)
                return;
            if (readOnly)
            {
                keyEvent.StopPropagation();
                return;
            }

            foreach (ISelectable selectable in selection)
            {
                if (!(selectable is CommandGraphNode node))
                    continue;

                DeleteRequested?.Invoke(node.item);
                keyEvent.StopPropagation();
                return;
            }
        }

        private void StoreNodePositions()
        {
            if (nodePositions == null)
                return;

            Dictionary<string, Vector2> currentPositions = new Dictionary<string, Vector2>();
            foreach (KeyValuePair<string, CommandGraphNode> pair in nodes)
                currentPositions[pair.Value.item.identity] = new Vector2(pair.Value.style.left.value.value, pair.Value.style.top.value.value);

            nodePositions.Clear();
            foreach (KeyValuePair<string, Vector2> pair in currentPositions)
                nodePositions.Add(pair.Key, pair.Value);
            if (IsPersistentContext(contextKey))
                CommandTreeEditorState.instance.SetPositions(contextKey, nodePositions);
        }

        private void SetContext(string contextKey)
        {
            this.contextKey = contextKey;
            if (string.IsNullOrEmpty(contextKey))
            {
                nodePositions = null;
                return;
            }

            if (!positionCaches.TryGetValue(contextKey, out nodePositions))
            {
                nodePositions = IsPersistentContext(contextKey) ? CommandTreeEditorState.instance.GetPositions(contextKey) : new Dictionary<string, Vector2>();
                positionCaches.Add(contextKey, nodePositions);
            }
        }

        private bool IsPersistentContext(string contextKey)
        {
            return !string.IsNullOrEmpty(contextKey) && !contextKey.StartsWith("instance:", StringComparison.Ordinal);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.movedElements != null && change.movedElements.Count > 0)
                schedule.Execute(StoreNodePositions).ExecuteLater(1);

            return change;
        }

        private void CollectOccupiedPositions(IReadOnlyList<CommandNodeItem> items)
        {
            occupiedPositions.Clear();
            if (nodePositions == null)
                return;

            for (int index = 0; index < items.Count; index++)
                CollectOccupiedPositions(items[index]);
        }

        private void CollectOccupiedPositions(CommandNodeItem item)
        {
            if (nodePositions.TryGetValue(item.identity, out Vector2 position))
                AddOccupiedPosition(position);

            for (int index = 0; index < item.children.Count; index++)
                CollectOccupiedPositions(item.children[index]);
        }

        private Vector2 FindAvailablePosition(Vector2 parentPosition)
        {
            Vector2 preferredPosition = new Vector2(parentPosition.x + horizontalSpacing, parentPosition.y);
            for (int offsetIndex = 0; offsetIndex < 1000; offsetIndex++)
            {
                int direction = offsetIndex % 2 == 0 ? 1 : -1;
                int distance = (offsetIndex + 1) / 2;
                Vector2 candidate = preferredPosition + Vector2.up * verticalSpacing * distance * direction;
                if (candidate.y < 20.0f || IsOccupied(candidate))
                    continue;

                return candidate;
            }

            return preferredPosition;
        }

        private bool IsOccupied(Vector2 position)
        {
            Rect candidate = new Rect(position - new Vector2(10.0f, 10.0f), nodeSize + new Vector2(20.0f, 20.0f));
            for (int index = 0; index < occupiedPositions.Count; index++)
            {
                if (occupiedPositions[index].Overlaps(candidate))
                    return true;
            }

            return false;
        }

        private void AddOccupiedPosition(Vector2 position)
        {
            occupiedPositions.Add(new Rect(position, nodeSize));
        }

        private void ReplaceIdentity(string previousIdentity, string identity)
        {
            if (string.IsNullOrEmpty(previousIdentity) || string.IsNullOrEmpty(identity) || previousIdentity == identity)
                return;

            string childPrefix = previousIdentity + "/";
            foreach (CommandGraphNode node in nodes.Values)
            {
                if (node.item.identity == previousIdentity)
                    node.item.identity = identity;
                else if (node.item.identity.StartsWith(childPrefix, StringComparison.Ordinal))
                {
                    node.item.identity = identity + node.item.identity.Substring(previousIdentity.Length);
                    if (node.item.parentIdentity == previousIdentity)
                        node.item.parentIdentity = identity;
                    else if (node.item.parentIdentity.StartsWith(childPrefix, StringComparison.Ordinal))
                        node.item.parentIdentity = identity + node.item.parentIdentity.Substring(previousIdentity.Length);
                }
            }
        }

        private void ClearGraph()
        {
            ClearSelection();
            for (int index = 0; index < edges.Count; index++)
                RemoveElement(edges[index]);
            foreach (CommandGraphNode node in nodes.Values)
                RemoveElement(node);
            if (rootNode != null)
                RemoveElement(rootNode);

            edges.Clear();
            nodes.Clear();
            occupiedPositions.Clear();
            rootNode = null;
        }
    }
}
