using System;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.Pool;


namespace UnityFramework.BattleSystem
{
    internal sealed class BuffInstance : IBuffInstance, PoolObject.IPoolObject
    {
        private BuffState buffState;
        private BattleAttributeSet battleAttributeSet;
        private int stack;
        private IBattleComponent buffTaget;
        private IBattleComponent buffSource;

        private Dictionary<IBuffModifiable, BuffModifierEntry> buffModifierEntries = new ();

        private float duration;

        public int BuffID => buffState.BuffData.BuffID;

        public BuffData BuffData => buffState.BuffData;
        public int Stack
        {
            get 
            {
                return stack;
            }

            set
            {
                var buffData = buffState.BuffData;
                int newStack = Mathf.Clamp(value, 0, buffData.MaxStack);
                if (stack == newStack)
                    return;

                stack = newStack;
                if (buffData.ResetDurationOnStack)
                    duration = 0;
            }
        }

        public IReadOnlyBattleComponent BuffTaget => buffTaget;

        public IReadOnlyBattleComponent BuffSource => buffSource;

        public void SetBuffData(BuffState buffState, BattleAttributeSet battleAttributeSet, IBattleComponent buffTaget , IBattleComponent buffSource)
        {
            this.battleAttributeSet = battleAttributeSet;
            this.buffState = buffState;
            this.buffTaget = buffTaget;
            this.buffSource = buffSource;
            this.buffState.Enter(this);
            stack = 0;
        }

        public void SetBuffBuffModifier<TBattleAttributeSet>(System.Func<TBattleAttributeSet, IBuffModifiable> getAttributeSet, IBuffModifier buffModifier) 
            where TBattleAttributeSet : BattleAttributeSet
        {
            IBuffModifiable buffModifiable = getAttributeSet(battleAttributeSet as TBattleAttributeSet);

            if (!buffModifierEntries.TryGetValue(buffModifiable, out var data))
            {
                data = PoolManager.GetClassObject<BuffModifierEntry>();
                data.SetBuffModifiable(buffModifiable);
                buffModifierEntries.Add(buffModifiable, data);
            }
            data.AddBuffModifiers(buffModifier);
            buffModifiable.AddBuffModifier(buffModifier, this);
        }

        public void Reslase()
        {
            this.buffState.Exit(this);
            var e = buffModifierEntries.GetEnumerator();
            while (e.MoveNext())
            {
                var value = e.Current.Value;
                value.RemoveAllModifiers(this);
                value.ApplyModifiers();
                PoolManager.SetClassObject(value);
            }

            buffModifierEntries.Clear();
        }

        public void Activate()
        {
            stack = 0;
        }

        public void Deactivate()
        {
            buffState = null;
            buffTaget = null;
            buffSource = null;
            battleAttributeSet = null;
            buffModifierEntries.Clear();
            duration = 0.0f;
            stack = 0;
        }

        public bool IsValid()
        {
            return buffState != null;
        }

        public void StackChanged()
        {
            buffState.StackChanged(this);

            var e = buffModifierEntries.GetEnumerator();
            while (e.MoveNext())
            {
                e.Current.Value.ApplyModifiers();
            }
        }

        public void Update(float deltaTime)
        {
            buffState.Update(this, deltaTime);
            duration += deltaTime;
        }

        public bool IsExpired()
        {            
            var buffData = buffState.BuffData;
            if (buffData.BuffLifetimeType == BuffLifetimeType.Timed &&
                buffData.Duration <= duration)
            {
                if (buffData.RemoveAllOnExpire)
                    stack = 0;

                return true;
            }

            return false;
        }

        public THitResult ExecuteHit<THitResult>(HitInfo hitInfo) where THitResult : struct, IHitResult
        {
            hitInfo.hitBattleComponent = buffSource;
            return buffTaget.Hit<THitResult>(in hitInfo);
        }
    }

}
