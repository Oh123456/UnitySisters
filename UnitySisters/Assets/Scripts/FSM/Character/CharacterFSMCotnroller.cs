using UnityEngine;
using UnityFramework.FSM;
using UnitySisters.FSM.States;
using UnitySisters.Model;

namespace FSM
{

    [RequireComponent(typeof(Character))]
    public partial class CharacterFSMCotnroller : FSMController
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
    }

}
