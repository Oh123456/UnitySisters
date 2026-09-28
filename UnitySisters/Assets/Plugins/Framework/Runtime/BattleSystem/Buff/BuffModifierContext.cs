using UnityEngine;
using UnityFramework.Pool;

namespace UnityFramework.BattleSystem
{
    public abstract class BuffModifierContext : PoolObject.ClassPoolObject
    {

        public float additiveValue;
        public float multiplier;

        public override void Activate()
        {
            base.Activate();
            multiplier = 0.0f;
            additiveValue = 0.0f;
        }

        public override void Deactivate()
        {
            base.Deactivate();
            multiplier = 0.0f;
            additiveValue = 0.0f;
        }

        public abstract float Calculate(float baseValue);

    }

    public class DefaultBuffModifierContext : BuffModifierContext
    {

        public override float Calculate(float baseValue)
        {
            return (baseValue + additiveValue) * (1.0f + multiplier);
        }
    }

}