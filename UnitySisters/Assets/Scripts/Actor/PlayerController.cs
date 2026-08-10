using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;
using PlayerActions = InputSystem_Actions.PlayerActions;

public class PlayerController : _3DModule.Controller.BaseController<CharacterCommand>
{
    private struct InputData
    {
        public InputAction inputAction;
        public System.Action<CallbackContext> performed;
        public System.Action<CallbackContext> canceled;
        public System.Action<CallbackContext> started;
    }

    [SerializeField] private CinemachineCamera cinemachineCamera;
    private PlayerActions playerActions;
    private Character currentControlCharacter;
    private bool showMouseCursor = false;

    private Queue<InputData> inputDatas = new();

    private void Reset()
    {
        cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
    }

    protected override void Awake()
    {
        base.Awake();
        PlayerInputSystem playerInputSystem = InputManager.Instance.GetPlayerInputSystem();
        playerActions = playerInputSystem.GetInputAction();
        UpdateMouseCursor();
    }

    protected override void OnEnable()
    {
        EnableInputSystem(playerActions.Move, MovePerformed, MoveCanceled);
        EnableInputSystem(playerActions.Jump, JumpPerformed);
        EnableInputSystem(playerActions.ToggleMouseCursor, ToggleMouseCursorPerformed);
        EnableInputSystem(playerActions.Look);
    }

    protected override void OnDisable()
    {
        while (inputDatas.Count > 0)
        {
            DisableInputSystem(inputDatas.Dequeue());
        }
    }

    private void FixedUpdate()
    {
        if (currentControlCharacter == null)
            return;

        Transform cameraTransform = cinemachineCamera.transform;
        Vector3 forward = cameraTransform.forward.normalized;
        forward.y = 0.0f;
        bool isShowCursor = showMouseCursor;
        characterCommand.isCameraControlAble = !isShowCursor;
        if (!isShowCursor)
        {            
            characterCommand.moveWorldDirection = new Vector3(forward.x, 0.0f, forward.z);
            characterCommand.moveWorldRight = cameraTransform.right;
            characterCommand.cameraRotation = playerActions.Look.ReadValue<Vector2>();
        }

        currentControlCharacter.ExecuteCommand(characterCommand);
        characterCommand.ClearData();
    }

    public void ConnectCharacter(Character character)
    {
        currentControlCharacter = character;
        cinemachineCamera.Target.TrackingTarget = character.CarmeraTarget;
    }

    public void DisconnectCharacter()
    {
        currentControlCharacter = null;
        cinemachineCamera.Target.TrackingTarget = null;
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

    private void MovePerformed(CallbackContext callbackContext)
    {
        characterCommand.moveInput = callbackContext.ReadValue<Vector2>();
    }

    private void MoveCanceled(CallbackContext obj)
    {
        characterCommand.moveInput = Vector2.zero;
    }

    private void ToggleMouseCursorPerformed(CallbackContext callbackContext)
    {
        showMouseCursor = !showMouseCursor;
        UpdateMouseCursor();
    }
    private void JumpPerformed(CallbackContext callbackContext)
    {
        characterCommand.isJumpButton = true;
    }

    private void UpdateMouseCursor()
    {
        if (!showMouseCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(cinemachineCamera.transform.position, cinemachineCamera.transform.position + cinemachineCamera.transform.forward * 5.0f);

        if (characterCommand == null)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(cinemachineCamera.transform.position, cinemachineCamera.transform.position + new Vector3(characterCommand.moveWorldDirection.x * 5.0f, 0.0f, 0.0f));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(cinemachineCamera.transform.position, cinemachineCamera.transform.position + new Vector3(0.0f, 0.0f, characterCommand.moveWorldDirection.z * 5.0f));
    }
}
