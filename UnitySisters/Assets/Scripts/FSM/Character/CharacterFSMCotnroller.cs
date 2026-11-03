using UnityEngine;
using UnityFramework.FSM;
using UnitySisters.FSM.States;
using UnitySisters.Model;

namespace FSM
{

    public interface ICharacterFSMCotnroller
    {
        void EnableInputBuffer();
        void DisableInputBuffer();
        bool IsInputBuffer { get; }
        bool IsCompletingState { get; }
        void SetInputBufferAllowed(bool allowed);
        void CancelInputBuffer();
        event System.Action InputBufferStarted;
        event System.Action InputBufferEnded;
        event System.Action InputBufferCancelled;
    }

    [RequireComponent(typeof(Character))]
    public partial class CharacterFSMCotnroller : FSMController , ICharacterFSMCotnroller
    {
        [FSMCondition]
        protected enum CharacterCondition
        {
        }

        [FSMStateID]
        public enum CharacterStateID
        {
            Idile = 0,
            Move = 1,
            Landing = 2,
            Falling = 3,
            Attack = 4,
        }

        private CharacterFSMModel characterFSMModel = new CharacterFSMModel();

        public CharacterFSMModel CharacterFSMModel => characterFSMModel;
        public bool IsInputBuffer { get => characterFSMModel.isInputBuffer; }
        public bool IsCompletingState { get => characterFSMModel.isCompletingState; }

        public event System.Action InputBufferStarted;
        public event System.Action InputBufferEnded;
        public event System.Action InputBufferCancelled;

        protected override System.Func<IStateMachine, bool> CreateCondition(int conditionID)
        {
            switch ((CharacterCondition)conditionID)
            {
                default:
                    throw new System.InvalidOperationException(
                        $"Unknown sample condition ID '{conditionID}'.");
            }
        }

        protected override State CreateState(FSMStateData stateData)
        {
            StateManager stateManager = StateManager.Instance;
            int id = stateData.ID;
            System.Type enumType = typeof(CharacterStateID);
            State state = null;
            StateKey stateKey;
            switch ((CharacterStateID)id)
            {
                case CharacterStateID.Idile:
                    {
                        stateKey = new StateKey(typeof(EmptyState), enumType, id);
                        stateManager.TryGetState<EmptyState>(stateKey, out state);
                        break;
                    }
                case CharacterStateID.Move:
                    {
                        stateKey = new StateKey(typeof(EmptyState), enumType, id);
                        stateManager.TryGetState<EmptyState>(stateKey, out state);
                        break;
                    }
                case CharacterStateID.Landing:
                    {
                        stateKey = new StateKey(typeof(EmptyState), enumType, id);
                        stateManager.TryGetState<EmptyState>(stateKey, out state);
                        break;
                    }
                case CharacterStateID.Falling:
                    {
                        stateKey = new StateKey(typeof(EmptyState), enumType, id);
                        stateManager.TryGetState<EmptyState>(stateKey, out state);
                        break;
                    }
                case CharacterStateID.Attack:
                    {
                        stateKey = new StateKey(typeof(AttackState), enumType, id);
                        stateManager.TryGetState<AttackState>(stateKey, out state);
                        break;
                    }
                default:
                    throw new System.InvalidOperationException(
                        $"Sample state ID {stateData.ID} has no behavior implementation.");
            }

            return state;
        }
        protected override IFSMParameterBinder GetParameterBinder()
        {
            return this.characterFSMModel as IFSMParameterBinder;
        }

        public override void SetDefaultState()
        {
            characterFSMModel.isCompletingState = true;
            try
            {
                stateMachine.ChangeState((int)CharacterStateID.Idile);
            }
            finally
            {
                characterFSMModel.isCompletingState = false;
            }
        }

        public void EnableInputBuffer()
        {
            if (!isActiveAndEnabled || !characterFSMModel.isInputBufferAllowed || IsInputBuffer)
                return;

            characterFSMModel.isInputBuffer = true;
            InputBufferStarted?.Invoke();
        }

        public void DisableInputBuffer()
        {
            if (!IsInputBuffer)
                return;

            characterFSMModel.isInputBuffer = false;
            InputBufferEnded?.Invoke();
        }

        public void SetInputBufferAllowed(bool allowed)
        {
            characterFSMModel.isInputBufferAllowed = allowed;
        }

        public void CancelInputBuffer()
        {
            characterFSMModel.isInputBuffer = false;
            InputBufferCancelled?.Invoke();
        }

        protected override void UnregisterEvent()
        {
            base.UnregisterEvent();
            CancelInputBuffer();
        }
    }

}
