using System;
using System.Collections.Generic;
using UnityFramework.Pool;

namespace UnityFramework.BattleSystem
{
    public abstract class BuffModifierContainer
    {
        protected Dictionary<int, IBuffModifier> buffModifierDic = new Dictionary<int, IBuffModifier>();

        public abstract void Initialize();

        public bool TryGetBuffModifier(int id, out IBuffModifier buffModifier)
        {
            return buffModifierDic.TryGetValue(id, out buffModifier);
        }

        public abstract BuffModifierContext GetBuffModifierContext();

        public abstract void SetBuffModifierContext(BuffModifierContext buffModifierContext);

    }

    public abstract class BuffModifierContainer<TBuffModifierContext> : BuffModifierContainer 
        where TBuffModifierContext : BuffModifierContext , new()
    {
        public override BuffModifierContext GetBuffModifierContext()
        {
            return PoolManager.GetClassObject<TBuffModifierContext>();
        }

        public override void SetBuffModifierContext(BuffModifierContext buffModifierContext)
        {
            if (!CheckBuffModifierContextType(buffModifierContext.GetType()))
                return;
            PoolManager.SetClassObject(buffModifierContext);
        }

        private bool CheckBuffModifierContextType(System.Type type)
        {
            bool isOk = type == typeof(TBuffModifierContext);
            if (!isOk)
                throw new ArgumentException($"Type mismatch. Expected {typeof(TBuffModifierContext)}, but received {type}.", nameof(type));
            return isOk;
        }
    }

}