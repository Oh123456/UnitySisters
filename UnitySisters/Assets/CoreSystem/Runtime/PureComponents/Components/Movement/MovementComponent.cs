using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using UnityEngine;

namespace CoreSystem.Components
{
    public class MovementComponent : PureComponent, IAwakeHandle, IFixedUpdateHandle , System.IDisposable
    {
        //TODO:: 모델로 분할 할것 그리고 외부에서 값변경할수있게 방법 생각할것 
        private MovementData movementData = new MovementData();

        private Rigidbody rigidbody;
        private Transform contorlGameObject;
        private bool ignoreTimeScale;

        public MovementData MovementData => movementData;

        private System.Action moveAction;

        public MovementComponent()
        {
            movementData.OnChangeMoveLockDirection += OnChangeMoveLockDirection;
            movementData.OnChangeIgnoreTimeScale += OnChangeMoveLockDirection;
            OnChangeMoveLockDirection(movementData.IsMoveLockDirection);
        }

        public void Awake()
        {
            var mono = CustomMonoBehaviour;
            contorlGameObject = mono.transform;
            rigidbody = mono.GetComponent<Rigidbody>();
        }

        public void FixedUpdate()
        {
            moveAction();
        }

        private void Move()
        {
            Vector2 movementInputValue = movementData.MovementInputValue;
            if (movementInputValue.Equals(Vector2.zero))
                return;

            float speed = movementData.Speed;

            Vector3 direction = GetForward() * movementInputValue.y + contorlGameObject.right * movementInputValue.x;
            direction *= speed * GetDeltaTime();

            rigidbody.MovePosition(contorlGameObject.position + direction);
        }

        private void MoveLockDirection()
        {
            Vector2 movementInputValue = movementData.MovementInputValue;
            if (movementInputValue.Equals(Vector2.zero))
                return;

            float speed = movementData.Speed;

            Vector3 direction = new Vector3(movementInputValue.x, 0.0f, movementInputValue.y).normalized;

            Vector3 moveDirection = GetForward() * direction.magnitude;
            moveDirection *= speed * GetDeltaTime();

            rigidbody.MoveRotation(Quaternion.LookRotation(direction));

            rigidbody.MovePosition(contorlGameObject.position + moveDirection);

        }

        private void OnChangeMoveLockDirection(bool isMoveLockDirection)
        {
            if (!isMoveLockDirection)
                moveAction = Move;
            else 
                moveAction = MoveLockDirection;
        }

        private float GetDeltaTime()
        {
            if (movementData.IgnoreTimeScale)
                return Time.fixedUnscaledDeltaTime;
            else
                return Time.fixedDeltaTime;
        }

        private Vector3 GetForward()
        {
            Transform controlCamera = movementData.ControlCamera;
            if (controlCamera == null)
                return contorlGameObject.forward;
            else 
                return controlCamera.forward;
        }

        public void Dispose()
        {
            contorlGameObject = null;
            rigidbody = null;
            movementData.OnChangeMoveLockDirection -= OnChangeMoveLockDirection;
            movementData = null;
        }


    }

}