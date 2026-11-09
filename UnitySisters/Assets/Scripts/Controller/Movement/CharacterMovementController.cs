using UnityEngine;

namespace UnitySisters.Controller
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
    [System.Serializable]
    public class CharacterMovementController : MovementController
    {
        [SerializeField] protected CharacterController characterController;
        [SerializeField] private LayerMask slideLayerMask;
        [SerializeField, Min(0.0f)] private float slideSpeed = 2.0f;

        protected override void ExecuteMove()
        {
            Vector3 velocity = movementModel.Velocity;
            bool sliding = movementModel.slideContact != null &&
                movementModel.slideContact.enabled && movementModel.slideContact.gameObject.activeInHierarchy &&
                Time.time - movementModel.slideContactTime <= Time.fixedDeltaTime * 2.0f && velocity.y <= 0.0f;
            if (sliding)
                velocity += movementModel.slideDirection * slideSpeed;
            movementModel.slideContact = null;
            CollisionFlags collisions = characterController.Move(velocity * Time.fixedDeltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && movementModel.refVelocity.y < 0.0f)
                movementModel.refVelocity.y = 0.0f;
            if ((collisions & CollisionFlags.Above) != 0 && movementModel.refVelocity.y > 0.0f)
                movementModel.refVelocity.y = 0.0f;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (movementModel == null || hit.collider == null || hit.collider.isTrigger ||
                hit.collider.transform.IsChildOf(movementModel.controlObject.transform))
                return;
            Bounds bounds = characterController.bounds;
            bool below = hit.normal.y > 0.1f &&
                hit.point.y <= bounds.min.y + characterController.radius + characterController.skinWidth;
            if (!below || movementModel.Velocity.y > 0.0f)
                return;
            bool slideLayer = (slideLayerMask.value & (1 << hit.gameObject.layer)) != 0;
            if (!slideLayer)
            {
                if (IsGroundLayer(hit.gameObject.layer) &&
                    hit.normal.y >= Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad))
                    SetGrounded(true);
                return;
            }
            SetGrounded(false);
            Vector3 direction = bounds.center - hit.collider.bounds.center;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = movementModel.slideDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = movementModel.rotationTarget.forward;
                direction.y = 0.0f;
            }
            movementModel.slideDirection = direction.normalized;
            movementModel.slideContact = hit.collider;
            movementModel.slideContactTime = Time.time;
        }
    }

}
