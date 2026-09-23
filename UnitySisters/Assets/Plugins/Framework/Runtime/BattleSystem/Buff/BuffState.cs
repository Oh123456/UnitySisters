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
            
        }
    }
}