using System;

namespace UnityFramework.FSM
{
    public abstract class State
    {
        protected int id;
        protected string name;
        protected IStateMachine ownerMachine;

        public int ID => this.id;
        public string Name => this.name;
        public IStateMachine OwnerMachine => this.ownerMachine;

        /// <summary>
        /// 상태 ID와 표시 이름 설정
        /// </summary>
        /// <param name="id">상태 식별 ID</param>
        /// <param name="name">상태 표시 이름</param>
        protected State(int id, string name = null)
        {
            this.id = id;
            this.name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
        }

        /// <summary>
        /// 상태를 소유할 상태 머신 연결
        /// </summary>
        internal void AttachTo(IStateMachine stateMachine)
        {
            if (this.ownerMachine != null && !ReferenceEquals(this.ownerMachine, stateMachine))
                throw new InvalidOperationException($"State '{this.name}' is already attached to another state machine.");

            this.ownerMachine = stateMachine;
        }

        /// <summary>
        /// 현재 상태 머신과의 연결 해제
        /// </summary>
        internal void DetachFrom(IStateMachine stateMachine)
        {
            if (ReferenceEquals(this.ownerMachine, stateMachine))
                this.ownerMachine = null;
        }

        /// <summary>
        /// 상태 진입 시 호출
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// 현재 상태가 활성화된 동안 호출
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 상태 종료 시 호출
        /// </summary>
        public virtual void Exit() { }
    }
}
