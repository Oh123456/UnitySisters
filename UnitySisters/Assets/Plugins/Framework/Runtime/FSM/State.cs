using System;

namespace UnityFramework.FSM
{
    public abstract class State
    {
        protected int id;
        protected string name;

        public int ID => this.id;
        public string Name => this.name;

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
        /// 상태 진입 시 호출
        /// </summary>
        public virtual void Enter<T>(T owner) where T : class { }

        /// <summary>
        /// 현재 상태가 활성화된 동안 호출
        /// </summary>
        public virtual void Update<T>(T owner) where T : class { }

        /// <summary>
        /// 상태 종료 시 호출
        /// </summary>
        public virtual void Exit<T>(T owner) where T : class { }
    }
}
