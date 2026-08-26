using System;

namespace UnityFramework.FSM
{
    public abstract class State
    {
        protected int id = int.MinValue;
        protected string name;
        private bool isInitialized;

        public int ID => this.id;
        public string Name => this.name;

        /// <summary>
        /// 상태 쉽게 생성하는 함수
        /// </summary>
        /// <typeparam name="T">상태 타입</typeparam>
        /// <param name="id">상태 id</param>
        public static T CreateState<T>(int id, string name = null) where T : State, new()
        {
            T state = new T();
            state.Initialize(id, name);
            return state;
        }

        ///// <summary>
        ///// 상태 ID와 표시 이름 설정
        ///// </summary>
        ///// <param name="id">상태 식별 ID</param>
        ///// <param name="name">상태 표시 이름</param>
        //protected State(int id, string name = null)
        //{
        //    Initialize(id, name);
        //}

        private void Initialize(int id, string name = null)
        {
            if (this.isInitialized)
                throw new InvalidOperationException($"State '{GetType().FullName}' is already initialized.");

            this.id = id;
            this.name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
            this.isInitialized = true;
        }

        /// <summary>
        /// 정적 생성 함수를 거치지 않은 상태가 런타임에 등록되는 것을 방지
        /// </summary>
        internal void ValidateInitialization()
        {
            if (!this.isInitialized)
                throw new InvalidOperationException(
                    $"State '{GetType().FullName}' is not initialized. " +
                    "Create it with State.CreateState<T>().");
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
