using System;
using System.Collections.Generic;

namespace UnityFramework.BehaviorTree
{
    public sealed class BehaviorTreeController
    {
        private readonly BehaviorTreeModel model;
        private bool ticking;
        private bool forceUtilityEvaluation;
        public IReadOnlyBlackboard Blackboard { get => model.blackboard; }
        public IBlackboardWriter BlackboardWriter { get => model.blackboard; }
        public bool IsRunning { get => model.running; }
        public BehaviorStatus? LastStatus { get => model.lastStatus; }
        internal BehaviorTreeModel Model { get => model; }

        internal BehaviorTreeController(BehaviorTreeData data, IBehaviorTreeOwner owner)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            List<string> errors = data.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            model = new BehaviorTreeModel(data, owner);
        }

        public void Start()
        {
            if (ticking) throw new InvalidOperationException("Start must be called outside tree execution.");
            if (model.running) return;
            model.running = true;
            model.dirty = true;
            model.lastStatus = null;
            foreach (BehaviorNodeMemory node in model.nodes)
            { node.status = null; node.exitReason = null; node.conditionsPassed = null; node.utility?.Reset(); }
            Blackboard.KeyChanged += OnKeyChanged;
        }

        public void Stop()
        {
            if (!model.running) return;
            model.running = false;
            Blackboard.KeyChanged -= OnKeyChanged;
            ExitSubtree(model.root, BehaviorExitReason.Stopped);
        }

        private void OnKeyChanged(string id) { model.dirty = true; }

        public void RequestEvaluation() { model.dirty = true; forceUtilityEvaluation = true; }

        public void Tick(float deltaTime)
        {
            if (!model.running) return;
            if (ticking) throw new InvalidOperationException("Behavior tree execution cannot be reentered.");
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            ticking = true;
            try
            {
                model.context.SetDeltaTime(deltaTime);
                UpdateServices(model.root, deltaTime);
                if (model.dirty)
                {
                    model.dirty = false;
                    EvaluateObservers(model.root);
                    forceUtilityEvaluation = false;
                }
                if (model.running) model.lastStatus = TickNode(model.root);
            }
            catch
            {
                try { Stop(); } catch { }
                throw;
            }
            finally { ticking = false; }
        }

        private bool ConditionsPass(BehaviorNodeMemory node)
        {
            for (int index = 0; index < node.definition.Decorators.Count; index++)
                if (!node.definition.Decorators[index].Test(model.context)) { node.conditionsPassed = false; return false; }
            node.conditionsPassed = true;
            return true;
        }

        private BehaviorStatus TickNode(int index)
        {
            BehaviorNodeMemory node = model.nodes[index];
            if (!node.active)
            {
                if (!ConditionsPass(node)) { node.status = BehaviorStatus.Failure; return BehaviorStatus.Failure; }
                node.active = true;
                node.childIndex = 0;
                node.utility?.Reset();
                node.exitReason = null;
                Array.Clear(node.serviceElapsed, 0, node.serviceElapsed.Length);
                for (int service = 0; service < node.definition.Services.Count; service++)
                {
                    node.activeServices = service + 1;
                    node.definition.Services[service].Enter(model.context, node.serviceMemory[service]);
                    if (!model.running || !node.active) return BehaviorStatus.Failure;
                }
                if (node.task is TaskNode task)
                {
                    node.taskEntered = true;
                    task.Enter(model.context, (TaskNodeData)node.definition.Behavior, node.taskMemory);
                    if (!model.running || !node.active) return BehaviorStatus.Failure;
                }
            }
            BehaviorStatus result;
            if (node.task is TaskNode action) result = action.Tick(model.context, (TaskNodeData)node.definition.Behavior, node.taskMemory);
            else if (node.definition.Behavior is UtilitySelectorNode utility) result = TickUtility(node, utility);
            else
            {
                bool selector = node.definition.Behavior is SelectorNode;
                result = selector ? BehaviorStatus.Failure : BehaviorStatus.Success;
                while (node.childIndex < node.children.Length)
                {
                    BehaviorStatus childResult = TickNode(node.children[node.childIndex]);
                    if (!model.running || !node.active) return BehaviorStatus.Failure;
                    if (childResult == BehaviorStatus.Running) { result = childResult; break; }
                    if ((selector && childResult == BehaviorStatus.Success) || (!selector && childResult == BehaviorStatus.Failure)) { result = childResult; break; }
                    node.childIndex++;
                }
            }
            if (!model.running || !node.active) return BehaviorStatus.Failure;
            if (result != BehaviorStatus.Success && result != BehaviorStatus.Failure && result != BehaviorStatus.Running) throw new InvalidOperationException("Task returned an invalid status.");
            node.status = result;
            if (result != BehaviorStatus.Running) ExitSubtree(index, result == BehaviorStatus.Success ? BehaviorExitReason.Success : BehaviorExitReason.Failure);
            return result;
        }

