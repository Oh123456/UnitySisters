using UnityEngine;
using UnityFramework.BehaviorTree;
using UnitySisters.Command;

using UnitySisters.Controller.Interface;

namespace UnitySisters.Controller
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp")]
    [RequireComponent(typeof(BehaviorTreeComponent))]
    public sealed class AIController : _3DModule.Controller.BaseController<CharacterCommand>, IAICommandControl
    {
        [SerializeField] private BehaviorTreeComponent behaviorTree;
        [SerializeField] private BlackboardKeyReference<UnityEngine.Object> targetKey = new();
        private Character currentControlCharacter;
        private Transform target;
    
        protected override void Awake()
        {
            base.Awake();
            if (behaviorTree == null)
                behaviorTree = GetComponent<BehaviorTreeComponent>();
        }
    
        protected override void OnEnable() { }
    
        protected override void OnDisable()
        {
            if (characterCommand != null)
                characterCommand.movementCommand.moveInput = Vector2.zero;
        }
    
        public void ConnectCharacter(Character character, Transform chaseTarget)
        {
            DisconnectCharacter();
            currentControlCharacter = character;
            target = chaseTarget;
            behaviorTree.Controller?.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)target);
        }
    
        public void DisconnectCharacter()
        {
            currentControlCharacter = null;
            target = null;
            behaviorTree.Controller?.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)null);
            if (characterCommand == null)
                return;
            characterCommand.ClearData();
            characterCommand.movementCommand.moveInput = Vector2.zero;
        }
    
        private void FixedUpdate()
        {
            if (currentControlCharacter == null || !currentControlCharacter.isActiveAndEnabled)
                return;
            currentControlCharacter.ExecuteCommand(characterCommand);
            characterCommand.ClearData();
        }
    
        bool IAICommandControl.TryGetPosition(out Vector3 position)
        {
            position = currentControlCharacter != null ? currentControlCharacter.transform.position : Vector3.zero;
            return isActiveAndEnabled && currentControlCharacter != null && currentControlCharacter.isActiveAndEnabled;
        }
    
        void IAICommandControl.RequestMovement(Vector3 direction)
        {
            if (!isActiveAndEnabled)
                return;
            direction.y = 0;
            characterCommand.movementCommand.moveWorldDirection = Vector3.forward;
            characterCommand.movementCommand.moveWorldRight = Vector3.right;
            characterCommand.movementCommand.moveInput = new Vector2(direction.x, direction.z);
        }
    }
}
