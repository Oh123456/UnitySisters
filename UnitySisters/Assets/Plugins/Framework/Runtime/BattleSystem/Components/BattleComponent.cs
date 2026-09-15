using System;
using UnityEngine;


namespace UnityFramework.BattleSystem
{
    public class BattleComponent : MonoBehaviour, IAttackAble, IHitAble
    {
        [SerializeReference, SerializeReferenceSelector] protected BattleAttributeSet battleAttributeSet;
        [SerializeReference, SerializeReferenceSelector] protected HitResolver hitResolver;
        [SerializeField] protected HitScanComponent hitScanComponent;

        public BattleAttributeSet BattleAttributeSet => battleAttributeSet;
        public T GetBattleAttributeSet<T>() where T : BattleAttributeSet => battleAttributeSet as T;

        public virtual void Attack<TAttackData>(in TAttackData attackData) where TAttackData : struct
        {
            if (hitScanComponent is IHitScaner<TAttackData> hitScaner)
            {
                hitScaner.StartHitScan(in attackData);
                return;
            }

            throw new InvalidOperationException($"{hitScanComponent.GetType().Name} cannot handle {typeof(TAttackData).Name}");            
        }

        public THitResult Hit<THitResult>(in HitInfo hitInfo) where THitResult : struct , IHitResult
        {
            LogUtility.Log("Hit");
            if (this.hitResolver == null)
            {
                LogUtility.LogWarning($"HitResolver is Null Object {gameObject.name}");
                return HitError<THitResult>(in hitInfo, (int)HitErrorCode.NoneHitResolver);
            }

            return hitResolver.Hit<THitResult>(in hitInfo);
        }

        protected THitResult HitError<THitResult>(in HitInfo hitInfo, int errorCode) where THitResult : struct, IHitResult
        {
            THitResult hitResult = default(THitResult);
            hitResult.SetHitError(errorCode);
            return hitResult;
        }
    }
}
