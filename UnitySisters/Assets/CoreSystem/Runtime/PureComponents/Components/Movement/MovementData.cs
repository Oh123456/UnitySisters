using UnityEngine;

namespace CoreSystem.Components
{

    public class MovementData
    {
        // 스피드
        private float speed = 2.0f;
        public float Speed => speed;


        //이동시 이동 방향 볼건지
        private bool isMoveLockDirection = false;
        public bool IsMoveLockDirection => isMoveLockDirection;
        public void SetIsMoveLockDirection(bool b)
        {
            if (isMoveLockDirection == b)
                return;

            isMoveLockDirection = b;
            OnChangeMoveLockDirection?.Invoke(b);
        }


        // 인풋값
        private Vector2 movementInputValue = Vector2.zero;

        public Vector2 MovementInputValue => movementInputValue;

        public void SetMovementDirection(Vector2 inputValue)
        {
            movementInputValue= inputValue; 
        }


        private bool ignoreTimeScale = false;

        public bool IgnoreTimeScale => ignoreTimeScale;
        public void SetIgnoreTimeScale(bool ignoreTimeScale)
        {
            if (this.ignoreTimeScale == ignoreTimeScale)
                return;
            this.ignoreTimeScale = ignoreTimeScale;
            OnChangeIgnoreTimeScale?.Invoke(ignoreTimeScale);
        }

        private Transform controlCamera;
        
        public Transform ControlCamera => controlCamera;
        public void SetControlCamera(GameObject camera)
        {
            this.controlCamera = camera.transform;
        }

        // event

        public event System.Action<bool> OnChangeMoveLockDirection;
        public event System.Action<bool> OnChangeIgnoreTimeScale;

    }
}
