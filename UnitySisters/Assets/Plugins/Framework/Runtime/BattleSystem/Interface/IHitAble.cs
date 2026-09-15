using UnityEngine;
namespace UnityFramework.BattleSystem
{
    public interface IHitAble
    {
        public THitResult Hit<THitResult>(in HitInfo hitInfo) where THitResult : struct, IHitResult;
    }
}