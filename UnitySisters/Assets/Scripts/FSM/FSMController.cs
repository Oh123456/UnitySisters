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

        public void CreateStateMachine()
        {
            this.stateMachine = this.fsmData.CreateStateMachine(
                this,
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

        public void StartStateMachine()
        {
            this.stateMachine.Start(this.fsmData.InitialStateID);
        }

        protected abstract State CreateState(FSMStateData stateData);

        protected abstract System.Func<IStateMachine, bool> CreateCondition(int conditionID);
    }

}