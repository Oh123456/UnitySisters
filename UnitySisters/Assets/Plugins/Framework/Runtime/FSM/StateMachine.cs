using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace UnityFramework.FSM
{
    public sealed class StateMachine : IStateMachine
    {
        private readonly object owner;
        private readonly FSMData sourceData;
        private readonly Dictionary<int, State> states;
        private readonly List<StateTransition> transitions;
        private readonly Dictionary<int, List<StateTransition>> outgoingTransitions;
        private readonly ReadOnlyDictionary<int, State> readOnlyStates;
        private readonly ReadOnlyCollection<StateTransition> readOnlyTransitions;
        private readonly Dictionary<int, int> parameterIndices;
        private readonly FSMParameterType[] parameterTypes;
        private readonly bool[] boolParameters;
        private readonly int[] intParameters;
        private readonly float[] floatParameters;
        private readonly IFSMParameterBinder parameterBinder;

        private State currentState;
        private StateTransition pendingTransition;
        private float pendingTransitionElapsedTime;
        private int defaultStateID;
        private bool isRunning;

        public State CurrentState => this.currentState;

        public event Action<StateChangedEvent> StateChanged;
        public event Action<TransitionEvaluatedEvent> TransitionEvaluated;

        /// <summary>
        /// 상태 머신 소유자 설정
        /// </summary>
        /// <param name="owner">상태에서 사용할 소유 객체</param>
        public StateMachine(object owner)
            : this(owner, null, null, null)
        {
        }

        /// <summary>
        /// FSMData에서 생성될 때 원본 정의를 함께 보관하는 내부 생성자
        /// </summary>
        internal StateMachine(
            object owner,
            FSMData sourceData,
            IReadOnlyList<FSMParameterData> parameters,
            IFSMParameterBinder parameterBinder)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.sourceData = sourceData;
            if (sourceData != null && sourceData.GetUsesFieldParameterBinding())
            {
                // 별도 바인더가 없으면 기존 방식대로 Owner에서 Parameter를 읽는다.
                this.parameterBinder = parameterBinder ?? owner as IFSMParameterBinder;
                if (this.parameterBinder == null)
                    throw new InvalidOperationException(
                        $"FSMData '{sourceData.name}' requires a generated FSM Parameter binder, " +
                        $"but no binder was supplied and owner '{owner.GetType().FullName}' " +
                        "does not implement IFSMParameterBinder.");
            }
            int stateCapacity = sourceData?.States.Count ?? 0;
            int transitionCapacity = sourceData?.Transitions.Count ?? 0;
            this.states = new Dictionary<int, State>(stateCapacity);
            this.transitions = new List<StateTransition>(transitionCapacity);
            this.outgoingTransitions = new Dictionary<int, List<StateTransition>>(stateCapacity);
            this.readOnlyStates = new ReadOnlyDictionary<int, State>(this.states);
            this.readOnlyTransitions = this.transitions.AsReadOnly();

            int parameterCount = parameters?.Count ?? 0;
            this.parameterIndices = new Dictionary<int, int>(parameterCount);
            this.parameterTypes = parameterCount > 0
                ? new FSMParameterType[parameterCount]
                : Array.Empty<FSMParameterType>();
            this.boolParameters = parameterCount > 0
                ? new bool[parameterCount]
                : Array.Empty<bool>();
            this.intParameters = parameterCount > 0
                ? new int[parameterCount]
                : Array.Empty<int>();
            this.floatParameters = parameterCount > 0
                ? new float[parameterCount]
                : Array.Empty<float>();

            for (int i = 0; i < parameterCount; i++)
            {
                FSMParameterData parameter = parameters[i];
                if (parameter == null)
                    throw new InvalidOperationException("FSMData contains a null parameter.");

                this.parameterIndices.Add(parameter.GetID(), i);
                this.parameterTypes[i] = parameter.GetParameterType();
                this.boolParameters[i] = parameter.GetDefaultBoolValue();
                this.intParameters[i] = parameter.GetDefaultIntValue();
                this.floatParameters[i] = parameter.GetDefaultFloatValue();
            }
        }

        /// <summary>
        /// 상태 머신 실행 여부 반환
        /// </summary>
        public bool GetIsRunning() => this.isRunning;

        /// <summary>
        /// 현재 상태 ID 반환
        /// </summary>
        public int? GetCurrentStateID() => this.currentState?.ID;

        /// <summary>
        /// 등록된 상태 목록 반환
        /// </summary>
        public IReadOnlyDictionary<int, State> GetStates() => this.readOnlyStates;

        /// <summary>
        /// 등록된 전이 목록 반환
        /// </summary>
        public IReadOnlyList<StateTransition> GetTransitions() => this.readOnlyTransitions;

        public StateTransition GetPendingTransition() => this.pendingTransition;

        public float GetPendingTransitionRemainingTime()
        {
            if (this.pendingTransition == null)
                return 0.0f;

            return Mathf.Max(
                0.0f,
                this.pendingTransition.Delay - this.pendingTransitionElapsedTime);
        }

        /// <summary>
        /// 상태 머신 소유자 반환
        /// </summary>
        public object GetOwner() => this.owner;

        /// <summary>
        /// 이 상태 머신을 생성한 FSMData 반환
        /// </summary>
        public FSMData GetSourceData() => this.sourceData;

        /// <summary>
        /// 상태 머신 소유자를 지정한 타입으로 반환
        /// </summary>
        public T GetOwner<T>() where T : class => this.owner as T;

        /// <summary>
        /// 상태 추가
        /// </summary>
        /// <param name="state">추가할 상태</param>
        public void AddState(State state)
        {
            EnsureNotRunning();

            if (state == null)
                throw new ArgumentNullException(nameof(state));
            state.ValidateInitialization();
            if (states.ContainsKey(state.ID))
                throw new ArgumentException($"A state with ID {state.ID} is already registered.", nameof(state));

            states.Add(state.ID, state);
            if (!this.outgoingTransitions.ContainsKey(state.ID))
                this.outgoingTransitions.Add(state.ID, new List<StateTransition>());
        }

        /// <summary>
        /// 상태와 해당 상태에 연결된 전이 제거
        /// </summary>
        /// <param name="id">제거할 상태 ID</param>
        public bool RemoveState(int id)
        {
            EnsureNotRunning();

            if (!states.Remove(id, out State removedState))
                return false;

            // 캡처 람다와 Predicate 할당 없이 연결된 전이를 역순으로 제거한다.
            for (int i = transitions.Count - 1; i >= 0; i--)
            {
                StateTransition transition = transitions[i];
                if (transition.FromStateID == id || transition.ToStateID == id)
                    transitions.RemoveAt(i);
            }
            this.outgoingTransitions.Remove(id);
            foreach (List<StateTransition> stateTransitions in this.outgoingTransitions.Values)
            {
                for (int i = stateTransitions.Count - 1; i >= 0; i--)
                {
                    StateTransition transition = stateTransitions[i];
                    if (transition.FromStateID == id || transition.ToStateID == id)
                        stateTransitions.RemoveAt(i);
                }
            }
            return true;
        }

        /// <summary>
        /// 상태 사이의 전이 추가
        /// </summary>
        /// <param name="transition">추가할 전이</param>
        public void AddTransition(StateTransition transition)
        {
            EnsureNotRunning();

            if (transition == null)
                throw new ArgumentNullException(nameof(transition));

            transitions.Add(transition);
            if (!this.outgoingTransitions.TryGetValue(
                    transition.FromStateID,
                    out List<StateTransition> stateTransitions))
            {
                stateTransitions = new List<StateTransition>();
                this.outgoingTransitions.Add(transition.FromStateID, stateTransitions);
            }

            stateTransitions.Add(transition);
        }

        /// <summary>
        /// 정의를 검증하고 지정한 상태에서 상태 머신 시작
        /// </summary>
        /// <param name="initialStateID">시작 상태 ID</param>
        public void Start(int initialStateID)
        {
            EnsureNotRunning();
            ValidateDefinition(initialStateID);

            defaultStateID = initialStateID;
            currentState = states[defaultStateID];
            this.isRunning = true;
            SyncBoundParameters();
            currentState.Enter(owner);
            FSMDebugRegistry.Register(this);
            StateChanged?.Invoke(new StateChangedEvent(null, currentState.ID, StateChangeReason.Start));
        }

        /// <summary>
        /// 현재 상태 업데이트
        /// </summary>
        public void Update()
        {
            Update(Time.deltaTime);
        }

        public void Update(float deltaTime)
        {
            EnsureRunning();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0.0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            currentState.Update(owner);
            SyncBoundParameters();
            EvaluateAutomaticTransitions(deltaTime);
        }

        /// <summary>
        /// 지정한 상태로 전환 요청
        /// </summary>
        /// <param name="id">전환할 상태 ID</param>
        public void ChangeState(int id)
        {
            TryChangeState(id, out _);
        }

        /// <summary>
        /// 전이 조건과 우선순위를 검사한 후 상태 전환 시도
        /// </summary>
        /// <param name="id">전환할 상태 ID</param>
        /// <param name="result">전환 성공 또는 실패 원인</param>
        public bool TryChangeState(int id, out StateChangeResult result)
        {
            if (!this.isRunning)
            {
                result = StateChangeResult.NotRunning;
                RaiseTransitionEvaluated(id, null, result);
                return false;
            }

            if (currentState.ID == id)
            {
                result = StateChangeResult.AlreadyCurrent;
                RaiseTransitionEvaluated(id, null, result);
                return false;
            }

            if (!states.ContainsKey(id))
            {
                result = StateChangeResult.TargetNotFound;
                RaiseTransitionEvaluated(id, null, result);
                return false;
            }

            SyncBoundParameters();

            StateTransition selectedTransition = FindPassingTransition(
                id,
                FSMTransitionMode.Manual,
                out StateTransition highestPriorityCandidate,
                out bool transitionExists);

            if (selectedTransition == null)
            {
                result = transitionExists
                    ? StateChangeResult.ConditionFailed
                    : StateChangeResult.TransitionNotFound;

                // 뷰어가 실패한 연결선을 표시할 수 있도록 가장 높은 우선순위의 후보를 전달한다.
                RaiseTransitionEvaluated(id, highestPriorityCandidate, result);
                return false;
            }

            result = RequestTransition(selectedTransition);
            return result == StateChangeResult.Success || result == StateChangeResult.Pending;
        }

        public bool ForceChangeState(int id)
        {
            EnsureRunning();
            CancelPendingTransition();
            if (this.currentState.ID == id || !this.states.ContainsKey(id))
                return false;

            ApplyStateChange(id, StateChangeReason.Forced);
            return true;
        }

        public void SetBool(int parameterID, bool value)
        {
            int index = GetBooleanParameterIndex(parameterID);
            this.boolParameters[index] = value;
        }

        public bool GetBool(int parameterID)
        {
            int index = GetBooleanParameterIndex(parameterID);
            return this.boolParameters[index];
        }

        public void SetInt(int parameterID, int value)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Int);
            this.intParameters[index] = value;
        }

        public int GetInt(int parameterID)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Int);
            return this.intParameters[index];
        }

        public void SetFloat(int parameterID, float value)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Float);
            this.floatParameters[index] = value;
        }

        public float GetFloat(int parameterID)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Float);
            return this.floatParameters[index];
        }

        public void SetTrigger(int parameterID)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Trigger);
            this.boolParameters[index] = true;
        }

        public void ResetTrigger(int parameterID)
        {
            int index = GetParameterIndex(parameterID, FSMParameterType.Trigger);
            this.boolParameters[index] = false;
        }

        internal bool GetBoolByIndex(int parameterIndex) => this.boolParameters[parameterIndex];
        internal int GetIntByIndex(int parameterIndex) => this.intParameters[parameterIndex];
        internal float GetFloatByIndex(int parameterIndex) => this.floatParameters[parameterIndex];

        internal void ResetTriggerByIndex(int parameterIndex)
        {
            this.boolParameters[parameterIndex] = false;
        }

        /// <summary>
        /// 생성 코드가 Owner 필드를 직접 읽어 Parameter 배열에 반영한다.
        /// </summary>
        private void SyncBoundParameters()
        {
            this.parameterBinder?.SyncFSMParameters(this);
        }

        /// <summary>
        /// 현재 상태를 종료하고 시작 상태로 복귀
        /// </summary>
        public void ResetState()
        {
            EnsureRunning();
            CancelPendingTransition();

            int previousStateID = currentState.ID;
            currentState.Exit(owner);
            currentState = states[defaultStateID];
            currentState.Enter(owner);
            StateChanged?.Invoke(new StateChangedEvent(previousStateID, currentState.ID, StateChangeReason.Reset));
        }

        /// <summary>
        /// 현재 상태에서 나가는 자동 전이만 평가하고 한 프레임에 하나의 상태 변경만 허용한다.
        /// </summary>
        private void EvaluateAutomaticTransitions(float deltaTime)
        {
            if (this.pendingTransition != null &&
                this.pendingTransition.CancelWhenConditionFails &&
                !this.pendingTransition.Evaluate(this))
            {
                CancelPendingTransition();
            }

            StateTransition selectedTransition = FindPassingTransition(
                null,
                FSMTransitionMode.Automatic,
                out _,
                out _);

            if (selectedTransition != null)
                RequestTransition(selectedTransition, false);

            if (this.pendingTransition == null)
                return;

            this.pendingTransitionElapsedTime += deltaTime;
            if (this.pendingTransitionElapsedTime < this.pendingTransition.Delay)
                return;

            StateTransition completedTransition = this.pendingTransition;
            ClearPendingTransition();
            RaiseTransitionEvaluated(
                completedTransition.ToStateID,
                completedTransition,
                StateChangeResult.Success);
            ApplyTransition(completedTransition);
        }

        /// <summary>
        /// 즉시 전이는 바로 적용하고 지연 전이는 우선순위 규칙에 따라 Pending으로 등록한다.
        /// </summary>
        private StateChangeResult RequestTransition(
            StateTransition transition,
            bool raiseBlockedEvent = true)
        {
            if (this.pendingTransition != null)
            {
                if (ReferenceEquals(this.pendingTransition, transition))
                    return StateChangeResult.Pending;

                if (transition.Priority <= this.pendingTransition.Priority)
                {
                    if (raiseBlockedEvent)
                    {
                        RaiseTransitionEvaluated(
                            transition.ToStateID,
                            transition,
                            StateChangeResult.PendingBlocked);
                    }
                    return StateChangeResult.PendingBlocked;
                }

                CancelPendingTransition();
            }

            if (transition.Delay <= 0.0f)
            {
                RaiseTransitionEvaluated(
                    transition.ToStateID,
                    transition,
                    StateChangeResult.Success);
                ApplyTransition(transition);
                return StateChangeResult.Success;
            }

            this.pendingTransition = transition;
            this.pendingTransitionElapsedTime = 0.0f;
            RaiseTransitionEvaluated(
                transition.ToStateID,
                transition,
                StateChangeResult.Pending);
            return StateChangeResult.Pending;
        }

        /// <summary>
        /// 취소된 전이의 Trigger가 이후 상태에서 늦게 발동하지 않도록 함께 정리한다.
        /// </summary>
        private void CancelPendingTransition()
        {
            if (this.pendingTransition == null)
                return;

            StateTransition cancelledTransition = this.pendingTransition;
            ClearPendingTransition();
            cancelledTransition.ConsumeTriggers(this);
            RaiseTransitionEvaluated(
                cancelledTransition.ToStateID,
                cancelledTransition,
                StateChangeResult.PendingCancelled);
        }

        private void ClearPendingTransition()
        {
            this.pendingTransition = null;
            this.pendingTransitionElapsedTime = 0.0f;
        }

        private StateTransition FindPassingTransition(
            int? targetStateID,
            FSMTransitionMode mode,
            out StateTransition highestPriorityCandidate,
            out bool transitionExists)
        {
            StateTransition selectedTransition = null;
            highestPriorityCandidate = null;
            transitionExists = false;

            if (!this.outgoingTransitions.TryGetValue(
                    this.currentState.ID,
                    out List<StateTransition> stateTransitions))
                return null;

            // 현재 상태의 전이만 순회하고 조건은 우선순위 선택 전에 AND로 단축 평가한다.
            for (int i = 0; i < stateTransitions.Count; i++)
            {
                StateTransition candidate = stateTransitions[i];
                if (candidate.GetMode() != mode ||
                    (targetStateID.HasValue && candidate.ToStateID != targetStateID.Value))
                    continue;

                transitionExists = true;
                if (highestPriorityCandidate == null ||
                    candidate.Priority > highestPriorityCandidate.Priority)
                    highestPriorityCandidate = candidate;

                if (!candidate.Evaluate(this))
                    continue;

                if (selectedTransition == null || candidate.Priority > selectedTransition.Priority)
                    selectedTransition = candidate;
            }

            return selectedTransition;
        }

        private void ApplyTransition(StateTransition transition)
        {
            transition.ConsumeTriggers(this);
            ApplyStateChange(transition.ToStateID, StateChangeReason.Transition);
        }

        private void ApplyStateChange(int targetStateID, StateChangeReason reason)
        {
            int previousStateID = this.currentState.ID;
            this.currentState.Exit(this.owner);
            this.currentState = this.states[targetStateID];
            this.currentState.Enter(this.owner);
            StateChanged?.Invoke(new StateChangedEvent(
                previousStateID,
                targetStateID,
                reason));
        }

        private int GetBooleanParameterIndex(int parameterID)
        {
            if (!this.parameterIndices.TryGetValue(parameterID, out int index))
                throw new ArgumentException(
                    $"Parameter ID {parameterID} is not registered.",
                    nameof(parameterID));

            FSMParameterType actualType = this.parameterTypes[index];
            if (actualType == FSMParameterType.Bool || actualType == FSMParameterType.Trigger)
                return index;

            throw new InvalidOperationException(
                $"Parameter ID {parameterID} is '{actualType}', not Bool or Trigger.");
        }

        private int GetParameterIndex(int parameterID, FSMParameterType expectedType)
        {
            if (!this.parameterIndices.TryGetValue(parameterID, out int index))
                throw new ArgumentException(
                    $"Parameter ID {parameterID} is not registered.",
                    nameof(parameterID));

            FSMParameterType actualType = this.parameterTypes[index];
            if (actualType == expectedType)
                return index;

            throw new InvalidOperationException(
                $"Parameter ID {parameterID} is '{actualType}', not the requested type.");
        }

        /// <summary>
        /// 시작 전에 상태와 전이 정의가 올바른지 검사
        /// </summary>
        private void ValidateDefinition(int initialStateID)
        {
            if (states.Count == 0)
                throw new InvalidOperationException("The state machine has no states.");
            if (!states.ContainsKey(initialStateID))
                throw new InvalidOperationException($"The initial state ID {initialStateID} is not registered.");

            // 실행 중 잘못된 상태 ID를 만나는 일을 막기 위해 시작 전에 전체 연결을 검사한다.
            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition transition = transitions[i];
                if (!states.ContainsKey(transition.FromStateID))
                    throw new InvalidOperationException($"Transition '{transition.Name}' has an unknown source state ID {transition.FromStateID}.");
                if (!states.ContainsKey(transition.ToStateID))
                    throw new InvalidOperationException($"Transition '{transition.Name}' has an unknown target state ID {transition.ToStateID}.");
            }
        }

        /// <summary>
        /// 전이 시도 결과 이벤트 전달
        /// </summary>
        private void RaiseTransitionEvaluated(
            int requestedStateID,
            StateTransition transition,
            StateChangeResult result)
        {
            TransitionEvaluated?.Invoke(new TransitionEvaluatedEvent(
                GetCurrentStateID(),
                requestedStateID,
                transition,
                result));
        }

        /// <summary>
        /// 실행 이후 정의 변경 방지
        /// </summary>
        private void EnsureNotRunning()
        {
            if (this.isRunning)
                throw new InvalidOperationException("The state machine definition cannot be changed after it starts.");
        }

        /// <summary>
        /// 시작되지 않은 상태 머신 사용 방지
        /// </summary>
        private void EnsureRunning()
        {
            if (!this.isRunning)
                throw new InvalidOperationException("The state machine has not started.");
        }
    }
}
