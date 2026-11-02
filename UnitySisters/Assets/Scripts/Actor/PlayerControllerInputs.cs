using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnitySisters;
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
    private Queue<InputBuffer> inputBuffers = new();

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
        Vector2 moveInput = callbackContext.ReadValue<Vector2>();
        characterCommand.movementCommand.moveInput = moveInput;

        InputKeyType inputKeyType = InputKeyType.None;

        if (moveInput.y > 0.0f)
            inputKeyType |= InputKeyType.Up;
        else if (moveInput.y < 0.0f)
            inputKeyType |= InputKeyType.Down;

        if (moveInput.x > 0.0f)
            inputKeyType |= InputKeyType.Right;
        else if (moveInput.x < 0.0f)
            inputKeyType |= InputKeyType.Left;


        inputBuffers.Enqueue(new InputBuffer()
        {
            inputKeyType = inputKeyType,
            time = Time.time
        });
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

        inputBuffers.Enqueue(new InputBuffer()
        {
            inputKeyType = InputKeyType.Attack,
            time = Time.time
        });
    }

    #endregion
}
