using System.Collections.Generic;
using FSM;
using UnityFramework.CommandTree;
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
    private InputKeyType queuedDirection;
    private ICharacterFSMCotnroller currentCharacterFSM;
    private bool hasCompletedInputBuffer;
    private int completedInputCount;
    private bool inputBufferEventsBound;
    private System.Action<ICommandTreeNode, ICommandBehavior, bool> commandTraceObserver = null;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void TraceCommandInput(ICommandTreeNode node, ICommandBehavior behavior, bool completed, int inputCount)
    {
        if (inputCount > 0)
            commandTraceObserver?.Invoke(node, behavior, completed);
    }

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



    private void ProcessCommandInputs()
    {
        characterCommand.isAttackButton = false;
        int count = hasCompletedInputBuffer ? completedInputCount : inputBuffers.Count;
        hasCompletedInputBuffer = false;
        completedInputCount = 0;
        var tree = currentControlCharacter.CommandTree;
        var node = tree?.Root;
        ICommandBehavior behavior = null;

        TraceCommandInput(node, null, false, count);

        for (int i = 0; i < count; i++)
        {
            var input = inputBuffers.Dequeue();
            var next = tree?.FindChild(node, input.inputKeyType);
            if (next == null)
                continue;

            node = next;
            if (node.CommandBehavior != null)
                behavior = node.CommandBehavior;
            TraceCommandInput(node, behavior, false, count);
        }

        if (inputBuffers.Count == 0)
            queuedDirection = InputKeyType.None;

        behavior?.Apply(characterCommand);
        TraceCommandInput(node, behavior, true, count);
    }

    private void RegisterInputBufferEvents()
    {
        if (currentCharacterFSM == null || inputBufferEventsBound)
            return;

        currentCharacterFSM.InputBufferStarted += OnInputBufferStarted;
        currentCharacterFSM.InputBufferEnded += OnInputBufferEnded;
        currentCharacterFSM.InputBufferCancelled += OnInputBufferCancelled;
        inputBufferEventsBound = true;
    }

    private void UnregisterInputBufferEvents()
    {
        if (currentCharacterFSM != null && inputBufferEventsBound)
        {
            currentCharacterFSM.InputBufferStarted -= OnInputBufferStarted;
            currentCharacterFSM.InputBufferEnded -= OnInputBufferEnded;
            currentCharacterFSM.InputBufferCancelled -= OnInputBufferCancelled;
        }
        inputBufferEventsBound = false;
    }

    private void OnInputBufferStarted()
    {
        OnInputBufferCancelled();
    }

    private void OnInputBufferEnded()
    {
        hasCompletedInputBuffer = true;
        completedInputCount = inputBuffers.Count;
        queuedDirection = InputKeyType.None;
    }

    private void OnInputBufferCancelled()
    {
        inputBuffers.Clear();
        hasCompletedInputBuffer = false;
        completedInputCount = 0;
        queuedDirection = InputKeyType.None;
    }

    private void ResetInput()
    {
        currentCharacterFSM?.CancelInputBuffer();
        OnInputBufferCancelled();
        if (characterCommand == null)
            return;

        characterCommand.ClearData();
        characterCommand.movementCommand.moveInput = Vector2.zero;
        characterCommand.cameraRotation = Vector3.zero;
    }

    #region KeyCallbacks
    private void MovePerformed(CallbackContext callbackContext)
    {
        if (currentControlCharacter == null || !currentControlCharacter.isActiveAndEnabled)
            return;

        Vector2 moveInput = callbackContext.ReadValue<Vector2>();
        characterCommand.movementCommand.moveInput = moveInput;
        QueueDirectionInput(moveInput, false);
    }

    private void QueueDirectionInput(Vector2 moveInput, bool skipQueuedDirection)
    {
        InputKeyType inputKeyType = InputKeyType.None;

        if (moveInput.y > 0.0f)
            inputKeyType |= InputKeyType.Up;
        else if (moveInput.y < 0.0f)
            inputKeyType |= InputKeyType.Down;

        if (moveInput.x > 0.0f)
            inputKeyType |= InputKeyType.Right;
        else if (moveInput.x < 0.0f)
            inputKeyType |= InputKeyType.Left;


        if (inputKeyType == InputKeyType.None || (skipQueuedDirection && queuedDirection == inputKeyType))
            return;

        queuedDirection = inputKeyType;
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
        if (currentControlCharacter == null || !currentControlCharacter.isActiveAndEnabled)
            return;

        QueueDirectionInput(characterCommand.movementCommand.moveInput, true);
        inputBuffers.Enqueue(new InputBuffer()
        {
            inputKeyType = InputKeyType.Attack,
            time = Time.time
        });
    }

    #endregion
}
