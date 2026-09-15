using System;
using System.Buffers;
using UnityEngine;
using UnityFramework.BattleSystem;
using UnitySisters.Manager;

namespace UnitySisters
{
    public class DefaultHitScanComponent : HitScanComponent<AttackAnimationEventContext>
    {
        public override void StartHitScan(in AttackAnimationEventContext hitScanData)
        {
            if (!DataManager.Instance.TryGetAttackData(hitScanData.attackKey, out var data))
                return;
            int maxTargets = data.MaxTargets;

            RaycastHit[] raycastHits = ArrayPool<RaycastHit>.Shared.Rent(maxTargets);

            try
            {
                int count = ExecuteHitBox(data, raycastHits);
                int hitCount = 0;
                for (int i = 0; i < count; i++)
                {
                    IHitAble hitAble = raycastHits[i].transform.GetComponent<IHitAble>();
                    if (hitAble == null)
                        continue;

                    DefaultHitResult hitResult = hitAble.Hit<DefaultHitResult>(new HitInfo()
                    {
                        hitObject = transform.gameObject,
                    });

                    if (hitResult.HasHitError())
                        continue;

                    ++hitCount;

                    Debug.Log(raycastHits[i].transform.name);

                    if (hitCount >= maxTargets)
                        break;
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#endif
            finally
            {
                ArrayPool<RaycastHit>.Shared.Return(raycastHits, true);
            }
        }

        private int ExecuteHitBox(AttackDataScriptableObject data, RaycastHit[] raycastHits)
        {
            int count = 0;

            switch (data.HitBoxType)
            {
                case HitBoxType.Line:
                    count = Physics.RaycastNonAlloc(hitScanOrigin.TransformPoint(data.Offset),
                        hitScanOrigin.forward,
                        raycastHits,
                        data.Length,
                        data.LayerMask);
                    break;
                case HitBoxType.Box:
                    count = Physics.BoxCastNonAlloc(hitScanOrigin.TransformPoint(data.Offset),
                        data.BoxSize,
                        hitScanOrigin.forward,
                        raycastHits,
                        hitScanOrigin.rotation,
                        data.Length,
                        data.LayerMask);
                    break;
                case HitBoxType.Sphere:
                    count = Physics.SphereCastNonAlloc(hitScanOrigin.TransformPoint(data.Offset),
                        data.Radius,
                        hitScanOrigin.forward,
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