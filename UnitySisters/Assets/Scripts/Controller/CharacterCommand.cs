using UnityEngine;

public class CharacterCommand : _3DModule.Controller.BaseCharacterCommand
{
    public Vector2 moveInput;
    public Vector3 moveWorldDirection;
    public Vector3 moveWorldRight;
    public Vector3 cameraRoatation;
    public float jumpValue;
    public bool isCameraControlAble;

    public override void ClearData()
    {
        jumpValue = 0.0f;
    }
}
