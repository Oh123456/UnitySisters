using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public enum DefaultHitResultType
    {
        Hit,
        Blocked,
        Dodged,
        Invincible,






        Error,
    }

    public struct DefaultHitResult : IHitResult
    {
        public DefaultHitResultType defaultHitResultType;
        public int errorCode;

        public bool HasHitError()
        {
            bool isError = errorCode != (int)HitErrorCode.None;
            if (isError)
                LogUtility.Log($"Hit Error Code {(HitErrorCode)errorCode}");
            return isError;
        }

        public void SetHitError(int errorCode)
        {
            this.errorCode = errorCode;
            this.defaultHitResultType = DefaultHitResultType.Error;
        }
    }

}