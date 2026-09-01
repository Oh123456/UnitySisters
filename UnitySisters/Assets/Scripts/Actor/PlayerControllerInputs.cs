using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;
using PlayerActions = InputSystem_Actions.PlayerActions;

public partial class PlayerController
{
    private struct InputData
    {
        public InputAction inputAction;
        public System.Action<CallbackContext> performed;
        public System.Action<CallbackContext> canceled;
        public System.Action<CallbackContext> started;
    }


    private PlayerActions playerActions;

    private void EnableInputSystems()
    {
        EnableInputSystem(playerActions.Move, MovePerformed, MoveCanceled);
        EnableInputSystem(playerActions.Jump, JumpPerformed);
        EnableInputSystem(playerActions.ToggleMouseCursor, ToggleMouseCursorPerformed);
        EnableInputSystem(playerActions.Look);
        EnableInputSystem(playerActions.Attack, AttackPerformed);
    }

    private void DisableInputSystems()
    {
        while (inputDatas.Count > 0)
        {
            DisableInputSystem(inputDatas.Dequeue());
        }
    }

    private void InitializeInputSystem()
    {
        PlayerInputSystem playerInputSystem = InputManager.Instance.GetPlayerInputSystem();
        playerActions = playerInputSystem.GetInputAction();
    }


    private void EnableInputSystem(InputAction inputAction, System.Action<CallbackContext> performed = null, System.Action<CallbackContext> canceled = null, System.Action<CallbackContext> started = null)
    {
        inputAction.Enable();
        if (started != null)
            inputAction.started += started;
        if (performed != null)
            inputAction.performed += performed;
        if (canceled != null)
            inputAction.canceled += canceled;

        inputDatas.Enqueue(new InputData()
        {
            inputAction = inputAction,
            started = started,
            canceled = canceled,
            performed = performed
        });
    }

    private void DisableInputSystem(InputData inputData)
    {
        inputData.inputAction.Disable();
        inputData.inputAction.started -= inputData.started;
        inputData.inputAction.performed -= inputData.performed;
        inputData.inputAction.canceled -= inputData.canceled;
    }



    #region KeyCallbacks
    private void MovePerformed(CallbackContext callbackContext)
    {
        characterCommand.movementCommand.moveInput = callbackContext.ReadValue<Vector2>();
    }

    private void MoveCanceled(CallbackContext obj)
    {
        characterCommand.movementCommand.moveInput = Vector2.zero;
    }

    private void ToggleMouseCursorPerformed(CallbackContext callbackContext)
    {
        showMouseCursor = !showMouseCursor;
        UpdateMouseCursor();
    }
    private void JumpPerformed(CallbackContext callbackContext)
    {
        characterCommand.movementCommand.isJumpButton = true;
    }

    private void AttackPerformed(CallbackContext callbackContext)
    {
        characterCommand.isAttackButton = true;
    }

    #endregion
}
