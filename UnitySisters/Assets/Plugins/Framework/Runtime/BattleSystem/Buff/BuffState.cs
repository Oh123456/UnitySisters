using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public abstract class BuffState
    {
        protected BuffData buffData;

        public BuffData BuffData => buffData;

        public BuffState(BuffData buffData)
        {
            this.buffData = buffData;
        }

        public abstract void Enter(IBuffInstance buffInstance);
        public abstract void StackChanged(IBuffInstance buffInstance);
        public abstract void Update(IBuffInstance buffInstance, float deltaTime);
        public abstract void Exit(IBuffInstance buffInstance);
    }


    internal class TempBuffState : BuffState
    {
        internal TempBuffState(BuffData buffData) : base(buffData)
        { 
        }

        public override void Enter(IBuffInstance buffInstance)
        {
            
        }

        public override void Exit(IBuffInstance buffInstance)
        {
            
        }

        public override void StackChanged(IBuffInstance buffInstance)
        {
            
        }

        public override void Update(IBuffInstance buffInstance, float deltaTime)
        {
            var attack = buffInstance.BuffSource.GetAttributeValue<int>(static x => ((DefaultAttributeSet)x).attackAttribute);
            HitInfo hitInfo = new HitInfo()
            {
                // 셈플이기에 이렇게함 풀링필요
                damageType = new DamageType() { damage = /*buffInstance.BuffData.damage * */ attack.Value }
            };

            buffInstance.ExecuteHit<DefaultHitResult>(hitInfo);
        }
    }
}