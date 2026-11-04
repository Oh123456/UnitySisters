namespace UnityFramework.BehaviorTree
{
    public enum BehaviorStatus { Success, Failure, Running }
    public enum BehaviorExitReason { Success, Failure, Aborted, Stopped }
    public enum BehaviorAbortMode { None, Self, LowerPriority, Both }
    internal enum BlackboardComparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual, IsSet, IsNotSet }

    public interface IBehaviorTreeOwner
    {
        T GetInterface<T>() where T : class;
    }

    public sealed class BehaviorTreeContext
    {
        private readonly IBehaviorTreeOwner owner;
        private readonly Blackboard blackboard;
        private readonly BlackboardData schema;
        private float deltaTime;
        public IReadOnlyBlackboard Blackboard { get => blackboard; }
        public IBlackboardWriter BlackboardWriter { get => blackboard; }
        public float DeltaTime { get => deltaTime; }
        internal Blackboard Storage { get => blackboard; }
        internal BlackboardData Schema { get => schema; }
        internal BehaviorTreeContext(IBehaviorTreeOwner owner, Blackboard blackboard, BlackboardData schema) { this.owner = owner; this.blackboard = blackboard; this.schema = schema; }
        internal void SetDeltaTime(float value) { deltaTime = value; }
        public T GetInterface<T>() where T : class { return owner?.GetInterface<T>(); }
    }
}
