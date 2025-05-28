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

        public MovementData MovementData => movementData;

        public void Awake()
        {
            var mono = CustomMonoBehaviour;
            contorlGameObject = mono.transform;
            rigidbody = mono.GetComponent<Rigidbody>();
        }

        public void FixedUpdate()
        {
            Vector2 movementInputValue = movementData.GetMovementInputValue();
            float speed = movementData.GetSpeed();

            Vector3 direction = contorlGameObject.forward * movementInputValue.y + contorlGameObject.right * movementInputValue.x;
            direction *= speed * Time.fixedDeltaTime;
            rigidbody.MovePosition(contorlGameObject.position + direction);
        }

        public void Dispose()
        {
            contorlGameObject = null;
            rigidbody = null;
        }


    }

}