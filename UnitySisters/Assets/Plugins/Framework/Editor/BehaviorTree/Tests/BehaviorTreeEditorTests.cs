using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityFramework.BehaviorTree.Editor;

namespace UnityFramework.BehaviorTree.Tests
{
    internal sealed class BehaviorTreeEditorTests
    {
        private const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private BlackboardData board;
        private BehaviorTreeData data;
        private BehaviorTreeEditorWindow window;

        [SetUp]
        public void SetUp()
        {
            Undo.IncrementCurrentGroup();
            board = ScriptableObject.CreateInstance<BlackboardData>(); data = ScriptableObject.CreateInstance<BehaviorTreeData>();
            using (SerializedObject serialized = new SerializedObject(data)) { serialized.FindProperty("blackboard").objectReferenceValue = board; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            window = ScriptableObject.CreateInstance<BehaviorTreeEditorWindow>(); window.CreateGUI();
            typeof(BehaviorTreeEditorWindow).GetMethod("SetData", flags).Invoke(window, new object[] { data });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(board);
        }

        [Test]
        public void AddingNodesAvoidsOverlapAndUndoRestoresTree()
        {
            MethodInfo add = typeof(BehaviorTreeEditorWindow).GetMethod("AddNode", flags);
            add.Invoke(window, new object[] { new WaitTaskData(), new Vector2(300, 180) }); Undo.IncrementCurrentGroup();
            add.Invoke(window, new object[] { new WaitTaskData(), new Vector2(300, 180) });
            Assert.That(data.Nodes.Count, Is.EqualTo(2)); Assert.That(data.Nodes[0].Position, Is.Not.EqualTo(data.Nodes[1].Position));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.That(data.Nodes.Count, Is.EqualTo(1));
            Undo.PerformRedo(); Assert.That(data.Nodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlayObservationGraphDoesNotOfferConnectionOrMutationMenus()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView(); var root = data.AddNode(new SequenceNode(), Vector2.zero); var task = data.AddNode(new WaitTaskData(), Vector2.one); data.Connect(null, root.ID); data.Connect(root.ID, task.ID);
            graph.ShowTree(data, true, false);
            Assert.That(graph.GetCompatiblePorts(null, null), Is.Empty);
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled()) using (var context = ContextualMenuPopulateEvent.GetPooled(trigger, menu, graph, null)) graph.BuildContextualMenu(context);
            Assert.That(menu.MenuItems(), Is.Empty);
        }

        [Test]
        public void EditGraphOffersCompatiblePortsAndNodeCreationMenu()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView(); data.AddNode(new WaitTaskData(), new Vector2(300, 180)); graph.ShowTree(data, false, false);
            UnityEditor.Experimental.GraphView.Port output = null;
            foreach (var port in graph.ports) if (port.direction == UnityEditor.Experimental.GraphView.Direction.Output) { output = port; break; }
            Assert.That(graph.GetCompatiblePorts(output, null).Count, Is.EqualTo(1));
            var menu = new DropdownMenu(); using (var trigger = MouseDownEvent.GetPooled()) using (var context = ContextualMenuPopulateEvent.GetPooled(trigger, menu, graph, null)) graph.BuildContextualMenu(context);
            Assert.That(menu.MenuItems().Count, Is.GreaterThan(0));
        }

