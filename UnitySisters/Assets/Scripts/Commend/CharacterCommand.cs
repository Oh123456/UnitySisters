using UnityEngine;

namespace UnitySisters.Command
{
    public class CharacterCommand : _3DModule.Controller.Command.BaseCharacterCommand
    {
        public MovementCommand movementCommand = new MovementCommand();
        public Vector3 cameraRotation;
        public bool isCameraControlAble;
        public bool isAttackButton;

        public override void ClearData()
        {
            movementCommand.ClearData();
            isAttackButton = false;
        }
    }

}