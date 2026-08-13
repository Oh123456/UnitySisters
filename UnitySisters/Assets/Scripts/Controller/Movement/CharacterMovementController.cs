using UnityEngine;

namespace UnitySisters.Controller
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
    [System.Serializable]
    public class CharacterMovementController : MovementController
    {
        [SerializeField] protected CharacterController characterController;

        protected override void ExecuteMove()
        {
            characterController.Move(movementModel.Velocity * Time.fixedDeltaTime);
        }
    }

}
