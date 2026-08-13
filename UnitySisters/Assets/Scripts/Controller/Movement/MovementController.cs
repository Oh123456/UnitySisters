using UnityEngine;
using UnitySisters.Command;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
    [System.Serializable]
    public class MovementController : IModelBinder<MovementModel>
    {
        [SerializeField] private float jumpPower = 10.0f;
        [SerializeField] private bool isMovement = true;
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private bool moveRotation = true;

        [Header("Gravity")]
        [SerializeField] private bool isGravity = true;
        [SerializeField] private float groundCheckDistance = 1.0f;
        [SerializeField] private Vector3 groundCheckOffset = Vector3.zero;
        [SerializeField] private float groundCheckRadius = 0.23f;


        protected MovementModel movementModel;

        public bool IsMovement => isMovement;

        protected MovementController() { }

        public void SetModel(MovementModel movementModel)
        {
            this.movementModel = movementModel;
        }


        public void Move(MovementCommand movementCommand)
        {
            if (isMovement)
                UpdateMovement(movementCommand);
            if (isGravity)
                UpdateGravity();

            ExecuteMove();
        }

        protected virtual void ExecuteMove()
        {
            //TODO:: 기본 구현해야함
        }

        private void UpdateMovement(MovementCommand movementCommand)
        {
            Vector2 moveInput = movementCommand.moveInput;

            Vector3 moveVelocity = movementCommand.moveWorldDirection * moveInput.y * moveSpeed + (movementCommand.moveWorldRight * moveInput.x * moveSpeed);

            ref Vector3 velocity = ref movementModel.refVelocity;
            velocity.x = moveVelocity.x;
            velocity.y += movementCommand.isJumpButton ? jumpPower : 0.0f;
            velocity.z = moveVelocity.z;

            if (moveRotation && !moveInput.Equals(Vector2.zero))
            {
                Vector3 moveNormal = new Vector3(velocity.x, 0.0f, velocity.z).normalized;
                movementModel.rotationTarget.rotation = Quaternion.LookRotation(moveNormal);
            }
        }

        public void UpdateGravity()
        {
            ref Vector3 velocity = ref movementModel.refVelocity;
            if (Physics.SphereCast(movementModel.controlObject.transform.position + groundCheckOffset, groundCheckRadius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, 1, QueryTriggerInteraction.Ignore))
            {
                #region 레거시
                //if (!isGrounded)
                //{
                //    Physics.Raycast(transform.position + groundCheckOffset, Vector3.down, out RaycastHit groundHitInfo, groundCheckDistance, 1);
                //    Vector3 hitdir = groundHitInfo.point - transform.position;
                //    controller.Move(groundHitInfo.point);
                //}
                //else if (velocity.y < 0.0f)
                //{
                //    velocity.y = 0.0f;
                //} 
                #endregion

                if (movementModel.isGrounded && velocity.y < 0.0f)
                {
                    velocity.y = 0.0f;
                }


                movementModel.isGrounded = true;
            }
            else
            {
                velocity += (Physics.gravity * Time.fixedDeltaTime);
                movementModel.isGrounded = false;
            }
        }


        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void OnDrawGizmos()
        {
            if (movementModel == null)
                return;


            Vector3 origin = movementModel.controlObject.transform.position + groundCheckOffset;
            float radius = groundCheckRadius;
            Vector3 end = origin + Vector3.down * groundCheckDistance;

            Gizmos.color = Color.red;

            // 시작 구체
            Gizmos.DrawWireSphere(origin, radius);

            // 끝 구체
            Gizmos.DrawWireSphere(end, radius);

            // 구체가 지나가는 범위 대충 표시
            Gizmos.DrawLine(origin + Vector3.right * radius, end + Vector3.right * radius);
            Gizmos.DrawLine(origin - Vector3.right * radius, end - Vector3.right * radius);
            Gizmos.DrawLine(origin + Vector3.forward * radius, end + Vector3.forward * radius);
            Gizmos.DrawLine(origin - Vector3.forward * radius, end - Vector3.forward * radius);
            Debug.DrawRay(origin, Vector3.down * groundCheckDistance, movementModel.isGrounded ? Color.green : Color.red);
        }
    }

}
