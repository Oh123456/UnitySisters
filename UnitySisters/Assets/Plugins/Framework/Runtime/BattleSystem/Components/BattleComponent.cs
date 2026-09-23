using System;
using UnityEngine;


namespace UnityFramework.BattleSystem
{
    public class BattleComponent : MonoBehaviour, IAttackAble, IHitAble
    {
        [SerializeReference, SerializeReferenceSelector] protected BattleAttributeSet battleAttributeSet;
        [SerializeReference, SerializeReferenceSelector] protected HitResolver hitResolver;
        [SerializeField] protected HitScanComponent hitScanComponent;
        protected BuffContainer buffContainer;

        public BattleAttributeSet BattleAttributeSet
        {
            set
            {
                if (value == null || value == battleAttributeSet)
                    return;
                battleAttributeSet = value;
            }
            get
            {
                return battleAttributeSet;
            }
        }

        public IBuffContainer BuffContainer => buffContainer;

        protected virtual void Awake()
        {
            buffContainer = new BuffContainer();
            buffContainer.SetBattleAttributeSet(battleAttributeSet);
        }

        private void Update()
        {
            buffContainer?.Update();
        }

        public T GetBattleAttributeSet<T>() where T : BattleAttributeSet => battleAttributeSet as T;

        public virtual void Attack<TAttackData>(in TAttackData attackData) where TAttackData : struct
        {
            hitScanComponent.StartHitScan<TAttackData>(in attackData, this);
        }

        public THitResult Hit<THitResult>(in HitInfo hitInfo) where THitResult : struct , IHitResult
        {
            LogUtility.Log("Hit");
            if (this.hitResolver == null)
            {
                LogUtility.LogWarning($"HitResolver is Null Object {gameObject.name}");
                return HitError<THitResult>(in hitInfo, (int)HitErrorCode.NoneHitResolver);
            }

            return hitResolver.Hit<THitResult>(in hitInfo, this);
        }

        protected THitResult HitError<THitResult>(in HitInfo hitInfo, int errorCode) where THitResult : struct, IHitResult
        {
            THitResult hitResult = default(THitResult);
            hitResult.SetHitError(errorCode);
            return hitResult;
        }
    }
}
