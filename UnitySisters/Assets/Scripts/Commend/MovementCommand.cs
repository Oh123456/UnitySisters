using UnityEngine;

namespace UnitySisters.Command
{
    public class MovementCommand
    {
        public Vector2 moveInput;
        public Vector3 moveWorldDirection;
        public Vector3 moveWorldRight;
        public bool isJumpButton;

        public void ClearData()
        {
            isJumpButton = false;
        }
    }

}