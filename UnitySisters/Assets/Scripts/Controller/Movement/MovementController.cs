using UnityEngine;
using UnityFramework.FSM;
using UnitySisters.Command;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
    [System.Serializable]
    public class MovementController : MonoBehaviour, IModelBinder<MovementModel> , IMoveControl
    {
        private enum JumpState
        {
            Standing,
            Falling,
            JumpDelay,
        }

        [Header("Movement")]
        [SerializeField] private bool isMovement = true;
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private bool moveRotation = true;

        [Header("Gravity")]
        [SerializeField] private bool isGravity = true;
        [SerializeField] private float groundCheckDistance = 1.0f;
        [SerializeField] private Vector3 groundCheckOffset = Vector3.zero;
        [SerializeField] private float groundCheckRadius = 0.23f;

        [Header("Jump")]
        [SerializeField] private int jumpCount = 1;
        [SerializeField] private float jumpPower = 10.0f;
        [Tooltip("코요테 타임 : 점프시 낙하중에 일정기간 점프 가능 여유 시간")]
        [SerializeField] private float coyoteTime = 0.15f;
        [SerializeField] private float jumpBetweenDelay = 0.1f;

        protected MovementModel movementModel;
        private StatePattern<JumpState> jumpState;
        private float currentCoyoteTime = 0.0f;
        private int currentJumpCount = 0;
        private float currentJumpBetweenDelay = 0.0f;


        public bool IsMovement => isMovement;

        public void Initialize()
        {
            jumpState = new StatePattern<JumpState>(JumpState.Standing);
            jumpState.OnStateChanged += OnChangeJunpState;
            currentJumpCount = jumpCount;
        }


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
            velocity.z = moveVelocity.z;

            if (currentJumpCount > 0)
            {
                if (jumpState.CurrentState != JumpState.JumpDelay &&
                    movementCommand.isJumpButton)
                {
                    velocity.y = jumpPower;
                    SubtractumpCount();
                }
                else if(jumpState.CurrentState != JumpState.Standing)
                {
                    currentJumpBetweenDelay += Time.deltaTime;
                    if (currentJumpBetweenDelay <= jumpBetweenDelay)
                    {
                        jumpState.ChangeState(JumpState.Falling);
                    }
                }

            }

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
                    jumpState.ChangeState(JumpState.Standing);
                }

                movementModel.isGrounded = true;
            }
            else
            {
                velocity += (Physics.gravity * Time.fixedDeltaTime);
                movementModel.isGrounded = false;
                // 코여테타임
                if (jumpState.CurrentState == JumpState.Standing)
                {
                    currentCoyoteTime += Time.deltaTime;
                    if (coyoteTime <= currentCoyoteTime)
                    {
                        SubtractumpCount();
                    }
                }
            }
        }

        private void SubtractumpCount()
        {
            movementModel.additionalJump = jumpCount - currentJumpCount != 0;
            --currentJumpCount;
            if (currentJumpCount > 0)
                jumpState.ChangeState(JumpState.JumpDelay);
            else
                jumpState.ChangeState(JumpState.Falling);
        }

        private void OnChangeJunpState(JumpState per, JumpState current)
        {
            switch (current)
            {
                case JumpState.Standing:
                    currentJumpCount = jumpCount;
                    break;
                case JumpState.Falling:
                    currentCoyoteTime = 0.0f;
                    break;
                case JumpState.JumpDelay:
                    currentJumpBetweenDelay = 0.0f;
                    break;
                default:
                    break;
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

        public void LockMove()
        {
            isMovement = false;
            ref Vector3 velocity = ref movementModel.refVelocity;
            velocity.x = 0.0f;
            velocity.z = 0.0f;
        }

        public void UnlockMove()
        {
            isMovement = true;
        }
    }

}
