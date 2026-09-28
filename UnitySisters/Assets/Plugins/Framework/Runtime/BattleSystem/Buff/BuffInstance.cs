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
        public BuffState BuffState => buffState;

        private Dictionary<IBuffModifiable, BuffModifierEntry> buffModifierEntries = new ();

        private float duration;

        public int BuffID => buffState.BuffData.BuffID;

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

        public void SetBuffData(BuffState buffState, BattleAttributeSet battleAttributeSet )
        {
            this.battleAttributeSet = battleAttributeSet;
            this.buffState = buffState;
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
    }

}
