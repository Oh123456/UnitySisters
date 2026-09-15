using NUnit.Framework.Internal;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public abstract class HitResolver
    {
        public abstract THitResult Hit<THitResult>(in HitInfo hitInfo) where THitResult : struct, IHitResult;
    }

}