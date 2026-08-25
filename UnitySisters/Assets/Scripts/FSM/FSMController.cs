using UnityEngine;
using UnityFramework.FSM;

namespace FSM
{
    public abstract class FSMController : MonoBehaviour
    {
        [SerializeField] protected FSMData fsmData;
        protected StateMachine stateMachine;

        private void OnEnable()
        {
            RegisterEvent();
        }

        private void OnDisable()
        {
            UnregisterEvent();
        }

        private void Update()
        {
            if (stateMachine == null)
                return;
            stateMachine.Update();
        }


        public int? GetCurrentStateID()
        {
            return stateMachine.GetCurrentStateID();
        }

        public void CreateStateMachine()
        {
            this.stateMachine = this.fsmData.CreateStateMachine(
                this,
                GetParameterBinder(),
                CreateState,
                CreateCondition);

            //this.stateMachine.StateChanged += OnStateChanged;
            //this.stateMachine.Start(this.fsmData.InitialStateID);
        }

        protected virtual void RegisterEvent()
        {

        }

        protected virtual void UnregisterEvent()
        {

        }

        /// <summary>
        /// 기본값은 Controller 자신이며, Model을 직접 바인딩할 때 재정의한다.
        /// </summary>
        protected virtual IFSMParameterBinder GetParameterBinder()
        {
            return this as IFSMParameterBinder;
        }

        public void StartStateMachine()
        {
            this.stateMachine.Start(this.fsmData.InitialStateID);
        }

        protected abstract State CreateState(FSMStateData stateData);

        protected abstract System.Func<IStateMachine, bool> CreateCondition(int conditionID);
    }

}
