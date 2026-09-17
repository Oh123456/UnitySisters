using System;
using UnityEngine;
namespace UnityFramework.BattleSystem
{
    public abstract class HitScanComponent : MonoBehaviour
    {
        [SerializeField] protected Transform hitScanOrigin;

        public void StartHitScan<T>(in T hitScanData, BattleComponent soureBattleComponent) where T : struct
        {
            if (this is HitScanComponent<T> hitScanComponent)
            {
                hitScanComponent.StartHitScan(in hitScanData, soureBattleComponent);
                return;
            }

            throw new InvalidOperationException($"{GetType().Name} cannot handle {typeof(T).Name}");
        }

    }

    public abstract class HitScanComponent<T> : HitScanComponent where T : struct
    {
        public abstract void StartHitScan(in T hitScanData, BattleComponent soureBattleComponent);
    }

}