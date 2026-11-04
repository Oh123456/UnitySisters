using System;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.BehaviorTree;

using UnitySisters.Controller.Interface;

namespace UnitySisters.BehaviorTree
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "UnitySisters.BehaviorTree", sourceAssembly: "Assembly-CSharp", sourceClassName: "ChaseTargetTask")]
    public sealed class ChaseTargetTaskData : TaskNodeData<ChaseTargetTask>
    {
        [SerializeField] private BlackboardKeyReference<UnityEngine.Object> targetKey = new();
        [SerializeField] private float stoppingDistance = 1.5f;
        public BlackboardKeyReference<UnityEngine.Object> TargetKey { get => targetKey; }
        public float StoppingDistance { get => stoppingDistance; }
        protected override void Validate(BlackboardData schema, ICollection<string> errors)
        {
            if (!schema.ContainsKey(targetKey))
                errors.Add("Chase target requires an Object blackboard key.");
            if (stoppingDistance < 0 || float.IsNaN(stoppingDistance) || float.IsInfinity(stoppingDistance))
                errors.Add("Stopping distance must be finite and nonnegative.");
        }
    }

    public sealed class ChaseTargetTask : TaskNode
    {
        protected override BehaviorStatus OnTick(BehaviorTreeContext context, TaskNodeData data, object memory)
        {
            IAICommandControl control = context.GetInterface<IAICommandControl>();
            if (control == null)
                return BehaviorStatus.Failure;
            if (!control.TryGetPosition(out Vector3 position) ||
                !context.Blackboard.TryGetValue(((ChaseTargetTaskData)data).TargetKey, out UnityEngine.Object value) ||
                value is not Transform target || target == null)
            {
                control.RequestMovement(Vector3.zero);
                return BehaviorStatus.Failure;
            }
            Vector3 direction = target.position - position;
            direction.y = 0;
            control.RequestMovement(direction.sqrMagnitude > ((ChaseTargetTaskData)data).StoppingDistance * ((ChaseTargetTaskData)data).StoppingDistance ? direction.normalized : Vector3.zero);
            return BehaviorStatus.Running;
        }
    
        protected override void OnExit(BehaviorTreeContext context, TaskNodeData data, object memory, BehaviorExitReason reason)
        {
            context.GetInterface<IAICommandControl>()?.RequestMovement(Vector3.zero);
        }
    
    }
}
