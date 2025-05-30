using CoreSystem.Components;
using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace CoreSystem.Controllers
{

    public class PlayerController : BaseController
    {
        private MovementData movementData;
        

        public override void Dispose()
        {
            base.Dispose();
            movementData = null;
        }

        public override void SetControlPawn(Pawn pawn)
        {
            if (!(pawn is Character character))
                return;

            base.SetControlPawn(pawn);

            MovementComponent movementComponent = character.GetPureComponent<MovementComponent>();
            if (movementComponent == null)
                return;
            movementData = movementComponent.MovementData;
        }

        protected override void SetInputAction(IInputActionCollection2 inputActions)
        {
            InputDefaultData inputDefaultData = InputManager.Instance.InputDefaultData;
            InputAction move = inputActions.FindAction(inputDefaultData.move);

            if (move != null)
            {
                move.performed += MoveCharacter;
                move.Enable();
            }
        }

        protected override void ClearInputAction(IInputActionCollection2 inputActions)
        {
            InputDefaultData inputDefaultData = InputManager.Instance.InputDefaultData;
            InputAction move = inputActions.FindAction(inputDefaultData.move);

            if (move != null)
            {
                move.performed -= MoveCharacter;
                move.Disable();
            }
        }

        

        private void MoveCharacter(CallbackContext callbackContext)
        {
            movementData.SetMovementDirection(callbackContext.ReadValue<Vector2>());
        }

    }
}