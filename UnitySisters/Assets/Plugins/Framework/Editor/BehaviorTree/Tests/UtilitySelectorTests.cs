using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityFramework.BehaviorTree.Tests
{
    internal sealed class UtilitySelectorTests
    {
        [Serializable]
        private sealed class ActionData : TaskNodeData<ActionTask>
        {
            internal BehaviorStatus result = BehaviorStatus.Running;
        }
        private sealed class ActionTask : TaskNode
        {
            public ActionTask() { }
            protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory) { return ((ActionData)data).result; }
        }
        private BlackboardData board;
        private readonly List<BehaviorTreeData> trees = new();
        private readonly List<BehaviorTreeController> controllers = new();
        private BlackboardKeyReference<float> input;
        private BlackboardKeyReference<bool> flag;
        [SetUp]
        public void SetUp()
        {
            board = ScriptableObject.CreateInstance<BlackboardData>();
            var keys = (List<BlackboardKeyData>)board.Keys;
            keys.Add(new BlackboardKeyData { ID = "input", Name = "Input", Type = BlackboardKeyType.Float });
            keys.Add(new BlackboardKeyData { ID = "flag", Name = "Flag", Type = BlackboardKeyType.Bool });
            input = board.GetKey<float>("Input"); flag = board.GetKey<bool>("Flag");
        }
        [TearDown]
        public void TearDown()
        {
            foreach (var controller in controllers) controller.Stop(); controllers.Clear();
            foreach (var tree in trees) UnityEngine.Object.DestroyImmediate(tree); trees.Clear();
            UnityEngine.Object.DestroyImmediate(board);
        }
        private BehaviorTreeData Tree(out BehaviorNodeData root, UtilitySelectorNode policy = null)
        {
            var tree = ScriptableObject.CreateInstance<BehaviorTreeData>(); trees.Add(tree);
            using (var so = new SerializedObject(tree)) { so.FindProperty("blackboard").objectReferenceValue = board; so.ApplyModifiedPropertiesWithoutUndo(); }
            root = tree.AddNode(policy ?? new UtilitySelectorNode { Interval = 0.1f, MinimumHoldTime = 0, SwitchMargin = 0 }, Vector2.zero);
            tree.Connect(null, root.ID); return tree;
        }
        private BehaviorNodeData Child(BehaviorTreeData tree, BehaviorNodeData parent, float score, BehaviorStatus result = BehaviorStatus.Running)
        {
            var node = tree.AddNode(new ActionData { result = result }, Vector2.one);
            node.UtilityScore.ConstantScore = score; tree.Connect(parent.ID, node.ID); return node;
        }
        private BehaviorTreeController Run(BehaviorTreeData tree)
        {
            var controller = tree.CreateController(null); controllers.Add(controller); controller.Start(); return controller;
        }
        private UtilityConsideration Term(UtilityInput kind = UtilityInput.Float)
        { return new UtilityConsideration { Input = kind, FloatKey = input, BoolKey = flag }; }

        [Test]
        public void HighestScoreWinsAndTiesKeepCurrent()
        {
            var tree = Tree(out var root); Child(tree, root, .3f); var b = Child(tree, root, .8f);
            var controller = Run(tree); controller.Tick(.1f);
            Assert.That(controller.Model.nodes[0].utility.selected, Is.EqualTo(1));
            b.UtilityScore.ConstantScore = .3f; controller.Tick(.1f);
            Assert.That(controller.Model.nodes[0].utility.selected, Is.EqualTo(1));
            controller.Stop(); controller.Start(); controller.Tick(.1f);
            Assert.That(controller.Model.nodes[0].utility.selected, Is.Zero);
        }
        [Test]
        public void FailureTriesNextCandidateAndSuccessCompletes()
        {
            var tree = Tree(out var root); Child(tree, root, 1, BehaviorStatus.Failure); Child(tree, root, .5f, BehaviorStatus.Success);
            var c = Run(tree); c.Tick(.1f);
            Assert.That(c.LastStatus, Is.EqualTo(BehaviorStatus.Success));
            Assert.That(c.Model.nodes[0].utility.reasons[0], Is.EqualTo(UtilityReason.Failed));
        }
        [Test]
        public void AllZeroDoesNotExecuteTasks()
        {
            var tree = Tree(out var root); Child(tree, root, 0); var c = Run(tree); c.Tick(.1f);
            Assert.That(c.LastStatus, Is.EqualTo(BehaviorStatus.Failure)); Assert.That(c.Model.nodes[1].taskEntered, Is.False);
        }
        [Test]
        public void CurveNormalizesAndClampsInputAndOutput()
        {
            var item = Term(); item.Minimum = 10; item.Maximum = 20; item.Curve = AnimationCurve.Linear(0, 2, 1, -1);
            Assert.That(UtilityEvaluator.Sample(item, 5, out var low), Is.True); Assert.That(low.score, Is.EqualTo(1));
            UtilityEvaluator.Sample(item, 30, out var high); Assert.That(high.score, Is.Zero);
            UtilityEvaluator.Sample(item, 15, out var mid); Assert.That(mid.normalized, Is.EqualTo(.5f)); Assert.That(mid.score, Is.EqualTo(.5f).Within(.001));
        }
        [Test]
        public void InvalidPreviewRangeAndCurveAreRejectedWithoutThrowing()
        {
            var item = Term(); item.Maximum = 0;
            Assert.That(UtilityEvaluator.Sample(item, .5f, out _), Is.False);
            item.Maximum = 1; item.Curve = null;
            Assert.That(UtilityEvaluator.Sample(item, .5f, out _), Is.False);
        }
        [TestCase(UtilityCombination.WeightedAverage, .5f)]
        [TestCase(UtilityCombination.Product, .1875f)]
        public void CombinationMatchesPreviewMath(UtilityCombination mode, float expected)
        {
            var tree = Tree(out var root); var child = Child(tree, root, 0); child.UtilityScore.Combination = mode;
            child.UtilityScore.Considerations.Add(new UtilityConsideration { Constant = .25f });
            child.UtilityScore.Considerations.Add(new UtilityConsideration { Constant = .75f });
            var c = Run(tree); c.Tick(.1f);
            Assert.That(c.Model.nodes[0].utility.scores[0], Is.EqualTo(expected).Within(.001));
        }
        [Test]
        public void MinimumHoldAndMarginPreventFlapping()
        {
            var tree = Tree(out var root, new UtilitySelectorNode { Interval = .1f, MinimumHoldTime = 1, SwitchMargin = .2f });
            Child(tree, root, .5f); var b = Child(tree, root, .4f); var c = Run(tree); c.Tick(.1f);
            b.UtilityScore.ConstantScore = .9f; c.Tick(.1f); Assert.That(c.Model.nodes[0].utility.reasons[1], Is.EqualTo(UtilityReason.MinimumHold));
            b.UtilityScore.ConstantScore = .6f; c.Tick(1); Assert.That(c.Model.nodes[0].utility.reasons[1], Is.EqualTo(UtilityReason.SwitchMargin));
            b.UtilityScore.ConstantScore = .9f; c.Tick(.1f); Assert.That(c.Model.nodes[0].utility.selected, Is.EqualTo(1));
            Assert.That(c.Model.nodes[1].exitReason, Is.EqualTo(BehaviorExitReason.Aborted));
        }
        [Test]
        public void PeriodicScoresDoNotRefreshEveryTickButExplicitRequestDoes()
        {
            var tree = Tree(out var root, new UtilitySelectorNode { Interval = 10, MinimumHoldTime = 0, SwitchMargin = 0 });
            Child(tree, root, .4f); var b = Child(tree, root, 0); b.UtilityScore.Considerations.Add(Term());
            var c = Run(tree); c.Tick(.1f); c.BlackboardWriter.SetValue(input, .8f); c.Tick(.1f);
            Assert.That(c.Model.nodes[0].utility.selected, Is.Zero);
            c.RequestEvaluation(); c.Tick(.1f); Assert.That(c.Model.nodes[0].utility.selected, Is.EqualTo(1));
        }
        [TestCase(BehaviorAbortMode.Self)]
        [TestCase(BehaviorAbortMode.Both)]
        public void InvalidCurrentConditionBypassesHold(BehaviorAbortMode mode)
        {
            var tree = Tree(out var root, new UtilitySelectorNode { Interval = 10, MinimumHoldTime = 100, SwitchMargin = 1 });
            var a = Child(tree, root, .9f); Child(tree, root, .1f);
            var guard = new BlackboardDecorator { KeyID = flag.ID };
            typeof(Decorator).GetField("abortMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(guard, mode);
            guard.Expected.BoolValue = true; ((List<Decorator>)a.Decorators).Add(guard);
            var c = Run(tree); c.BlackboardWriter.SetValue(flag, true); c.Tick(.1f);
            c.BlackboardWriter.SetValue(flag, false); c.Tick(.1f);
            Assert.That(c.Model.nodes[0].utility.selected, Is.EqualTo(1));
        }
        [Test]
        public void DifferentControllersHaveIndependentScoresAndSelection()
        {
            var tree = Tree(out var root); Child(tree, root, .4f); var b = Child(tree, root, 0); b.UtilityScore.Considerations.Add(Term());
            var a = Run(tree); var c = Run(tree); a.BlackboardWriter.SetValue(input, .9f); c.BlackboardWriter.SetValue(input, .1f);
            a.Tick(.1f); c.Tick(.1f);
            Assert.That(a.Model.nodes[0].utility.selected, Is.EqualTo(1)); Assert.That(c.Model.nodes[0].utility.selected, Is.Zero);
            Assert.That(a.Model.nodes[0].utility.samples, Is.Not.SameAs(c.Model.nodes[0].utility.samples));
        }
        [Test]
        public void InvalidRuntimeInputRejectsBranch()
        {
            var tree = Tree(out var root); var a = Child(tree, root, 0); a.UtilityScore.Considerations.Add(Term()); Child(tree, root, .1f);
            var c = Run(tree); c.BlackboardWriter.SetValue(input, float.NaN); c.Tick(.1f);
            Assert.That(c.Model.nodes[0].utility.reasons[0], Is.EqualTo(UtilityReason.InvalidInput)); Assert.That(c.Model.nodes[0].utility.selected, Is.EqualTo(1));
        }
        [Test]
        public void ReorderingChildrenKeepsNodeScoreSettings()
        {
            var tree = Tree(out var root); var a = Child(tree, root, .2f); var b = Child(tree, root, .8f);
            root.Children.Reverse(); Assert.That(tree.FindNode(a.ID).UtilityScore.ConstantScore, Is.EqualTo(.2f));
            var c = Run(tree); c.Tick(.1f); Assert.That(c.Model.nodes[c.Model.nodes[0].children[0]].definition.ID, Is.EqualTo(b.ID));
        }
        [Test]
        public void InvalidRangeAndKeyPreventControllerCreation()
        {
            var tree = Tree(out var root); var a = Child(tree, root, 0); var item = Term(); item.Maximum = 0; a.UtilityScore.Considerations.Add(item);
            Assert.Throws<InvalidOperationException>(() => tree.CreateController(null));
            item.Maximum = 1; item.FloatKey = new BlackboardKeyReference<float>(); Assert.Throws<InvalidOperationException>(() => tree.CreateController(null));
        }
        [Test]
        public void RepeatedCurveEvaluationAndSelectionDoNotAllocate()
        {
            var tree = Tree(out var root, new UtilitySelectorNode { Interval = .001f, MinimumHoldTime = 0, SwitchMargin = 0 });
            var a = Child(tree, root, 0); a.UtilityScore.Considerations.Add(Term()); Child(tree, root, .1f);
            var c = Run(tree); c.BlackboardWriter.SetValue(input, .8f); for (int i = 0; i < 10; i++) c.Tick(.002f);
            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 100; i++) c.Tick(.002f);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void NestedNormalSelectorKeepsItsOwnFailureOrder()
        {
            var tree = Tree(out var root);
            var normal = tree.AddNode(new SelectorNode(), Vector2.one); normal.UtilityScore.ConstantScore = .9f; tree.Connect(root.ID, normal.ID);
            Child(tree, normal, 0, BehaviorStatus.Failure); Child(tree, normal, 0, BehaviorStatus.Success);
            Child(tree, root, .1f);
            var c = Run(tree); c.Tick(.1f); Assert.That(c.LastStatus, Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void BoolAndIntInputsAreTypedAndWeightsCanDisableAnItem()
        {
            ((List<BlackboardKeyData>)board.Keys).Add(new BlackboardKeyData { ID = "number", Name = "Number", Type = BlackboardKeyType.Int });
            var tree = Tree(out var root); var a = Child(tree, root, 0);
            a.UtilityScore.Considerations.Add(Term(UtilityInput.Bool));
            a.UtilityScore.Considerations.Add(new UtilityConsideration { Input = UtilityInput.Int, IntKey = board.GetKey<int>("Number"), Maximum = 10 });
            a.UtilityScore.Considerations.Add(new UtilityConsideration { Constant = 0, Weight = 0 });
            var c = Run(tree); c.BlackboardWriter.SetValue(flag, true); c.BlackboardWriter.SetValue(board.GetKey<int>("Number"), 5); c.Tick(.1f);
            Assert.That(c.Model.nodes[0].utility.scores[0], Is.EqualTo(.75f).Within(.001));
        }

        [Test]
        public void CurveAndChildIdentitySurviveSaveReloadAndUndo()
        {
            const string boardPath = "Assets/__UtilityTestBoard.asset", treePath = "Assets/__UtilityTestTree.asset";
            Assert.That(AssetDatabase.LoadMainAssetAtPath(boardPath), Is.Null); Assert.That(AssetDatabase.LoadMainAssetAtPath(treePath), Is.Null);
            var tree = Tree(out var root); var a = Child(tree, root, 0); Child(tree, root, .1f);
            var term = Term(); term.Curve = AnimationCurve.Linear(0, 1, 1, 0); a.UtilityScore.Considerations.Add(term);
            string childID = a.ID;
            try
            {
                AssetDatabase.CreateAsset(board, boardPath); AssetDatabase.CreateAsset(tree, treePath); AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(treePath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BehaviorTreeData>(treePath);
                Assert.That(loaded.FindNode(childID).UtilityScore.Considerations[0].Curve.Evaluate(.25f), Is.EqualTo(.75f).Within(.001));
                Undo.IncrementCurrentGroup(); Undo.RecordObject(loaded, "Reorder utility children");
                loaded.FindNode(root.ID).Children.Reverse(); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.That(loaded.FindNode(root.ID).Children[0], Is.EqualTo(childID));
                Assert.That(loaded.FindNode(childID).UtilityScore.Considerations[0].FloatKey.ID, Is.EqualTo(input.ID));
                Undo.ClearUndo(loaded);
            }
            finally { AssetDatabase.DeleteAsset(treePath); AssetDatabase.DeleteAsset(boardPath); }
        }
    }
}
