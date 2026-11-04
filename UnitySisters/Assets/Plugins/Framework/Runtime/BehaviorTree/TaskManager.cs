using System;
using System.Collections.Generic;

namespace UnityFramework.BehaviorTree
{
    internal static class TaskManager
    {
        private static readonly Dictionary<Type, TaskNode> tasks = new();

        internal static TTask GetTask<TTask>() where TTask : TaskNode, new()
        {
            if (!tasks.TryGetValue(typeof(TTask), out TaskNode task))
            {
                task = new TTask();
                tasks.Add(typeof(TTask), task);
            }
            return (TTask)task;
        }
    }
}
