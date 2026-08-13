using FSM;
using UnityEngine;
using UnitySisters.Command;
using UnitySisters.Controller;
using UnitySisters.Model;

[RequireComponent(typeof(CharacterController))]
public class Character : MonoBehaviour
{

    protected static Vector3 gravity = new Vector3(0.0f, -9.81f, 0.0f);

    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform carmeraTarget;
    [SerializeField] private Transform characterTarget;

    [Header("Movement")]
    [SerializeReference] private MovementController movementController = new CharacterMovementController();

    [Header("FMS")]
    [SerializeField] CharacterFSMCotnroller characterFSMCotnroller;

    private MovementModel movementModel = null;

    public Transform CarmeraTarget => carmeraTarget;
    public MovementController MovementController => movementController;

    private void Reset()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Awake()
    {
        movementModel = new MovementModel()
        {
            controlObject = gameObject,
            rotationTarget = characterTarget,
        };

        movementController.SetModel(movementModel: movementModel);

        if (characterFSMCotnroller != null)
        {
            characterFSMCotnroller.CreateStateMachine();
            characterFSMCotnroller.StartStateMachine();
        }
    }

    public void ExecuteCommand(CharacterCommand command)
    {
        movementController?.Move(command.movementCommand);
    }

    private void OnDrawGizmos()
    {
        movementController?.OnDrawGizmos();
    }

}

//마지막 확인후 안쓰면 삭제
#region 짬통
/*
 * 
    [Header("Gravity")]
    [SerializeField] private bool isGravity = true;
    [SerializeField] private float groundCheckDistance = 1.0f;
    [SerializeField] private Vector3 groundCheckOffset = Vector3.zero;

    [Header("Movement")]
    [SerializeField] private float jumpPower = 10.0f;
    [SerializeField] private bool isMovement = true;
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private bool moveRotation = true;

        //if (isMovement)
        //    UpdateMovement(command);
        //if (isGravity)
        //    UpdateGravity();


    //private void UpdateMovement(CharacterCommand command)
    //{
    //    if (command == null)
    //        return;


    //    Vector2 moveInput = command.moveInput;

    //    Vector3 moveVelocity = command.moveWorldDirection * moveInput.y * moveSpeed + (command.moveWorldRight * moveInput.x * moveSpeed);

    //    velocity.x = moveVelocity.x;
    //    velocity.y += command.isJumpButton ? jumpPower : 0.0f;
    //    velocity.z = moveVelocity.z;

    //    if (moveRotation && !moveInput.Equals(Vector2.zero))
    //    {
    //        Vector3 moveNormal = new Vector3(velocity.x, 0.0f, velocity.z).normalized;
    //        characterTarget.rotation = Quaternion.LookRotation(moveNormal);
    //    }
    //}

    //private void UpdateGravity()
    //{
    //    if (Physics.SphereCast(transform.position + groundCheckOffset, controller.radius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, 1, QueryTriggerInteraction.Ignore))
    //    {
    //        #region 레거시
    //        //if (!isGrounded)
    //        //{
    //        //    Physics.Raycast(transform.position + groundCheckOffset, Vector3.down, out RaycastHit groundHitInfo, groundCheckDistance, 1);
    //        //    Vector3 hitdir = groundHitInfo.point - transform.position;
    //        //    controller.Move(groundHitInfo.point);
    //        //}
    //        //else if (velocity.y < 0.0f)
    //        //{
    //        //    velocity.y = 0.0f;
    //        //} 
    //        #endregion
    //        if (isGrounded && velocity.y < 0.0f)
    //        {
    //            velocity.y = 0.0f;
    //        }


    //        isGrounded = true;
    //    }
    //    else
    //    {
    //        velocity += (Physics.gravity * Time.fixedDeltaTime) ;
    //        isGrounded = false;
    //    }
    //}


















        //Vector3 origin = transform.position + groundCheckOffset;
        //float radius = controller.radius;
        //Vector3 end = origin + Vector3.down * groundCheckDistance;

        //Gizmos.color = Color.red;

        //// 시작 구체
        //Gizmos.DrawWireSphere(origin, radius);

        //// 끝 구체
        //Gizmos.DrawWireSphere(end, radius);

        //// 구체가 지나가는 범위 대충 표시
        //Gizmos.DrawLine(origin + Vector3.right * radius, end + Vector3.right * radius);
        //Gizmos.DrawLine(origin - Vector3.right * radius, end - Vector3.right * radius);
        //Gizmos.DrawLine(origin + Vector3.forward * radius, end + Vector3.forward * radius);
        //Gizmos.DrawLine(origin - Vector3.forward * radius, end - Vector3.forward * radius);
        //Debug.DrawRay(origin, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
 */
#endregion
