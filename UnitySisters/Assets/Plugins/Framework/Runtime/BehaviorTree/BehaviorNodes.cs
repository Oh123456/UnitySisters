using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    // Node objects are shared definitions. Actor-specific state belongs to CreateMemory().
    [Serializable]
    public abstract class BehaviorNode
    {
        private protected BehaviorNode() { }
    }

    [Serializable] internal sealed class SelectorNode : BehaviorNode { }
    [Serializable] internal sealed class SequenceNode : BehaviorNode { }

    [Serializable]
    public abstract class TaskNodeData : BehaviorNode
    {
        private protected TaskNodeData() { }
        internal abstract TaskNode Task { get; }
        protected virtual void Validate(BlackboardData schema, ICollection<string> errors) { }
        internal TaskNode ResolveTask()
        {
            return Task ?? throw new InvalidOperationException($"{GetType().Name}: Task behavior is missing.");
        }
        internal void ValidateDefinition(BlackboardData schema, ICollection<string> errors) { Validate(schema, errors); }
    }

    [Serializable]
    public abstract class TaskNodeData<TTask> : TaskNodeData where TTask : TaskNode, new()
    {
        internal sealed override TaskNode Task { get => TaskManager.GetTask<TTask>(); }
    }

    public abstract class TaskNode
    {
        protected virtual object CreateMemory() { return null; }
        protected virtual void OnEnter(BehaviorTreeContext context, TaskNodeData data, object memory) { }
        protected abstract BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory);
        protected virtual void OnExit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason) { }
        internal object NewMemory() { return CreateMemory(); }
        internal void Enter(BehaviorTreeContext context, TaskNodeData data, object memory) { OnEnter(context, data, memory); }
        internal BehaviorStatus Tick(BehaviorTreeContext context, TaskNodeData data, object memory) { return OnTick(context, data, memory); }
        internal void Exit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason) { OnExit(context, data, memory, reason); }
    }

    [Serializable]
    public abstract class Decorator
    {
        [SerializeField] private BehaviorAbortMode abortMode = BehaviorAbortMode.Self;
        internal BehaviorAbortMode AbortMode { get => abortMode; }
        protected abstract bool Evaluate(BehaviorTreeContext context);
        protected virtual void Validate(BlackboardData schema, ICollection<string> errors) { }
        internal bool Test(BehaviorTreeContext context) { return Evaluate(context); }
        internal void ValidateDefinition(BlackboardData schema, ICollection<string> errors) { Validate(schema, errors); }
    }

    [Serializable]
    public abstract class Service
    {
        [SerializeField] private float interval = 0.2f;
        protected internal float Interval { get => interval; }
        protected virtual object CreateMemory() { return null; }
        protected virtual void OnEnter(BehaviorTreeContext context, object memory) { }
        protected abstract void OnTick(BehaviorTreeContext context, object memory);
        protected virtual void OnExit(BehaviorTreeContext context, object memory, BehaviorExitReason reason) { }
        protected virtual void Validate(BlackboardData schema, ICollection<string> errors) { }
        internal object NewMemory() { return CreateMemory(); }
        internal void Enter(BehaviorTreeContext context, object memory) { OnEnter(context, memory); }
        internal void Tick(BehaviorTreeContext context, object memory) { OnTick(context, memory); }
        internal void Exit(BehaviorTreeContext context, object memory, BehaviorExitReason reason) { OnExit(context, memory, reason); }
        internal void ValidateDefinition(BlackboardData schema, ICollection<string> errors)
        {
            if (interval <= 0 || float.IsNaN(interval) || float.IsInfinity(interval)) errors.Add("Service interval must be finite and greater than zero.");
            Validate(schema, errors);
        }
    }

    [Serializable]
    internal sealed class WaitTaskData : TaskNodeData<WaitTask>
    {
        [SerializeField] private float duration = 1.0f;
        internal float Duration { get => duration; }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            if (duration < 0 || float.IsNaN(duration) || float.IsInfinity(duration)) errors.Add("Wait duration must be finite and nonnegative.");
        }
    }

    internal sealed class WaitTask : TaskNode
    {
        public WaitTask() { }
        private sealed class Memory { internal float elapsed; }
        protected override object CreateMemory() { return new Memory(); }
        protected override void OnEnter(BehaviorTreeContext context, TaskNodeData data, object memory) { ((Memory)memory).elapsed = 0; }
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            Memory state = (Memory)memory;
            state.elapsed += context.DeltaTime;
            return state.elapsed >= ((WaitTaskData)data).Duration ? BehaviorStatus.Success : BehaviorStatus.Running;
        }
    }

    [Serializable]
    internal sealed class BlackboardDecorator : Decorator
    {
        [SerializeField] private string keyId;
        internal string KeyID { get => keyId; set => keyId = value; }
        [SerializeField] private BlackboardComparison comparison;
        internal BlackboardComparison Comparison { get => comparison; set => comparison = value; }
        [SerializeField] private BlackboardValue expected = new BlackboardValue();
        internal BlackboardValue Expected { get => expected; }
        protected override bool Evaluate(BehaviorTreeContext context)
        {
            BlackboardKeyData key = context.Schema.FindKey(keyId);
            return context.Storage.Compare(keyId, key.Type, comparison, expected);
        }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            BlackboardKeyData key = schema.FindKey(keyId);
            if (key == null || expected == null) { errors.Add("Blackboard condition has a missing key or value."); return; }
            bool equality = comparison == BlackboardComparison.Equal || comparison == BlackboardComparison.NotEqual;
            bool order = comparison >= BlackboardComparison.Less && comparison <= BlackboardComparison.GreaterOrEqual;
            bool set = comparison == BlackboardComparison.IsSet || comparison == BlackboardComparison.IsNotSet;
            if (!equality && !(order && (key.Type == BlackboardKeyType.Int || key.Type == BlackboardKeyType.Float)) && !(set && key.Type == BlackboardKeyType.Object)) errors.Add("Blackboard comparison does not support the selected key type.");
        }
    }

    [Serializable]
    internal sealed class SetBlackboardValueTaskData : TaskNodeData<SetBlackboardValueTask>
    {
        [SerializeField] private string keyId;
        internal string KeyID { get => keyId; set => keyId = value; }
        [SerializeField] private BlackboardValue value = new BlackboardValue();
        internal BlackboardValue Value { get => value; }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            if (schema.FindKey(keyId) == null || value == null) errors.Add("Set value task has a missing blackboard key or value.");
        }
    }

    internal sealed class SetBlackboardValueTask : TaskNode
    {
        public SetBlackboardValueTask() { }
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            var settings = (SetBlackboardValueTaskData)data;
            context.Storage.Write(settings.KeyID, context.Schema.FindKey(settings.KeyID).Type, settings.Value);
            return BehaviorStatus.Success;
        }
    }

    [Serializable]
    internal sealed class SetBlackboardValueService : Service
    {
        [SerializeField] private string keyId;
        [SerializeField] private BlackboardValue value = new BlackboardValue();
        internal string KeyID { get => keyId; set => keyId = value; }
        internal BlackboardValue Value { get => value; }
        protected override void OnTick(BehaviorTreeContext context, object memory) { context.Storage.Write(keyId, context.Schema.FindKey(keyId).Type, value); }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            if (schema.FindKey(keyId) == null || value == null) errors.Add("Set value service has a missing blackboard key or value.");
        }
    }
}