        private void UpdateServices(int nodeIndex, float deltaTime)
        {
            BehaviorNodeMemory node = model.nodes[nodeIndex];
            if (!node.active) return;
            for (int index = 0; index < node.activeServices; index++)
            {
                Service service = node.definition.Services[index];
                node.serviceElapsed[index] += deltaTime;
                if (node.serviceElapsed[index] < service.Interval) continue;
                node.serviceElapsed[index] %= service.Interval;
                service.Tick(model.context, node.serviceMemory[index]);
                if (!model.running) return;
            }
            if (node.childIndex < node.children.Length)
            {
                int child = node.children[node.childIndex];
                if (model.nodes[child].active) UpdateServices(child, deltaTime);
            }
        }


        private void EvaluateObservers(int index)
        {
            BehaviorNodeMemory node = model.nodes[index];
            if (!node.active) return;
            for (int decoratorIndex = 0; decoratorIndex < node.definition.Decorators.Count; decoratorIndex++)
            {
                Decorator decorator = node.definition.Decorators[decoratorIndex];
                if ((decorator.AbortMode == BehaviorAbortMode.Self || decorator.AbortMode == BehaviorAbortMode.Both) && !decorator.Test(model.context))
                { ExitSubtree(index, BehaviorExitReason.Aborted); return; }
            }

            if (node.utility != null)
            {
                if (forceUtilityEvaluation) node.utility.force = true;
                for (int i = 0; i < node.children.Length; i++)
                {
                    var decorators = model.nodes[node.children[i]].definition.Decorators;
                    for (int d = 0; d < decorators.Count; d++)
                    {
                        Decorator decorator = decorators[d];
                        if (decorator.AbortMode == BehaviorAbortMode.LowerPriority || decorator.AbortMode == BehaviorAbortMode.Both)
                            node.utility.force = true;
                    }
                }
            }

            if (node.definition.Behavior is SelectorNode)
            {
                for (int child = 0; child < node.childIndex; child++)
                {
                    if (ObserveLowerPriority(node.children[child]))
                    {
                        ExitSubtree(node.children[node.childIndex], BehaviorExitReason.Aborted);
                        node.childIndex = child;
                        break;
                    }
                }
            }
            if (node.childIndex < node.children.Length) EvaluateObservers(node.children[node.childIndex]);
        }

        private bool ObserveLowerPriority(int index)
        {
            BehaviorNodeMemory node = model.nodes[index];
            if (!node.conditionsPassed.HasValue) return false;
            bool previous = node.conditionsPassed.Value;
            if (!ConditionsPass(node)) return false;
            if (!previous)
                for (int decorator = 0; decorator < node.definition.Decorators.Count; decorator++)
                {
                    BehaviorAbortMode mode = node.definition.Decorators[decorator].AbortMode;
                    if (mode == BehaviorAbortMode.LowerPriority || mode == BehaviorAbortMode.Both) return true;
                }
            if (node.utility != null) return false;
            foreach (int child in node.children)
            {
                if (ObserveLowerPriority(child)) return true;
                if (node.definition.Behavior is SequenceNode && model.nodes[child].status != BehaviorStatus.Success) break;
            }
            return false;
        }

