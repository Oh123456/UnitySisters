using FSM;
using UnityEngine;
using UnitySisters.Command;
using UnitySisters.Controller;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

[RequireComponent(typeof(CharacterController))]
public class Character : MonoBehaviour , IMoveControl
{    
    [SerializeField] private Transform carmeraTarget;
    [SerializeField] private Transform characterTarget;

    [Header("Animiation")]
    [SerializeField] private AnimationController animationController;

    [Header("Movement")]
    [SerializeField] private MovementController movementController;

    [Header("FMS")]
    [SerializeField] CharacterFSMCotnroller characterFSMCotnroller;

    private MovementModel movementModel = null;
    private CharacterAnimationModel characterAnimationModel = null;
    public Transform CarmeraTarget => carmeraTarget;
    public MovementController MovementController => movementController;

    private void Awake()
    {
        movementModel = new MovementModel()
        {
            controlObject = gameObject,
            rotationTarget = characterTarget,
        };

        movementController.Initialize();
        movementController.SetModel(movementModel: movementModel);

        characterAnimationModel = new CharacterAnimationModel();

        animationController.SetModel(characterAnimationModel);
        characterFSMCotnroller?.Initialize(this);
    }

    public void ExecuteCommand(CharacterCommand command)
    {
        if (command.isAttackButton)
        {
            characterFSMCotnroller.ChangeState((int)CharacterFSMCotnroller.CharacterStateID.Attack);
        }
        movementController?.Move(command.movementCommand);
    }

    private void Update()
    {
        animationController?.UpdateAnimation();
    }

    private void LateUpdate()
    {
        UpdateFSMModel();
        UpdateAnimationModel();


        ClearModelData();
    }

    private void UpdateFSMModel()
    {
        if (characterFSMCotnroller == null)
            return;
        var model = characterFSMCotnroller.CharacterFSMModel;
        if (model == null)
            return;

        Vector3 velocity = movementModel.Velocity;
        float falling = velocity.y;
        velocity.y = 0.0f;
        model.moveValue = velocity.sqrMagnitude;
        model.isFalling = !falling.Equals(0.0f);
    }

    private void UpdateAnimationModel()
    {
        if (characterAnimationModel == null)
            return;

        if (characterFSMCotnroller == null)
            return;

        characterAnimationModel.stateID = (int)characterFSMCotnroller.GetCurrentStateID();

        Vector3 velocity = movementModel.Velocity;
        characterAnimationModel.yValue = velocity.y;
        characterAnimationModel.isFalling = !characterAnimationModel.yValue.Equals(0.0f);
        characterAnimationModel.additionalJunmp = movementModel.additionalJump;
    }

    private void ClearModelData()
    {
        movementModel.additionalJump = false;
    }

    private void OnDrawGizmos()
    {
        movementController?.OnDrawGizmos();
    }

    public void LockMove()
    {
        movementController.LockMove();
    }

    public void UnlockMove()
    {
        movementController.UnlockMove();
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
