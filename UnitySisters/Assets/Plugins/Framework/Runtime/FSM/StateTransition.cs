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
        Forced,
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
        private static readonly StateTransitionCondition[] emptyConditions =
            Array.Empty<StateTransitionCondition>();

        private readonly StateTransitionCondition[] conditions;
        private readonly int fromStateID;
        private readonly int toStateID;
        private readonly string name;
        private readonly int priority;
        private readonly FSMTransitionMode mode;

        public int FromStateID => this.fromStateID;
        public int ToStateID => this.toStateID;
        public string Name => this.name;
        public int Priority => this.priority;
        public bool HasCondition => this.conditions.Length > 0;

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
            this.conditions = condition == null
                ? emptyConditions
                : new[] { StateTransitionCondition.CreateCustom(condition, true) };
            this.mode = FSMTransitionMode.Manual;
            this.priority = priority;
            this.name = string.IsNullOrWhiteSpace(name)
                ? $"{fromStateID} -> {toStateID}"
                : name;
        }

        internal StateTransition(
            int fromStateID,
            int toStateID,
            StateTransitionCondition[] conditions,
            FSMTransitionMode mode,
            int priority = 0,
            string name = null)
        {
            this.fromStateID = fromStateID;
            this.toStateID = toStateID;
            this.conditions = conditions ?? emptyConditions;
            this.mode = mode;
            this.priority = priority;
            this.name = string.IsNullOrWhiteSpace(name)
                ? $"{fromStateID} -> {toStateID}"
                : name;
        }

        public FSMTransitionMode GetMode() => this.mode;
        public int GetConditionCount() => this.conditions.Length;

        /// <summary>
        /// 등록된 전이 조건 평가
        /// </summary>
        /// <param name="stateMachine">조건을 평가할 상태 머신</param>
        public bool Evaluate(IStateMachine stateMachine)
        {
            if (stateMachine == null)
                throw new ArgumentNullException(nameof(stateMachine));

            // 조건은 직렬화된 순서를 유지하며 AND로 단축 평가한다.
            for (int i = 0; i < this.conditions.Length; i++)
            {
                if (!this.conditions[i].Evaluate(stateMachine))
                    return false;
            }

            return true;
        }

        internal void ConsumeTriggers(IStateMachine stateMachine)
        {
            for (int i = 0; i < this.conditions.Length; i++)
                this.conditions[i].ConsumeTrigger(stateMachine);
        }
    }

    internal readonly struct StateTransitionCondition
    {
        private readonly FSMConditionKind kind;
        private readonly FSMParameterType parameterType;
        private readonly FSMParameterComparison comparison;
        private readonly int parameterID;
        private readonly int parameterIndex;
        private readonly bool boolValue;
        private readonly int intValue;
        private readonly float floatValue;
        private readonly Func<IStateMachine, bool> customCondition;
        private readonly bool customExpectedResult;

        private StateTransitionCondition(
            FSMConditionKind kind,
            FSMParameterType parameterType,
            FSMParameterComparison comparison,
            int parameterID,
            int parameterIndex,
            bool boolValue,
            int intValue,
            float floatValue,
            Func<IStateMachine, bool> customCondition,
            bool customExpectedResult)
        {
            this.kind = kind;
            this.parameterType = parameterType;
            this.comparison = comparison;
            this.parameterID = parameterID;
            this.parameterIndex = parameterIndex;
            this.boolValue = boolValue;
            this.intValue = intValue;
            this.floatValue = floatValue;
            this.customCondition = customCondition;
            this.customExpectedResult = customExpectedResult;
        }

        public static StateTransitionCondition CreateParameter(
            FSMConditionData conditionData,
            FSMParameterType parameterType,
            int parameterIndex)
        {
            return new StateTransitionCondition(
                FSMConditionKind.Parameter,
                parameterType,
                conditionData.GetComparison(),
                conditionData.GetParameterID(),
                parameterIndex,
                conditionData.GetBoolValue(),
                conditionData.GetIntValue(),
                conditionData.GetFloatValue(),
                null,
                true);
        }

        public static StateTransitionCondition CreateCustom(
            Func<IStateMachine, bool> condition,
            bool expectedResult)
        {
            return new StateTransitionCondition(
                FSMConditionKind.Custom,
                FSMParameterType.Bool,
                FSMParameterComparison.Equal,
                0,
                -1,
                false,
                0,
                0.0f,
                condition,
                expectedResult);
        }

        public bool Evaluate(IStateMachine stateMachine)
        {
            if (this.kind == FSMConditionKind.Custom)
                return this.customCondition.Invoke(stateMachine) == this.customExpectedResult;

            switch (this.parameterType)
            {
                case FSMParameterType.Bool:
                case FSMParameterType.Trigger:
                    return CompareBool(
                        GetBoolValue(stateMachine),
                        this.boolValue);

                case FSMParameterType.Int:
                    return CompareInt(GetIntValue(stateMachine), this.intValue);

                case FSMParameterType.Float:
                    return CompareFloat(GetFloatValue(stateMachine), this.floatValue);

                default:
                    throw new InvalidOperationException(
                        $"Unsupported FSM parameter type '{this.parameterType}'.");
            }
        }

        public void ConsumeTrigger(IStateMachine stateMachine)
        {
            if (this.kind == FSMConditionKind.Parameter &&
                this.parameterType == FSMParameterType.Trigger)
            {
                if (stateMachine is StateMachine concreteMachine)
                    concreteMachine.ResetTriggerByIndex(this.parameterIndex);
                else
                    stateMachine.ResetTrigger(this.parameterID);
            }
        }

        private bool GetBoolValue(IStateMachine stateMachine)
        {
            return stateMachine is StateMachine concreteMachine
                ? concreteMachine.GetBoolByIndex(this.parameterIndex)
                : stateMachine.GetBool(this.parameterID);
        }

        private int GetIntValue(IStateMachine stateMachine)
        {
            return stateMachine is StateMachine concreteMachine
                ? concreteMachine.GetIntByIndex(this.parameterIndex)
                : stateMachine.GetInt(this.parameterID);
        }

        private float GetFloatValue(IStateMachine stateMachine)
        {
            return stateMachine is StateMachine concreteMachine
                ? concreteMachine.GetFloatByIndex(this.parameterIndex)
                : stateMachine.GetFloat(this.parameterID);
        }

        private bool CompareBool(bool currentValue, bool expectedValue)
        {
            if (this.comparison == FSMParameterComparison.Equal)
                return currentValue == expectedValue;
            if (this.comparison == FSMParameterComparison.NotEqual)
                return currentValue != expectedValue;

            return false;
        }

        private bool CompareInt(int currentValue, int expectedValue)
        {
            switch (this.comparison)
            {
                case FSMParameterComparison.Equal: return currentValue == expectedValue;
                case FSMParameterComparison.NotEqual: return currentValue != expectedValue;
                case FSMParameterComparison.Greater: return currentValue > expectedValue;
                case FSMParameterComparison.Less: return currentValue < expectedValue;
                case FSMParameterComparison.GreaterOrEqual: return currentValue >= expectedValue;
                case FSMParameterComparison.LessOrEqual: return currentValue <= expectedValue;
                default: return false;
            }
        }

        private bool CompareFloat(float currentValue, float expectedValue)
        {
            switch (this.comparison)
            {
                case FSMParameterComparison.Equal: return currentValue == expectedValue;
                case FSMParameterComparison.NotEqual: return currentValue != expectedValue;
                case FSMParameterComparison.Greater: return currentValue > expectedValue;
                case FSMParameterComparison.Less: return currentValue < expectedValue;
                case FSMParameterComparison.GreaterOrEqual: return currentValue >= expectedValue;
                case FSMParameterComparison.LessOrEqual: return currentValue <= expectedValue;
                default: return false;
            }
        }
    }
}
