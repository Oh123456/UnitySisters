using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IHitScaner<THitScanData> where THitScanData : struct
    {
        public void StartHitScan(in THitScanData hitScanData);
    } 
}