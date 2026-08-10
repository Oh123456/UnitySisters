using System;

namespace UnityFramework.FSM
{
    public enum StateChangeResult
    {
        Success,
        NotRunning,
        AlreadyCurrent,
        TargetNotFound,
        TransitionNotFound,
        ConditionFailed
    }

    public enum StateChangeReason
    {
        Start,
        Transition,
        Reset
    }

    public readonly struct StateChangedEvent
    {
        private readonly int? previousStateID;
        private readonly int currentStateID;
        private readonly StateChangeReason reason;

        public int? PreviousStateID => this.previousStateID;
        public int CurrentStateID => this.currentStateID;
        public StateChangeReason Reason => this.reason;

        public StateChangedEvent(int? previousStateID, int currentStateID, StateChangeReason reason)
        {
            this.previousStateID = previousStateID;
            this.currentStateID = currentStateID;
            this.reason = reason;
        }
    }

    public readonly struct TransitionEvaluatedEvent
    {
        private readonly int? fromStateID;
        private readonly int requestedStateID;
        private readonly StateTransition transition;
        private readonly StateChangeResult result;

        public int? FromStateID => this.fromStateID;
        public int RequestedStateID => this.requestedStateID;
        public StateTransition Transition => this.transition;
        public StateChangeResult Result => this.result;

        public TransitionEvaluatedEvent(
            int? fromStateID,
            int requestedStateID,
            StateTransition transition,
            StateChangeResult result)
        {
            this.fromStateID = fromStateID;
            this.requestedStateID = requestedStateID;
            this.transition = transition;
            this.result = result;
        }
    }

    public sealed class StateTransition
    {
        private readonly Func<IStateMachine, bool> condition;
        private readonly int fromStateID;
        private readonly int toStateID;
        private readonly string name;
        private readonly int priority;

        public int FromStateID => this.fromStateID;
        public int ToStateID => this.toStateID;
        public string Name => this.name;
        public int Priority => this.priority;
        public bool HasCondition => this.condition != null;

        /// <summary>
        /// 상태 전이에 필요한 출발지, 목적지, 조건과 우선순위 설정
        /// </summary>
        public StateTransition(
            int fromStateID,
            int toStateID,
            Func<IStateMachine, bool> condition = null,
            int priority = 0,
            string name = null)
        {
            this.fromStateID = fromStateID;
            this.toStateID = toStateID;
            this.condition = condition;
            this.priority = priority;
            this.name = string.IsNullOrWhiteSpace(name)
                ? $"{fromStateID} -> {toStateID}"
                : name;
        }

        /// <summary>
        /// 등록된 전이 조건 평가
        /// </summary>
        /// <param name="stateMachine">조건을 평가할 상태 머신</param>
        public bool Evaluate(IStateMachine stateMachine)
        {
            if (stateMachine == null)
                throw new ArgumentNullException(nameof(stateMachine));

            return condition?.Invoke(stateMachine) ?? true;
        }
    }
}
