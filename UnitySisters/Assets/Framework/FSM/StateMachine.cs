using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace UnityFramework.FSM
{    
    public struct StateMachineData
    {
        public object owner;
        public Dictionary<int, State> states;
        public State currentState;
        public int defaultID;
    }

    public abstract class StateMachine : IStateMachine
    {

        private StateMachineData stateMachineData = new StateMachineData()
        { 
            owner = null,
            states = new Dictionary<int, State>(),
            currentState = null,
            defaultID = 0,
        };

        public int CurrentID => stateMachineData.currentState == null ? stateMachineData.defaultID : stateMachineData.currentState.ID;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="owner">무조건 클래스 타입</param>
        /// <exception cref="System.InvalidOperationException">클래스 타입이 아닐경우 예외 발생함</exception>
        public StateMachine(object owner)
        {
            if (!owner.GetType().IsClass)
                throw new System.InvalidOperationException("클래스 타입이 아닙니다.");
            stateMachineData.owner = owner;
            SetStates();
            SetDefulatID(out stateMachineData.defaultID);
            ResetState();
        }

        /// <summary>
        /// 상태 세팅 초기화애서 호출이됨
        /// </summary>
        protected abstract void SetStates();

        /// <summary>
        /// 기본 상태 ID 세팅 생성자에서 호출됨
        /// </summary>
        /// <param name="defaultID"></param>
        protected abstract void SetDefulatID(out int defaultID);

        public object GetOwner() { return stateMachineData.owner; }
        public T GetOwner<T>() where T : class { return stateMachineData.owner as T; }

        /// <summary>
        /// 상태 추가
        /// </summary>
        /// <param name="id"> </param>
        /// <param name="state"></param>
        public void AddState(State state)
        {
            if (stateMachineData.states.TryAdd(state.ID, state))
                state.SetOwnerMachine(this);
        }


        /// <summary>
        /// 상태 삭제
        /// </summary>
        /// <param name="id"></param>
        public void RemoveState(int id)
        {
            if (stateMachineData.states.Remove(id,out State state))
                state.SetOwnerMachine(null);   
        }

        /// <summary>
        /// 기본 상태 세팅
        /// </summary>
        /// <param name="id">기본상태 ID</param>
        public void SetDefaultState(int id)
        {
            stateMachineData.defaultID = id;
            ResetState();
        }

        /// <summary>
        /// 기본 상태로 전환
        /// </summary>
        public void ResetState()
        {
            stateMachineData.currentState?.Exit();
            if (!stateMachineData.states.TryGetValue(stateMachineData.defaultID, out stateMachineData.currentState))
                return;
            stateMachineData.currentState.Enter();
        }        

        /// <summary>
        /// 상태 변환
        /// </summary>
        /// <param name="id"></param>
        public void ChangeState(int id)
        {
            // 같은 ID 제외
            if (CurrentID == id)
                return;

            // 현재 상태에서 변환 이 가능한지 검사
            if (!stateMachineData.currentState.ConditionChangeID(id))
                return;

            // 상태 존재 검사
            if (!stateMachineData.states.TryGetValue(id, out State nextState))
                return;

            stateMachineData.currentState?.Exit();
            stateMachineData.currentState = nextState;
            stateMachineData.currentState.Enter();
        }

        public void Update()
        {
            stateMachineData.currentState.Update();
        }

    }
}

