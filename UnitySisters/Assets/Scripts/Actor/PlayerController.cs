using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnitySisters.Command;
using UnitySisters.Controller;
using UnitySisters.Model;

public partial class PlayerController : _3DModule.Controller.BaseController<CharacterCommand>
{
 
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Camera")]
    [SerializeField] private CameraControiller cameraControiller = new CameraControiller();

    private Character currentControlCharacter;
    private bool showMouseCursor = false;
    private CameraCotrolModel cameraCotrolModel;
    private Queue<InputData> inputDatas = new();

    private void Reset()
    {
        cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
    }

    protected override void Awake()
    {
        base.Awake();
        InitializeInputSystem();
        InitializeCameraController();
        UpdateMouseCursor();
    }

    protected override void OnEnable()
    {
        EnableInputSystems();
    }

    protected override void OnDisable()
    {
        DisableInputSystems();
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
            characterCommand.movementCommand.moveWorldDirection = new Vector3(forward.x, 0.0f, forward.z);
            characterCommand.movementCommand.moveWorldRight = cameraTransform.right;
            characterCommand.cameraRotation = playerActions.Look.ReadValue<Vector2>();
        }

        cameraControiller?.ExecuteRotation(characterCommand);
        currentControlCharacter.ExecuteCommand(characterCommand);
        characterCommand.ClearData();
    }


    private void InitializeCameraController()
    {
        cameraCotrolModel = new CameraCotrolModel();
        cameraControiller.SetModel(cameraCotrolModel);
    }

    public void ConnectCharacter(Character character)
    {
        currentControlCharacter = character;
        cameraCotrolModel.carmeraTarget = character.CarmeraTarget;
        cinemachineCamera.Target.TrackingTarget = character.CarmeraTarget;
    }

    public void DisconnectCharacter()
    {
        currentControlCharacter = null;
        cinemachineCamera.Target.TrackingTarget = null;
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
        Gizmos.DrawLine(cinemachineCamera.transform.position, cinemachineCamera.transform.position + new Vector3(characterCommand.movementCommand.moveWorldDirection.x * 5.0f, 0.0f, 0.0f));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(cinemachineCamera.transform.position, cinemachineCamera.transform.position + new Vector3(0.0f, 0.0f, characterCommand.movementCommand.moveWorldDirection.z * 5.0f));
    }
}
