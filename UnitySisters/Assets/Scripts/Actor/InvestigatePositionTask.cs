using System;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.BehaviorTree;
using UnitySisters.Controller.Interface;

namespace UnitySisters.BehaviorTree
{
    [Serializable]
    public sealed class InvestigatePositionTaskData : TaskNodeData<InvestigatePositionTask>
    {
        [SerializeField] private BlackboardKeyReference<Vector3> positionKey = new();
        [SerializeField] private BlackboardKeyReference<bool> pendingKey = new();
        [SerializeField, Min(0)] private float stoppingDistance = 0.5f;
        [SerializeField, Min(0)] private float progressTimeout;
        public BlackboardKeyReference<Vector3> PositionKey { get => positionKey; }
        public BlackboardKeyReference<bool> PendingKey { get => pendingKey; }
        public float StoppingDistance { get => stoppingDistance; }
        public float ProgressTimeout { get => progressTimeout; }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            if (!schema.ContainsKey(positionKey) || !schema.ContainsKey(pendingKey)) errors.Add("Investigation requires position and pending keys.");
            if (float.IsNaN(stoppingDistance) || float.IsInfinity(stoppingDistance) || stoppingDistance < 0) errors.Add("Invalid investigation stopping distance.");
            if (float.IsNaN(progressTimeout) || float.IsInfinity(progressTimeout) || progressTimeout < 0) errors.Add("Invalid investigation progress timeout.");
        }
    }
    public sealed class InvestigatePositionTask : TaskNode
    {
        private sealed class Memory { internal Vector3 position; internal float stalled; internal bool initialized; }
        protected override object CreateMemory() { return new Memory(); }
        protected override void OnEnter(BehaviorTreeContext context, TaskNodeData data, object memory)
        { var state = (Memory)memory; state.initialized = false; state.stalled = 0; }
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            var settings = (InvestigatePositionTaskData)data;
            var control = context.GetInterface<IAICommandControl>();
            if (control == null || !control.TryGetPosition(out var position) ||
                !context.Blackboard.TryGetValue(settings.PositionKey, out var destination)) return BehaviorStatus.Failure;
            Vector3 delta = destination - position; delta.y = 0;
            var state = (Memory)memory;
            Vector3 progress = position - state.position; progress.y = 0;
            if (!state.initialized || progress.sqrMagnitude > .01f)
            { state.position = position; state.stalled = 0; state.initialized = true; }
            else state.stalled += context.DeltaTime;
            if (delta.sqrMagnitude <= settings.StoppingDistance * settings.StoppingDistance ||
                settings.ProgressTimeout > 0 && state.stalled >= settings.ProgressTimeout)
            {
                control.RequestMovement(Vector3.zero);
                context.BlackboardWriter.SetValue(settings.PendingKey, false);
                return BehaviorStatus.Success;
            }
            control.RequestMovement(delta.normalized); return BehaviorStatus.Running;
        }
        protected override void OnExit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason)
        { context.GetInterface<IAICommandControl>()?.RequestMovement(Vector3.zero); }
    }

    [Serializable]
    public sealed class StopMovementTaskData : TaskNodeData<StopMovementTask> { }
    public sealed class StopMovementTask : TaskNode
    {
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            context.GetInterface<IAICommandControl>()?.RequestMovement(Vector3.zero);
            return BehaviorStatus.Running;
        }
    }
}
