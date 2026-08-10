using System;
using System.Collections.Generic;

namespace UnityFramework.FSM
{
    public interface IStateMachine
    {
        event Action<StateChangedEvent> StateChanged;
        event Action<TransitionEvaluatedEvent> TransitionEvaluated;

        /// <summary>
        /// 상태 머신이 시작되었는지 반환
        /// </summary>
        bool GetIsRunning();

        /// <summary>
        /// 현재 실행 중인 상태 ID 반환
        /// </summary>
        int? GetCurrentStateID();

        /// <summary>
        /// 등록된 상태 목록 반환
        /// </summary>
        IReadOnlyDictionary<int, State> GetStates();

        /// <summary>
        /// 등록된 전이 목록 반환
        /// </summary>
        IReadOnlyList<StateTransition> GetTransitions();

        /// <summary>
        /// 현재 상태 업데이트
        /// </summary>
        void Update();

        /// <summary>
        /// 지정한 상태로 전환 요청
        /// </summary>
        /// <param name="id">전환할 상태 ID</param>
        void ChangeState(int id);

        /// <summary>
        /// 지정한 상태로 전환을 시도하고 결과 반환
        /// </summary>
        /// <param name="id">전환할 상태 ID</param>
        /// <param name="result">전환 결과</param>
        bool TryChangeState(int id, out StateChangeResult result);

        /// <summary>
        /// 상태 머신 소유자 반환
        /// </summary>
        object GetOwner();

        /// <summary>
        /// 이 상태 머신을 생성한 FSMData 반환
        /// </summary>
        FSMData GetSourceData();

        /// <summary>
        /// 상태 머신 소유자를 지정한 타입으로 반환
        /// </summary>
        T GetOwner<T>() where T : class;

        /// <summary>
        /// 시작 상태로 초기화
        /// </summary>
        void ResetState();
    }
}
