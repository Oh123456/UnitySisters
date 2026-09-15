using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IHitResult
    {
        public void SetHitError(int errorCode);
        public bool HasHitError();
    }

}