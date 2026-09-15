using UnityEngine;
using UnityFramework.PoolObject;
namespace UnityFramework.BattleSystem
{
    public abstract class HitScanComponent : MonoBehaviour
    {
        [SerializeField] protected Transform hitScanOrigin;
    }

    public abstract class HitScanComponent<T> : HitScanComponent, IHitScaner<T> where T : struct
    {
        public abstract void StartHitScan(in T hitScanData);
    }

}