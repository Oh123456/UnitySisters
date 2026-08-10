using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UnityFramework.FSM
{
    public sealed class StateMachine : IStateMachine
    {
        private readonly object owner;
        private readonly FSMData sourceData;
        private readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private readonly List<StateTransition> transitions = new List<StateTransition>();
        private readonly ReadOnlyDictionary<int, State> readOnlyStates;
        private readonly ReadOnlyCollection<StateTransition> readOnlyTransitions;

        private State currentState;
        private int defaultStateID;
        private bool isRunning;

        public bool IsRunning => this.isRunning;
        public int? CurrentStateID => this.currentState?.ID;
        public State CurrentState => this.currentState;
        public IReadOnlyDictionary<int, State> States => this.readOnlyStates;
        public IReadOnlyList<StateTransition> Transitions => this.readOnlyTransitions;

        public event Action<StateChangedEvent> StateChanged;
        public event Action<TransitionEvaluatedEvent> TransitionEvaluated;

        /// <summary>
        /// 상태 머신 소유자 설정
        /// </summary>
        /// <param name="owner">상태에서 사용할 소유 객체</param>
        public StateMachine(object owner)
            : this(owner, null)
        {
        }

        /// <summary>
        /// FSMData에서 생성될 때 원본 정의를 함께 보관하는 내부 생성자
        /// </summary>
        internal StateMachine(object owner, FSMData sourceData)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.sourceData = sourceData;
            this.readOnlyStates = new ReadOnlyDictionary<int, State>(this.states);
            this.readOnlyTransitions = this.transitions.AsReadOnly();
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
            if (states.ContainsKey(state.ID))
                throw new ArgumentException($"A state with ID {state.ID} is already registered.", nameof(state));

            state.AttachTo(this);
            states.Add(state.ID, state);
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

            removedState.DetachFrom(this);
            transitions.RemoveAll(transition => transition.FromStateID == id || transition.ToStateID == id);
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
            currentState.Enter();
            FSMDebugRegistry.Register(this);
            StateChanged?.Invoke(new StateChangedEvent(null, currentState.ID, StateChangeReason.Start));
        }

        /// <summary>
        /// 현재 상태 업데이트
        /// </summary>
        public void Update()
        {
            EnsureRunning();
            currentState.Update();
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

            StateTransition selectedTransition = null;
            StateTransition highestPriorityCandidate = null;
            bool transitionExists = false;

            // 같은 목적지로 향하는 전이 중 조건을 통과하고 우선순위가 가장 높은 전이를 선택한다.
            for (int i = 0; i < transitions.Count; i++)
            {
                StateTransition candidate = transitions[i];
                if (candidate.FromStateID != currentState.ID || candidate.ToStateID != id)
                    continue;

                transitionExists = true;
                if (highestPriorityCandidate == null || candidate.Priority > highestPriorityCandidate.Priority)
                    highestPriorityCandidate = candidate;

                if (!candidate.Evaluate(this))
                    continue;

                if (selectedTransition == null || candidate.Priority > selectedTransition.Priority)
                    selectedTransition = candidate;
            }

            if (selectedTransition == null)
            {
                result = transitionExists
                    ? StateChangeResult.ConditionFailed
                    : StateChangeResult.TransitionNotFound;

                // 뷰어가 실패한 연결선을 표시할 수 있도록 가장 높은 우선순위의 후보를 전달한다.
                RaiseTransitionEvaluated(id, highestPriorityCandidate, result);
                return false;
            }

            int previousStateID = currentState.ID;
            result = StateChangeResult.Success;
            RaiseTransitionEvaluated(id, selectedTransition, result);

            currentState.Exit();
            currentState = states[id];
            currentState.Enter();

            StateChanged?.Invoke(new StateChangedEvent(previousStateID, id, StateChangeReason.Transition));
            return true;
        }

        /// <summary>
        /// 현재 상태를 종료하고 시작 상태로 복귀
        /// </summary>
        public void ResetState()
        {
            EnsureRunning();

            int previousStateID = currentState.ID;
            currentState.Exit();
            currentState = states[defaultStateID];
            currentState.Enter();
            StateChanged?.Invoke(new StateChangedEvent(previousStateID, currentState.ID, StateChangeReason.Reset));
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
                CurrentStateID,
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
