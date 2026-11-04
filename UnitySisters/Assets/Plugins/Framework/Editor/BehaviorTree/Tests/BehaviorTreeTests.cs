using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityFramework.BehaviorTree.Tests
{
    internal sealed class BehaviorTreeTests
    {
        private sealed class Tracker
        {
            internal readonly List<string> events = new List<string>();
            internal BehaviorTreeController controller;
        }

        private sealed class Owner : IBehaviorTreeOwner
        {
            internal readonly Tracker tracker = new Tracker();
            T IBehaviorTreeOwner.GetInterface<T>() { return tracker as T; }
        }

        [Serializable]
        private sealed class ProbeTaskData : TaskNodeData<ProbeTask>
        {
            internal string label = "task";
            internal BehaviorStatus result = BehaviorStatus.Running;
            internal int ticksToComplete;
            internal bool throwOnTick;
            internal bool stopOnEnter;
        }

        private sealed class ProbeTask : TaskNode
        {
            public ProbeTask() { }
            private sealed class Memory { internal int ticks; }
            protected override object CreateMemory() { return new Memory(); }
            protected override void OnEnter(BehaviorTreeContext context, TaskNodeData data, object memory)
            {
                var settings = (ProbeTaskData)data;
                ((Memory)memory).ticks = 0;
                Tracker tracker = context.GetInterface<Tracker>();
                tracker.events.Add(settings.label + ":enter");
                if (settings.stopOnEnter) tracker.controller.Stop();
            }
            protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
            {
                var settings = (ProbeTaskData)data;
                context.GetInterface<Tracker>().events.Add(settings.label + ":tick");
                if (settings.throwOnTick) throw new InvalidOperationException("Task failure for cleanup test");
                if (settings.ticksToComplete > 0) return ++((Memory)memory).ticks >= settings.ticksToComplete ? BehaviorStatus.Success : BehaviorStatus.Running;
                return settings.result;
            }
            protected override void OnExit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason) { context.GetInterface<Tracker>().events.Add(((ProbeTaskData)data).label + ":" + reason); }
        }

        [Serializable]
        private sealed class ProbeService : Service
        {
            protected override void OnEnter(BehaviorTreeContext context, object memory) { context.GetInterface<Tracker>().events.Add("service:enter"); }
            protected override void OnTick(BehaviorTreeContext context, object memory) { context.GetInterface<Tracker>().events.Add("service:tick"); }
            protected override void OnExit(BehaviorTreeContext context, object memory, BehaviorExitReason reason) { context.GetInterface<Tracker>().events.Add("service:" + reason); }
        }

        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private readonly List<BehaviorTreeController> controllers = new List<BehaviorTreeController>();
        private BlackboardData schema;
        private BlackboardKeyReference<bool> flag;

        [SetUp]
        public void SetUp()
        {
            schema = ScriptableObject.CreateInstance<BlackboardData>(); objects.Add(schema);
            AddKey("Flag", BlackboardKeyType.Bool);
            AddKey("Other", BlackboardKeyType.Int);
            flag = schema.GetKey<bool>("Flag");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (BehaviorTreeController controller in controllers) controller.Stop();
            controllers.Clear();
            foreach (UnityEngine.Object asset in objects) UnityEngine.Object.DestroyImmediate(asset);
            objects.Clear();
        }

        private BlackboardKeyData AddKey(string name, BlackboardKeyType type)
        {
            BlackboardKeyData key = new BlackboardKeyData { ID = Guid.NewGuid().ToString("N"), Name = name, Type = type };
            ((List<BlackboardKeyData>)schema.Keys).Add(key);
            return key;
        }

        [Test]
        public void TaskManagerSharesBehaviorAcrossNodesTreesAndControllers()
        {
            var first = NewTree();
            var root = Root(first, new SequenceNode());
            Child(first, root, new ProbeTaskData());
            Child(first, root, new ProbeTaskData());
            var second = NewTree();
            Root(second, new ProbeTaskData());
            var a = new BehaviorTreeModel(first, new Owner());
            var b = new BehaviorTreeModel(first, new Owner());
            var c = new BehaviorTreeModel(second, new Owner());
            Assert.That(a.nodes[1].task, Is.SameAs(a.nodes[2].task));
            Assert.That(a.nodes[1].task, Is.SameAs(b.nodes[1].task));
            Assert.That(a.nodes[1].task, Is.SameAs(c.nodes[0].task));
            Assert.That(a.nodes[1].taskMemory, Is.Not.SameAs(a.nodes[2].taskMemory));
            Assert.That(a.nodes[1].taskMemory, Is.Not.SameAs(b.nodes[1].taskMemory));
        }

        [Test]
        public void SharedTaskUsesEachNodesSettingsAndIndependentProgress()
        {
            var first = NewTree();
            Root(first, new ProbeTaskData { label = "first", ticksToComplete = 2 });
            var second = NewTree();
            Root(second, new ProbeTaskData { label = "second", ticksToComplete = 3 });
            var ownerA = new Owner();
            var ownerB = new Owner();
            var a = Run(first, ownerA);
            var b = Run(second, ownerB);
            a.Tick(0.1f); b.Tick(0.1f);
            a.Tick(0.1f); b.Tick(0.1f);
            Assert.That(a.LastStatus, Is.EqualTo(BehaviorStatus.Success));
            Assert.That(b.LastStatus, Is.EqualTo(BehaviorStatus.Running));
            b.Tick(0.1f);
            Assert.That(b.LastStatus, Is.EqualTo(BehaviorStatus.Success));
            Assert.That(ownerA.tracker.events, Does.Not.Contain("second:tick"));
            Assert.That(ownerB.tracker.events, Does.Not.Contain("first:tick"));
        }

        [Test]
        public void MissingTaskIsRejectedBeforeControllerCanExecute()
        {
            var data = NewTree();
            Root(data, new ProbeTaskData());
            var shared = TaskManager.GetTask<ProbeTask>();
            var table = (Dictionary<Type, TaskNode>)typeof(TaskManager)
                .GetField("tasks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            try
            {
                table[typeof(ProbeTask)] = null;
                var error = Assert.Throws<InvalidOperationException>(() => data.CreateController(null));
                Assert.That(error.Message, Does.Contain("Task behavior is missing"));
            }
            finally
            {
                table[typeof(ProbeTask)] = shared;
            }
        }

        private BehaviorTreeData NewTree()
        {
            BehaviorTreeData data = ScriptableObject.CreateInstance<BehaviorTreeData>(); objects.Add(data);
            using (SerializedObject serialized = new SerializedObject(data)) { serialized.FindProperty("blackboard").objectReferenceValue = schema; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            return data;
        }

        private BehaviorNodeData Root(BehaviorTreeData data, BehaviorNode behavior)
        {
            BehaviorNodeData node = data.AddNode(behavior, Vector2.zero); data.Connect(null, node.ID); return node;
        }

        private BehaviorNodeData Child(BehaviorTreeData data, BehaviorNodeData parent, BehaviorNode behavior)
        {
            BehaviorNodeData node = data.AddNode(behavior, Vector2.one); data.Connect(parent.ID, node.ID); return node;
        }

        private BehaviorTreeController Run(BehaviorTreeData data, Owner owner = null)
        {
            owner = owner ?? new Owner();
            BehaviorTreeController controller = data.CreateController(owner); owner.tracker.controller = controller;
            controllers.Add(controller); controller.Start(); return controller;
        }

        private BlackboardDecorator Guard(BehaviorNodeData node, BehaviorAbortMode mode)
        {
            BlackboardDecorator condition = new BlackboardDecorator { KeyID = flag.ID };
            condition.Expected.BoolValue = true;
            typeof(Decorator).GetField("abortMode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(condition, mode);
            ((List<Decorator>)node.Decorators).Add(condition);
            return condition;
        }

        [Test]
        public void BlackboardContractsAreExplicitAndTypeSafe()
        {
            BehaviorTreeData data = NewTree(); Root(data, new WaitTaskData()); var controller = Run(data);
            int changed = 0; controller.Blackboard.KeyChanged += _ => changed++;
            controller.BlackboardWriter.SetValue(flag, true); controller.BlackboardWriter.SetValue(flag, true);
            Assert.That(controller.Blackboard.GetValue(flag), Is.True); Assert.That(changed, Is.EqualTo(1));
            var wrong = new BlackboardKeyReference<int>(flag.ID);
            Assert.That(controller.Blackboard.TryGetValue(wrong, out int ignored), Is.False);
            Assert.Throws<ArgumentException>(() => controller.Blackboard.GetValue(wrong));
            Assert.Throws<ArgumentException>(() => controller.BlackboardWriter.SetValue(wrong, 3));
            Assert.Throws<ArgumentException>(() => controller.BlackboardWriter.ResetValue(wrong));
            controller.BlackboardWriter.ResetValue(flag); Assert.That(controller.Blackboard.GetValue(flag), Is.False);
            Assert.That(typeof(Blackboard).IsNotPublic, Is.True);
            Assert.That(typeof(Blackboard).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void BlackboardSupportsAllConfiguredValueTypes()
        {
            AddKey("Number", BlackboardKeyType.Float).DefaultValue.FloatValue = 1.5f;
            AddKey("Position", BlackboardKeyType.Vector3).DefaultValue.VectorValue = Vector3.up;
            AddKey("Target", BlackboardKeyType.Object);
            BehaviorTreeData data = NewTree(); Root(data, new WaitTaskData()); var controller = Run(data);
            Assert.That(controller.Blackboard.GetValue(schema.GetKey<float>("Number")), Is.EqualTo(1.5f));
            Assert.That(controller.Blackboard.GetValue(schema.GetKey<Vector3>("Position")), Is.EqualTo(Vector3.up));
            Assert.That(controller.Blackboard.GetValue(schema.GetKey<UnityEngine.Object>("Target")), Is.Null);
            Assert.Throws<ArgumentException>(() => schema.GetKey<int>("Number"));
        }

        [Test]
        public void SharedTreeHasIndependentBlackboardsAndTaskMemory()
        {
            BehaviorTreeData data = NewTree(); Root(data, new ProbeTaskData { ticksToComplete = 2 });
            var first = Run(data); var second = Run(data);
            first.BlackboardWriter.SetValue(flag, true);
            Assert.That(second.Blackboard.GetValue(flag), Is.False);
            first.Tick(0.1f); first.Tick(0.1f); second.Tick(0.1f);
            Assert.That(first.LastStatus, Is.EqualTo(BehaviorStatus.Success)); Assert.That(second.LastStatus, Is.EqualTo(BehaviorStatus.Running));
        }

        [Test]
        public void RenamingKeyDoesNotInvalidateStoredReference()
        {
            ((List<BlackboardKeyData>)schema.Keys)[0].Name = "Renamed";
            BehaviorTreeData data = NewTree(); Root(data, new WaitTaskData()); var controller = Run(data);
            controller.BlackboardWriter.SetValue(flag, true);
            Assert.That(controller.Blackboard.GetValue(schema.GetKey<bool>("Renamed")), Is.True);
        }

        [Test]
        public void SequenceResumesRunningChildWithoutReenteringEarlierTasks()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode());
            Child(data, root, new ProbeTaskData { label = "first", result = BehaviorStatus.Success });
            Child(data, root, new ProbeTaskData { label = "second", ticksToComplete = 2 });
            Owner owner = new Owner(); var controller = Run(data, owner); controller.Tick(0.1f); controller.Tick(0.1f);
            Assert.That(owner.tracker.events.FindAll(x => x == "first:enter").Count, Is.EqualTo(1));
            Assert.That(owner.tracker.events.FindAll(x => x == "second:enter").Count, Is.EqualTo(1));
            Assert.That(controller.LastStatus, Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void SequenceFailureDoesNotRunLaterChildren()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode());
            Child(data, root, new ProbeTaskData { label = "first", result = BehaviorStatus.Failure }); Child(data, root, new ProbeTaskData { label = "second" });
            Owner owner = new Owner(); var controller = Run(data, owner); controller.Tick(0.1f);
            Assert.That(owner.tracker.events, Does.Not.Contain("second:enter")); Assert.That(controller.LastStatus, Is.EqualTo(BehaviorStatus.Failure));
        }

        [Test]
        public void SelectorTriesChildrenInExplicitOrder()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SelectorNode());
            var first = Child(data, root, new ProbeTaskData { label = "first", result = BehaviorStatus.Failure });
            var second = Child(data, root, new ProbeTaskData { label = "second", result = BehaviorStatus.Success });
            first.Position = new Vector2(900, 0); second.Position = Vector2.zero;
            Owner owner = new Owner(); var controller = Run(data, owner); controller.Tick(0.1f);
            Assert.That(owner.tracker.events.IndexOf("first:enter"), Is.LessThan(owner.tracker.events.IndexOf("second:enter")));
            Assert.That(controller.LastStatus, Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void StopExitsTaskExactlyOnceAndRestartEntersAgain()
        {
            BehaviorTreeData data = NewTree(); Root(data, new ProbeTaskData()); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.Tick(0.1f); controller.Stop(); controller.Stop();
            Assert.That(owner.tracker.events.FindAll(x => x == "task:Stopped").Count, Is.EqualTo(1));
            controller.Start(); controller.Tick(0.1f);
            Assert.That(owner.tracker.events.FindAll(x => x == "task:enter").Count, Is.EqualTo(2));
        }

        [Test]
        public void StopDuringEnterPreventsTaskTick()
        {
            BehaviorTreeData data = NewTree(); Root(data, new ProbeTaskData { stopOnEnter = true }); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.Tick(0.1f); Assert.That(controller.IsRunning, Is.False); Assert.That(owner.tracker.events, Does.Not.Contain("task:tick"));
            Assert.That(owner.tracker.events, Does.Contain("task:Stopped"));
        }

        [Test]
        public void ExceptionStopsTreeAndCleansTaskAndServices()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode()); ((List<Service>)root.Services).Add(new ProbeService());
            Child(data, root, new ProbeTaskData { throwOnTick = true }); Owner owner = new Owner(); var controller = Run(data, owner);
            Assert.Throws<InvalidOperationException>(() => controller.Tick(0.1f));
            Assert.That(controller.IsRunning, Is.False); Assert.That(owner.tracker.events, Does.Contain("task:Stopped")); Assert.That(owner.tracker.events, Does.Contain("service:Stopped"));
        }

        [Test]
        public void ServiceTicksAtIntervalOnlyWhileBranchIsActive()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode()); ((List<Service>)root.Services).Add(new ProbeService()); Child(data, root, new ProbeTaskData());
            Owner owner = new Owner(); var controller = Run(data, owner);
            controller.Tick(0.1f); controller.Tick(0.1f); Assert.That(owner.tracker.events, Does.Not.Contain("service:tick"));
            controller.Tick(0.1f); Assert.That(owner.tracker.events.FindAll(x => x == "service:tick").Count, Is.EqualTo(1));
            controller.Stop(); controller.Tick(10); Assert.That(owner.tracker.events.FindAll(x => x == "service:tick").Count, Is.EqualTo(1));
        }

        [TestCase(BehaviorAbortMode.Self)]
        [TestCase(BehaviorAbortMode.Both)]
        public void SelfObserverAbortsRunningBranch(BehaviorAbortMode mode)
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SelectorNode()); var first = Child(data, root, new ProbeTaskData { label = "first" }); Guard(first, mode);
            Child(data, root, new ProbeTaskData { label = "fallback" }); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f); controller.BlackboardWriter.SetValue(flag, false); controller.Tick(0.1f);
            Assert.That(owner.tracker.events, Does.Contain("first:Aborted")); Assert.That(owner.tracker.events, Does.Contain("fallback:enter"));
        }

        [Test]
        public void NoneObserverKeepsRunningTaskWhenConditionChanges()
        {
            BehaviorTreeData data = NewTree(); var first = Root(data, new ProbeTaskData()); Guard(first, BehaviorAbortMode.None);
            Owner owner = new Owner(); var controller = Run(data, owner); controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f);
            controller.BlackboardWriter.SetValue(flag, false); controller.Tick(0.1f);
            Assert.That(controller.LastStatus, Is.EqualTo(BehaviorStatus.Running)); Assert.That(owner.tracker.events, Does.Not.Contain("task:Aborted"));
        }

        [Test]
        public void LowerPriorityObserverPreemptsFallbackWhenConditionBecomesTrue()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SelectorNode()); var high = Child(data, root, new ProbeTaskData { label = "high" }); Guard(high, BehaviorAbortMode.LowerPriority);
            Child(data, root, new ProbeTaskData { label = "low" }); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.Tick(0.1f); controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f);
            Assert.That(owner.tracker.events, Does.Contain("low:Aborted")); Assert.That(owner.tracker.events, Does.Contain("high:enter"));
        }

        [Test]
        public void UnchangedTrueConditionDoesNotRepeatedlyPreemptAfterTaskFailure()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SelectorNode()); var high = Child(data, root, new ProbeTaskData { label = "high", result = BehaviorStatus.Failure }); Guard(high, BehaviorAbortMode.LowerPriority);
            Child(data, root, new ProbeTaskData { label = "low" }); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f);
            controller.BlackboardWriter.SetValue(schema.GetKey<int>("Other"), 1); controller.Tick(0.1f);
            Assert.That(owner.tracker.events, Does.Not.Contain("low:Aborted")); Assert.That(owner.tracker.events.FindAll(x => x == "high:enter").Count, Is.EqualTo(1));
        }

        [Test]
        public void GraphRejectsCyclesAndMultipleParents()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode()); var branch = Child(data, root, new SelectorNode()); var leaf = Child(data, branch, new WaitTaskData());
            Assert.Throws<InvalidOperationException>(() => data.Connect(root.ID, leaf.ID));
            Assert.Throws<InvalidOperationException>(() => data.Connect(branch.ID, root.ID));
            branch.Children.Add(root.ID); Assert.That(data.Validate(), Is.Not.Empty);
        }

        [Test]
        public void InvalidOrUnconnectedTreeCannotStart()
        {
            BehaviorTreeData data = NewTree(); Assert.Throws<InvalidOperationException>(() => data.CreateController(null));
            Root(data, new WaitTaskData()); data.AddNode(new WaitTaskData(), Vector2.zero); Assert.Throws<InvalidOperationException>(() => data.CreateController(null));
        }

        [Test]
        public void MissingBlackboardKeyIsRejectedBeforeExecution()
        {
            BehaviorTreeData data = NewTree(); var node = Root(data, new WaitTaskData()); Guard(node, BehaviorAbortMode.Self);
            ((List<BlackboardKeyData>)schema.Keys).Clear(); Assert.Throws<InvalidOperationException>(() => data.CreateController(null));
        }

        [Test]
        public void RuntimeExecutionDoesNotModifySerializedAssets()
        {
            BehaviorTreeData data = NewTree(); Root(data, new WaitTaskData()); string treeBefore = EditorJsonUtility.ToJson(data); string boardBefore = EditorJsonUtility.ToJson(schema);
            var controller = Run(data); controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f); controller.Stop();
            Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(treeBefore)); Assert.That(EditorJsonUtility.ToJson(schema), Is.EqualTo(boardBefore));
        }

        [Test]
        public void SteadyRunningWaitDoesNotAllocatePerTick()
        {
            BehaviorTreeData data = NewTree(); Root(data, new WaitTaskData()); var controller = Run(data); controller.Tick(0.0001f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 100; index++) controller.Tick(0.0001f);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void SetValueTaskUpdatesBlackboardAndCompletes()
        {
            BehaviorTreeData data = NewTree(); var task = new SetBlackboardValueTaskData { KeyID = flag.ID }; task.Value.BoolValue = true; Root(data, task);
            var controller = Run(data); controller.Tick(0.1f); Assert.That(controller.Blackboard.GetValue(flag), Is.True); Assert.That(controller.LastStatus, Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void NestedLowerPriorityConditionCanReactivateItsSequence()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SelectorNode()); var branch = Child(data, root, new SequenceNode());
            Child(data, branch, new ProbeTaskData { label = "setup", result = BehaviorStatus.Success });
            var task = Child(data, branch, new ProbeTaskData { label = "high" }); Guard(task, BehaviorAbortMode.LowerPriority);
            Child(data, root, new ProbeTaskData { label = "low" }); Owner owner = new Owner(); var controller = Run(data, owner);
            controller.Tick(0.1f); controller.BlackboardWriter.SetValue(flag, true); controller.Tick(0.1f);
            Assert.That(owner.tracker.events, Does.Contain("low:Aborted")); Assert.That(owner.tracker.events, Does.Contain("high:enter"));
        }

        [Test]
        public void BuiltinServiceWritesOnlyAfterItsInterval()
        {
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode());
            var service = new SetBlackboardValueService { KeyID = flag.ID }; service.Value.BoolValue = true; ((List<Service>)root.Services).Add(service); Child(data, root, new WaitTaskData());
            var controller = Run(data); controller.Tick(0.1f); controller.Tick(0.1f); Assert.That(controller.Blackboard.GetValue(flag), Is.False);
            controller.Tick(0.1f); Assert.That(controller.Blackboard.GetValue(flag), Is.True);
        }

        [Test]
        public void SerializedAssetsRoundTripKeepsNodeAndKeyIdentities()
        {
            string prefix = "Assets/__BTTest_" + Guid.NewGuid().ToString("N");
            BehaviorTreeData data = NewTree(); var root = Root(data, new SequenceNode());
            var setter = new SetBlackboardValueTaskData { KeyID = flag.ID }; setter.Value.BoolValue = true;
            var child = Child(data, root, setter);
            string rootID = root.ID; string childID = child.ID;
            try
            {
                AssetDatabase.CreateAsset(schema, prefix + "_BB.asset"); AssetDatabase.CreateAsset(data, prefix + "_BT.asset");
                AssetDatabase.SaveAssetIfDirty(schema); AssetDatabase.SaveAssetIfDirty(data);
                Resources.UnloadAsset(data);
                var loaded = AssetDatabase.LoadAssetAtPath<BehaviorTreeData>(prefix + "_BT.asset");
                Assert.That(loaded.RootID, Is.EqualTo(rootID)); Assert.That(loaded.FindNode(childID).Behavior, Is.TypeOf<SetBlackboardValueTaskData>());
                var controller = Run(loaded); controller.Tick(0.1f); Assert.That(controller.Blackboard.GetValue(flag), Is.True); controller.Stop();
            }
            finally { AssetDatabase.DeleteAsset(prefix + "_BT.asset"); AssetDatabase.DeleteAsset(prefix + "_BB.asset"); }
        }
    }
}