        [Test]
        public void TaskPaletteExcludesEditorOnlyTestTypes()
        {
            MethodInfo canCreate = typeof(BehaviorTreeEditorWindow).GetMethod("CanCreate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(canCreate.Invoke(null, new object[] { typeof(WaitTaskData) }), Is.True);
            Assert.That(canCreate.Invoke(null, new object[] { typeof(EditorOnlyTask) }), Is.False);
        }

        [TestCase(300, 0, 100, 25, 300, 25)]
        [TestCase(0, 300, 50, 50, 50, 300)]
        [TestCase(300, 300, 75, 50, 325, 300)]
        [TestCase(-300, 0, 0, 25, -200, 25)]
        public void StraightConnectionsMeetNodeBorders(float x, float y, float startX, float startY, float endX, float endY)
        {
            bool visible = BehaviorTreeGraphView.TryGetConnectionGeometry(new Rect(0, 0, 100, 50), new Rect(x, y, 100, 50), out Vector2 start, out Vector2 end);
            Assert.That(visible, Is.True);
            Assert.That(Vector2.Distance(start, new Vector2(startX, startY)), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(end, new Vector2(endX, endY)), Is.LessThan(0.001f));
        }

        [Test]
        public void OverlappingNodesHaveNoMisleadingArrow()
        {
            Rect source = new Rect(0, 0, 100, 50);
            Assert.That(BehaviorTreeGraphView.TryGetConnectionGeometry(source, new Rect(20, 0, 100, 50), out Vector2 start, out Vector2 end), Is.False);
            Assert.That(start, Is.EqualTo(end));
            Assert.That(BehaviorTreeGraphView.TryGetConnectionGeometry(source, source, out start, out end), Is.False);
            Assert.That(float.IsNaN(start.x) || float.IsNaN(start.y), Is.False);
        }

        [Test]
        public void NodeSelectionConnectsRootAndChildrenWithoutPortDragging()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView();
            var parent = data.AddNode(new SelectorNode(), new Vector2(200, 150));
            var child = data.AddNode(new WaitTaskData(), new Vector2(200, 400));
            graph.ShowTree(data, false, false);
            Assert.That(graph.BeginConnection(null), Is.True);
            Assert.That(graph.CompleteConnection(parent.ID), Is.True);
            Assert.That(data.RootID, Is.EqualTo(parent.ID));
            Assert.That(graph.IsConnecting, Is.False);
            graph.ShowTree(data, false, false);
            Assert.That(graph.BeginConnection(parent.ID), Is.True);
            Assert.That(graph.CompleteConnection(child.ID), Is.True);
            Assert.That(parent.Children, Is.EqualTo(new[] { child.ID }));
            Assert.That(graph.IsConnecting, Is.False);
            Assert.That(graph.BeginConnection(child.ID), Is.False);
            foreach (var port in graph.ports)
            {
                Assert.That(port.pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(port.ClassListContains("bt-hidden-port"), Is.True);
            }
        }

        [Test]
        public void InvalidNodeConnectionsKeepTreeUnchanged()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView();
            var parent = data.AddNode(new SelectorNode(), Vector2.zero);
            var branch = data.AddNode(new SequenceNode(), new Vector2(200, 200));
            var child = data.AddNode(new WaitTaskData(), new Vector2(200, 400));
            data.Connect(null, parent.ID); data.Connect(parent.ID, branch.ID); data.Connect(branch.ID, child.ID);
            graph.ShowTree(data, false, false);
            string before = EditorJsonUtility.ToJson(data);
            Assert.That(graph.BeginConnection(parent.ID), Is.True);
            Assert.That(graph.CompleteConnection(parent.ID), Is.False);
            Assert.That(graph.CompleteConnection(child.ID), Is.False);
            graph.CancelConnection();
            Assert.That(graph.BeginConnection(branch.ID), Is.True);
            Assert.That(graph.CompleteConnection(parent.ID), Is.False);
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before));
            graph.CancelConnection();
        }

        [Test]
        public void EscapeAndRightClickCancelWithoutChangingData()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView();
            var parent = data.AddNode(new SelectorNode(), Vector2.zero);
            graph.ShowTree(data, false, false);
            string before = EditorJsonUtility.ToJson(data);
            graph.BeginConnection(parent.ID);
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                typeof(BehaviorTreeGraphView).GetMethod("OnKeyDown", flags).Invoke(graph, new object[] { key });
            Assert.That(graph.IsConnecting, Is.False);
            graph.BeginConnection(parent.ID);
            using (var mouse = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1 }))
                typeof(BehaviorTreeGraphView).GetMethod("OnMouseDown", flags).Invoke(graph, new object[] { mouse });
            Assert.That(graph.IsConnecting, Is.False);
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [Test]
        public void SwitchingToObservationCancelsPendingConnection()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView();
            var parent = data.AddNode(new SelectorNode(), Vector2.zero);
            var child = data.AddNode(new WaitTaskData(), Vector2.one);
            graph.ShowTree(data, false, false);
            graph.BeginConnection(parent.ID);
            string before = EditorJsonUtility.ToJson(data);
            graph.ShowTree(data, true, false);
            Assert.That(graph.IsConnecting, Is.False);
            Assert.That(graph.BeginConnection(parent.ID), Is.False);
            Assert.That(graph.CompleteConnection(child.ID), Is.False);
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [Test]
        public void ConnectionUndoAndRedoRestoreLinks()
        {
            BehaviorTreeGraphView graph = new BehaviorTreeGraphView();
            var parent = data.AddNode(new SelectorNode(), Vector2.zero);
            var child = data.AddNode(new WaitTaskData(), Vector2.one);
            data.Connect(null, parent.ID);
            graph.ShowTree(data, false, false);
            graph.BeginConnection(parent.ID); graph.CompleteConnection(child.ID);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(data.FindNode(parent.ID).Children, Is.Empty);
            Undo.PerformRedo();
            Assert.That(data.FindNode(parent.ID).Children, Is.EqualTo(new[] { child.ID }));
        }

        [Serializable]
        private sealed class EditorOnlyTask : TaskNodeData<EditorOnlyAction>
        {
        }

        private sealed class EditorOnlyAction : TaskNode
        {
            public EditorOnlyAction() { }
            protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory) { return BehaviorStatus.Success; }
        }
    }
}
