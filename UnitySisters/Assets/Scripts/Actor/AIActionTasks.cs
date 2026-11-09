using System;
using UnityEngine;
using UnityFramework.BehaviorTree;
using UnitySisters.Controller.Interface;

namespace UnitySisters.BehaviorTree
{
    [Serializable]
    public sealed class AttackTargetTaskData : TaskNodeData<AttackTargetTask> { }
    public sealed class AttackTargetTask : TaskNode
    {
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            var control = context.GetInterface<IAICommandControl>();
            return control != null && control.RequestAttack() ? BehaviorStatus.Success : BehaviorStatus.Failure;
        }
    }

    [Serializable]
    public sealed class RoamTaskData : TaskNodeData<RoamTask> { }
    public sealed class RoamTask : TaskNode
    {
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            var control = context.GetInterface<IAICommandControl>();
            if (control == null || !control.TryGetPosition(out _)) return BehaviorStatus.Failure;
            control.RequestRoam();
            return BehaviorStatus.Running;
        }
        protected override void OnExit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason)
        { context.GetInterface<IAICommandControl>()?.RequestMovement(Vector3.zero); }
    }
}
