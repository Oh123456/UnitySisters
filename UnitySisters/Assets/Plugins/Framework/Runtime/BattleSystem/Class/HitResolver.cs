using NUnit.Framework.Internal;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public abstract class HitResolver
    {
        public THitResult Hit<THitResult>(in HitInfo hitInfo, BattleComponent soureBattleComponent) where THitResult : struct, IHitResult
        {
            if (this is HitResolver<THitResult> hitResolver)
                return hitResolver.Hit(hitInfo, soureBattleComponent);

            THitResult hitResult = default(THitResult);
            hitResult.SetHitError((int)HitErrorCode.HitResultTypeMismatch);
            return hitResult;
        }
    }

    [System.Serializable]
    public abstract class HitResolver<THitResult> : HitResolver where THitResult : struct, IHitResult
    {
        public abstract THitResult Hit(in HitInfo hitInfo, BattleComponent soureBattleComponent);
    }

}