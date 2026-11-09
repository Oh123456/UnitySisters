using UnityEngine;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;
using UnitySisters.Table;

namespace UnitySisters.Controller
{
    public sealed class CharacterAIBehaviorController : IModelBinder<CharacterAIBehaviorModel>
    {
        private CharacterAIBehaviorModel model;
        private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        public void SetModel(CharacterAIBehaviorModel model) { this.model = model; }
        public void Reset(Transform owner, in AIBehaviorSettings settings)
        {
            settings.Validate(); model.owner = owner; model.settings = settings;
            model.roamCenter = owner != null ? owner.position : default;
            model.hasDestination = false; model.waitUntil = 0; model.attackReadyAt = 0;
            model.canAttack = false; model.attackDistance01 = 1;
        }
        public void UpdateAttack(CharacterPerceptionModel perception, float time, bool attacking)
        {
            Transform target = perception.DetectedTarget;
            Vector3 delta = target != null && model.owner != null ? target.position - model.owner.position : Vector3.zero;
            delta.y = 0;
            model.attackDistance01 = target != null ? Mathf.Clamp01(delta.magnitude / model.settings.attackRange) : 1;
            model.canAttack = model.owner != null && target != null && perception.Visible &&
                delta.sqrMagnitude <= model.settings.attackRange * model.settings.attackRange && time >= model.attackReadyAt && !attacking;
        }
        public bool RequestAttack(float time)
        {
            if (!model.canAttack) return false;
            model.attackReadyAt = time + model.settings.attackRequestInterval; model.canAttack = false; return true;
        }
        public void SetRoamCenter(Vector3 center)
        { model.roamCenter = center; model.hasDestination = false; model.waitUntil = 0; }
        public Vector3 Roam(Vector3 position, float time)
        {
            if (model.owner == null || time < model.waitUntil) return Vector3.zero;
            if (!model.hasDestination)
            {
                if (!TryChooseDestination(position, out model.destination)) { model.waitUntil = time + model.settings.roamPause + .1f; return Vector3.zero; }
                model.hasDestination = true; model.progressPosition = position; model.progressAt = time;
            }
            Vector3 delta = model.destination - position; delta.y = 0;
            if (delta.sqrMagnitude < .36f)
            { model.hasDestination = false; model.waitUntil = time + model.settings.roamPause; return Vector3.zero; }
            Vector3 progress = position - model.progressPosition; progress.y = 0;
            if (progress.sqrMagnitude > .01f) { model.progressPosition = position; model.progressAt = time; }
            else if (time - model.progressAt >= model.settings.stuckTimeout)
            { model.hasDestination = false; model.waitUntil = time + .1f; return Vector3.zero; }
            return delta.normalized;
        }
        private bool TryChooseDestination(Vector3 position, out Vector3 destination)
        {
            destination = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * model.settings.roamRadius;
                Vector3 point = model.roamCenter + new Vector3(offset.x, 0, offset.y);
                if ((point - position).sqrMagnitude < 1 ||
                    !Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var ground, 8, model.settings.groundLayers, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < .7f || Physics.CheckSphere(ground.point + Vector3.up * .6f, .25f, model.settings.obstacleLayers, QueryTriggerInteraction.Ignore)) continue;
                Vector3 origin = position + Vector3.up;
                Vector3 delta = ground.point + Vector3.up * .5f - origin;
                int count = Physics.RaycastNonAlloc(origin, delta.normalized, obstacleHits, delta.magnitude, model.settings.obstacleLayers, QueryTriggerInteraction.Ignore);
                bool blocked = count == obstacleHits.Length;
                for (int i = 0; i < count && !blocked; i++)
                    if (!obstacleHits[i].transform.IsChildOf(model.owner) && obstacleHits[i].normal.y < .7f) blocked = true;
                System.Array.Clear(obstacleHits, 0, count);
                if (blocked) continue;
                destination = ground.point; return true;
            }
            return false;
        }
    }
}
