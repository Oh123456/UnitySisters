using UnityEngine;

namespace CoreSystem.Components
{
    public class MovementData
    {
        private float speed = 2.0f;

        public float GetSpeed() => speed;



        private Vector2 movementInputValue = Vector2.zero;

        public Vector2 GetMovementInputValue() => movementInputValue;

        public void SetMovementDirection(Vector2 inputValue)
        {
            movementInputValue= inputValue; 
        }

    }
}
