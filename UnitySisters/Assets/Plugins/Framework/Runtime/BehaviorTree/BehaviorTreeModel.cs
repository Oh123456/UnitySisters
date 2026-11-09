using System.Collections.Generic;

namespace UnityFramework.BehaviorTree
{
    internal sealed class BehaviorNodeMemory
    {
        internal readonly BehaviorNodeData definition;
        internal readonly int[] children;
        internal readonly object taskMemory;
        internal readonly TaskNode task;
        internal readonly object[] serviceMemory;
        internal readonly float[] serviceElapsed;
        internal bool active;
        internal bool taskEntered;
        internal int activeServices;
        internal int childIndex;
        internal BehaviorStatus? status;
        internal BehaviorExitReason? exitReason;
        internal bool? conditionsPassed;
        internal UtilityRuntime utility;

        internal BehaviorNodeMemory(BehaviorNodeData definition, Dictionary<string, int> indices)
        {
            this.definition = definition;
            children = new int[definition.Children.Count];
            for (int index = 0; index < children.Length; index++) children[index] = indices[definition.Children[index]];
            task = (definition.Behavior as TaskNodeData)?.ResolveTask();
            taskMemory = task?.NewMemory();
            serviceMemory = new object[definition.Services.Count];
            serviceElapsed = new float[serviceMemory.Length];
            for (int index = 0; index < serviceMemory.Length; index++) serviceMemory[index] = definition.Services[index].NewMemory();
        }
    }

    internal sealed class BehaviorTreeModel
    {
        internal readonly BehaviorTreeData data;
        internal readonly Blackboard blackboard;
        internal readonly BehaviorTreeContext context;
        internal readonly BehaviorNodeMemory[] nodes;
        internal readonly int root;
        internal bool running;
        internal bool dirty;
        internal BehaviorStatus? lastStatus;

        internal BehaviorTreeModel(BehaviorTreeData data, IBehaviorTreeOwner owner)
        {
            this.data = data;
            blackboard = new Blackboard(data.BlackboardData);
            context = new BehaviorTreeContext(owner, blackboard, data.BlackboardData);
            Dictionary<string, int> indices = new Dictionary<string, int>();
            for (int index = 0; index < data.Nodes.Count; index++) indices.Add(data.Nodes[index].ID, index);
            nodes = new BehaviorNodeMemory[data.Nodes.Count];
            for (int index = 0; index < nodes.Length; index++)
            {
                nodes[index] = new BehaviorNodeMemory(data.Nodes[index], indices);
                if (data.Nodes[index].Behavior is UtilitySelectorNode) nodes[index].utility = new UtilityRuntime(data.Nodes[index], data);
            }
            root = indices[data.RootID];
        }
    }
}
