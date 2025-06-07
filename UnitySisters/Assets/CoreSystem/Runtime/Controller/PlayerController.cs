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
        private CinemachineCameraData cinemachineCameraData;

        public override void Dispose()
        {
            base.Dispose();
            movementData = null;
            cinemachineCameraData = null;
        }

        public override void SetControlPawn(Pawn pawn)
        {
            if (!(pawn is Character character))
                return;

            base.SetControlPawn(pawn);

            MovementComponent movementComponent = character.GetPureComponent<MovementComponent>();
            if (movementComponent != null)                   
                movementData = movementComponent.MovementData;

            // TODO:: 분할 해야하는게.. 캐릭터클래스를 어떻게 분할하냐..
            CinemachineCameraComponent cinemachineCameraComponent = character.GetPureComponent<CinemachineCameraComponent>();
            if (cinemachineCameraComponent != null)
                cinemachineCameraData = cinemachineCameraComponent.CinemachineCameraData;
        }

        protected override void SetInputAction(IInputActionCollection2 inputActions)
        {
            InputDefaultData inputDefaultData = InputManager.Instance.InputDefaultData;
            SetMoveInput(inputActions, in inputDefaultData);
            SetLookInput(inputActions, in inputDefaultData);
        }


        protected override void ClearInputAction(IInputActionCollection2 inputActions)
        {
            InputDefaultData inputDefaultData = InputManager.Instance.InputDefaultData;
            RemoveMoveInput(inputActions, in inputDefaultData);
            RemoveLookInput(inputActions, in inputDefaultData);
        }

        #region Move
        private void SetMoveInput(IInputActionCollection2 inputActions, in InputDefaultData inputDefaultData)
        {
            InputAction move = inputActions.FindAction(inputDefaultData.move);

            if (move != null)
            {
                move.performed += MoveCharacter;
                move.Enable();
            }
        }
        private void RemoveMoveInput(IInputActionCollection2 inputActions, in InputDefaultData inputDefaultData)
        {
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
        #endregion

        #region Look
        private void SetLookInput(IInputActionCollection2 inputActions, in InputDefaultData inputDefaultData)
        {
            InputAction lookAt = inputActions.FindAction(inputDefaultData.lookAt);

            if (lookAt != null)
            {
                lookAt.performed += LookAtCharacter;
                lookAt.Enable();
            }
        }
        private void RemoveLookInput(IInputActionCollection2 inputActions, in InputDefaultData inputDefaultData)
        {
            InputAction lookAt = inputActions.FindAction(inputDefaultData.lookAt);

            if (lookAt != null)
            {
                lookAt.performed -= LookAtCharacter;
                lookAt.Enable();
            }
        }

        private void LookAtCharacter(CallbackContext callbackContext)
        {
            if (Mouse.current.rightButton.isPressed)
                cinemachineCameraData.SetRotationValue(callbackContext.ReadValue<Vector2>());
            else
                cinemachineCameraData.SetRotationValue(Vector2.zero);
        }

        #endregion
    }
}