        private BehaviorStatus TickUtility(BehaviorNodeMemory node, UtilitySelectorNode settings)
        {
            var runtime = node.utility;
            runtime.elapsed += model.context.DeltaTime;
            runtime.held += model.context.DeltaTime;
            if (runtime.selected >= 0 && !model.nodes[node.children[runtime.selected]].active) runtime.force = true;
            if (runtime.force || runtime.elapsed >= settings.Interval || runtime.selected < 0)
            {
                runtime.force = false; runtime.elapsed = 0;
                Array.Clear(runtime.failed, 0, runtime.failed.Length);
                for (int i = 0; i < node.children.Length; i++)
                {
                    var child = model.nodes[node.children[i]];
                    Array.Clear(runtime.samples[i], 0, runtime.samples[i].Length);
                    runtime.scores[i] = 0;
                    if (!ConditionsPass(child)) { runtime.reasons[i] = UtilityReason.ConditionsRejected; continue; }
                    if (!UtilityEvaluator.Evaluate(child.definition.UtilityScore, model.blackboard, runtime.samples[i], out float score))
                    { runtime.reasons[i] = UtilityReason.InvalidInput; continue; }
                    runtime.scores[i] = score;
                    runtime.reasons[i] = score > 0 ? UtilityReason.LowerScore : UtilityReason.ZeroScore;
                }
                int best = BestUtility(node);
                int current = runtime.selected;
                bool currentValid = current >= 0 && runtime.scores[current] > 0 && model.nodes[node.children[current]].active;
                if (best != current && currentValid && best >= 0)
                {
                    if (runtime.held < settings.MinimumHoldTime)
                    { runtime.reasons[best] = UtilityReason.MinimumHold; best = current; }
                    else if (runtime.scores[best] <= runtime.scores[current] + settings.SwitchMargin)
                    { runtime.reasons[best] = UtilityReason.SwitchMargin; best = current; }
                }
                SelectUtility(node, best);
                if (!model.running || !node.active) return BehaviorStatus.Failure;
            }
            while (runtime.selected >= 0)
            {
                int selected = runtime.selected;
                BehaviorStatus result = TickNode(node.children[selected]);
                if (!model.running || !node.active) return BehaviorStatus.Failure;
                if (result != BehaviorStatus.Failure) return result;
                runtime.failed[selected] = true; runtime.reasons[selected] = UtilityReason.Failed;
                SelectUtility(node, BestUtility(node));
                if (!model.running || !node.active) return BehaviorStatus.Failure;
            }
            return BehaviorStatus.Failure;
        }

        private int BestUtility(BehaviorNodeMemory node)
        {
            var runtime = node.utility;
            int best = runtime.selected;
            if (best < 0 || runtime.failed[best] || runtime.scores[best] <= 0) best = -1;
            for (int i = 0; i < runtime.scores.Length; i++)
                if (!runtime.failed[i] && runtime.scores[i] > 0 && (best < 0 || runtime.scores[i] > runtime.scores[best])) best = i;
            return best;
        }

        private void SelectUtility(BehaviorNodeMemory node, int selected)
        {
            var runtime = node.utility;
            if (runtime.selected != selected)
            {
                if (runtime.selected >= 0) ExitSubtree(node.children[runtime.selected], BehaviorExitReason.Aborted);
                runtime.selected = selected; runtime.held = 0;
            }
            node.childIndex = selected >= 0 ? selected : node.children.Length;
            if (selected >= 0) runtime.reasons[selected] = UtilityReason.Selected;
        }

        private void ExitSubtree(int index, BehaviorExitReason reason)
        {
            BehaviorNodeMemory node = model.nodes[index];
            if (!node.active) return;
            node.active = false;
            node.exitReason = reason;
            if (reason == BehaviorExitReason.Aborted || reason == BehaviorExitReason.Stopped) node.status = BehaviorStatus.Failure;
            Exception firstError = null;
            foreach (int child in node.children)
                try { ExitSubtree(child, reason); } catch (Exception error) { if (firstError == null) firstError = error; }
            if (node.taskEntered)
            {
                node.taskEntered = false;
                try { node.task.Exit(model.context, (TaskNodeData)node.definition.Behavior, node.taskMemory, reason); }
                catch (Exception error) { if (firstError == null) firstError = error; }
            }
            int services = node.activeServices;
            node.activeServices = 0;
            for (int service = services - 1; service >= 0; service--)
                try { node.definition.Services[service].Exit(model.context, node.serviceMemory[service], reason); }
                catch (Exception error) { if (firstError == null) firstError = error; }
            if (firstError != null) throw firstError;
        }
    }
}
