using System.Buffers;
using UnityEngine;
using UnitySisters.Manager;

namespace UnitySisters.Controller
{
    public class AttackController : MonoBehaviour , IAttackController
    {
        //[SerializeField] private InterfaceReference<>
        [SerializeField] private Transform hitboxStart;
        private const int hitArrayCount = 16;

        public void Attack(AttackAnimationEventContext attackAnimationEventContext)
        {
            if (!DataManager.Instance.TryGetAttackData(attackAnimationEventContext.attackKey, out var data))
                return;
            RaycastHit[] raycastHits = ArrayPool<RaycastHit>.Shared.Rent(hitArrayCount);
            int count = ExecuteHitBox(data, raycastHits);
            int max = Mathf.Min(count, hitArrayCount);
            for (int i = 0; i < max; i++)
            {
                Debug.Log(raycastHits[i].transform.name);
            }


            ArrayPool<RaycastHit>.Shared.Return(raycastHits,true);
        }


        private int ExecuteHitBox(AttackDataScriptableObject data, RaycastHit[] raycastHits)
        {
            int count = 0;

            switch (data.HitBoxType)
            {
                case HitBoxType.Line:
                    count = Physics.RaycastNonAlloc(hitboxStart.TransformPoint(data.Offset),
                        hitboxStart.forward,
                        raycastHits,
                        data.Length,
                        data.LayerMask);
                    break;
                case HitBoxType.Box:
                    count = Physics.BoxCastNonAlloc(hitboxStart.TransformPoint(data.Offset),
                        data.BoxSize,
                        hitboxStart.forward,
                        raycastHits,
                        hitboxStart.rotation,
                        data.Length,
                        data.LayerMask);
                    break;
                case HitBoxType.Sphere:
                    count = Physics.SphereCastNonAlloc(hitboxStart.TransformPoint(data.Offset),
                        data.Radius,
                        hitboxStart.forward,
                        raycastHits,
                        data.Length,
                        data.LayerMask);
                    break;
                default:
                    break;
            }

            return count;
        }

    }

}