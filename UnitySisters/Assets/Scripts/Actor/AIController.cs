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
        [SerializeField] private SensorScene sensorScene;
        [SerializeField] private BlackboardKeyReference<UnityEngine.Object> targetKey = new();
        [SerializeField] private BlackboardKeyReference<int> sensorResultCountKey = new();
        [SerializeField] private BlackboardKeyReference<Vector3> sensorLastPositionKey = new();
        [SerializeField] private BlackboardKeyReference<bool> investigationPendingKey = new();
        [SerializeField] private BlackboardKeyReference<Vector3> investigationPositionKey = new();
        [SerializeField] private BlackboardKeyReference<bool> searchPendingKey = new();
        [SerializeField] private BlackboardKeyReference<Vector3> lastSeenPositionKey = new();
        [SerializeField] private BlackboardKeyReference<bool> canAttackKey = new();
        [SerializeField] private BlackboardKeyReference<float> attackDistanceKey = new();
        private CharacterPerceptionController perceptionController = new();
        private CharacterAIBehaviorController aiBehaviorController = new();
        private UnitySisters.FSM.IFSMController fsmController;
        private Character currentControlCharacter;
        private Transform target;
    
        protected override void Awake()
        {
            base.Awake();
            if (behaviorTree == null)
                behaviorTree = GetComponent<BehaviorTreeComponent>();
        }
    
        protected override void OnEnable()
        {
            if (currentControlCharacter != null)
            {
                ResetPerception();
                perceptionController.Reset(target);
                if (sensorScene != null) sensorScene.Connect(currentControlCharacter, this);
                ResetBehavior();
            }
        }
    
        protected override void OnDisable()
        {
            if (currentControlCharacter != null) currentControlCharacter.SensorModel.Disconnect(this);
            ResetPerception();
            if (characterCommand != null)
            {
                characterCommand.ClearData();
                characterCommand.movementCommand.moveInput = Vector2.zero;
            }
        }
    
        public void ConnectCharacter(Character character, Transform chaseTarget)
        {
            DisconnectCharacter();
            currentControlCharacter = character;
            target = chaseTarget;
            if (character != null)
            {
                perceptionController.SetModel(character.PerceptionModel);
                perceptionController.Reset(chaseTarget);
                aiBehaviorController.SetModel(character.AIBehaviorModel);
                fsmController = character.GetComponent<UnitySisters.FSM.IFSMController>();
                ResetBehavior();
            }
            if (isActiveAndEnabled && sensorScene != null && character != null) sensorScene.Connect(character, this);
            behaviorTree.Controller?.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)null);
        }
    
        public void DisconnectCharacter()
        {
            if (currentControlCharacter != null) currentControlCharacter.SensorModel.Disconnect(this);
            ResetPerception();
            if (currentControlCharacter != null) aiBehaviorController.Reset(null, UnitySisters.Table.AIBehaviorSettings.Default);
            fsmController = null;
            currentControlCharacter = null;
            target = null;
            behaviorTree.Controller?.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)null);
            if (behaviorTree.Controller != null)
            {
                if (!string.IsNullOrEmpty(sensorResultCountKey.ID)) behaviorTree.Controller.BlackboardWriter.SetValue(sensorResultCountKey, 0);
                if (!string.IsNullOrEmpty(sensorLastPositionKey.ID)) behaviorTree.Controller.BlackboardWriter.SetValue(sensorLastPositionKey, Vector3.zero);
            }
            if (characterCommand == null)
                return;
            characterCommand.ClearData();
            characterCommand.movementCommand.moveInput = Vector2.zero;
        }
    
        private void FixedUpdate()
        {
            if (currentControlCharacter == null || !currentControlCharacter.isActiveAndEnabled)
                return;
            currentControlCharacter.SensorModel.Tick(Time.fixedDeltaTime, Time.time);
            if (behaviorTree.Controller != null)
            {
                var results = currentControlCharacter.SensorModel.Results;
                var perception = currentControlCharacter.PerceptionModel;
                if (!string.IsNullOrEmpty(searchPendingKey.ID) && perception.SearchPending &&
                    !behaviorTree.Controller.Blackboard.GetValue(searchPendingKey))
                {
                    perceptionController.CompleteSearch();
                    aiBehaviorController.SetRoamCenter(perception.LastSeenPosition);
                }
                if (!string.IsNullOrEmpty(investigationPendingKey.ID) && perception.HasInvestigation &&
                    !behaviorTree.Controller.Blackboard.GetValue(investigationPendingKey)) perceptionController.CompleteInvestigation();
                if (currentControlCharacter.SensorModel.IsActive) perceptionController.Consume(results);
                else perceptionController.Reset(target);
                aiBehaviorController.UpdateAttack(perception, Time.time,
                    fsmController?.GetCurrentStateID() == (int)global::FSM.CharacterFSMCotnroller.CharacterStateID.Attack);
                var writer = behaviorTree.Controller.BlackboardWriter;
                if (!string.IsNullOrEmpty(searchPendingKey.ID)) writer.SetValue(searchPendingKey, perception.SearchPending);
                if (!string.IsNullOrEmpty(lastSeenPositionKey.ID)) writer.SetValue(lastSeenPositionKey, perception.LastSeenPosition);
                if (!string.IsNullOrEmpty(canAttackKey.ID)) writer.SetValue(canAttackKey, currentControlCharacter.AIBehaviorModel.CanAttack);
                if (!string.IsNullOrEmpty(attackDistanceKey.ID)) writer.SetValue(attackDistanceKey, currentControlCharacter.AIBehaviorModel.AttackDistance01);
                behaviorTree.Controller.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)perception.DetectedTarget);
                if (!string.IsNullOrEmpty(investigationPendingKey.ID))
                    behaviorTree.Controller.BlackboardWriter.SetValue(investigationPendingKey, perception.HasInvestigation);
                if (!string.IsNullOrEmpty(investigationPositionKey.ID))
                    behaviorTree.Controller.BlackboardWriter.SetValue(investigationPositionKey, perception.InvestigationPosition);
                if (!string.IsNullOrEmpty(sensorResultCountKey.ID))
                    behaviorTree.Controller.BlackboardWriter.SetValue(sensorResultCountKey, results.Count);
                if (!string.IsNullOrEmpty(sensorLastPositionKey.ID) && results.TryGetResult(results.Count - 1, out var last))
                    behaviorTree.Controller.BlackboardWriter.SetValue(sensorLastPositionKey, last.position);
                currentControlCharacter.SensorModel.ClearResults();
                if (!currentControlCharacter.SensorModel.IsActive)
                    characterCommand.movementCommand.moveInput = Vector2.zero;
            }
            currentControlCharacter.ExecuteCommand(characterCommand);
            characterCommand.ClearData();
        }

        private void ResetPerception()
        {
            if (currentControlCharacter != null) perceptionController.Reset(null);
            var controller = behaviorTree != null ? behaviorTree.Controller : null;
            if (controller == null) return;
            controller.BlackboardWriter.SetValue(targetKey, (UnityEngine.Object)null);
            if (!string.IsNullOrEmpty(investigationPendingKey.ID)) controller.BlackboardWriter.SetValue(investigationPendingKey, false);
            if (!string.IsNullOrEmpty(investigationPositionKey.ID)) controller.BlackboardWriter.SetValue(investigationPositionKey, Vector3.zero);
            if (!string.IsNullOrEmpty(searchPendingKey.ID)) controller.BlackboardWriter.SetValue(searchPendingKey, false);
            if (!string.IsNullOrEmpty(lastSeenPositionKey.ID)) controller.BlackboardWriter.SetValue(lastSeenPositionKey, Vector3.zero);
            if (!string.IsNullOrEmpty(canAttackKey.ID)) controller.BlackboardWriter.SetValue(canAttackKey, false);
            if (!string.IsNullOrEmpty(attackDistanceKey.ID)) controller.BlackboardWriter.SetValue(attackDistanceKey, 1f);
        }

        private void ResetBehavior()
        {
            var settings = UnitySisters.Table.AIBehaviorSettings.Default;
            if (sensorScene != null && sensorScene.Table.TryGet(currentControlCharacter.SensorKey, out var row)) settings = row.Behavior;
            aiBehaviorController.Reset(currentControlCharacter.transform, settings);
        }

        bool IAICommandControl.RequestAttack()
        {
            if (!isActiveAndEnabled || currentControlCharacter == null || !currentControlCharacter.isActiveAndEnabled ||
                !aiBehaviorController.RequestAttack(Time.time)) return false;
            characterCommand.isAttackButton = true;
            characterCommand.movementCommand.moveInput = Vector2.zero;
            if (behaviorTree.Controller != null && !string.IsNullOrEmpty(canAttackKey.ID))
                behaviorTree.Controller.BlackboardWriter.SetValue(canAttackKey, false);
            return true;
        }

        void IAICommandControl.RequestRoam()
        {
            if (currentControlCharacter != null)
                ((IAICommandControl)this).RequestMovement(aiBehaviorController.Roam(currentControlCharacter.transform.position, Time.time));
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
