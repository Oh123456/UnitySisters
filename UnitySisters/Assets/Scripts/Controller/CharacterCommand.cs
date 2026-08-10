using UnityEngine;

public class CharacterCommand : _3DModule.Controller.BaseCharacterCommand
{
    public Vector2 moveInput;
    public Vector3 moveWorldDirection;
    public Vector3 moveWorldRight;
    public Vector3 cameraRotation;
    public bool isJumpButton;
    public bool isCameraControlAble;

    public override void ClearData()
    {
        isJumpButton = false;
    }
}
