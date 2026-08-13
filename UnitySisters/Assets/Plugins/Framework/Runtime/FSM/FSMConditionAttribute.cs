using System;

namespace UnityFramework.FSM
{
    /// <summary>
    /// FSM Editor의 Condition Type 목록에 표시할 enum 지정
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public sealed class FSMConditionAttribute : Attribute
    {
    }

#if UNITY_EDITOR
    public static class FSMConditionType
    {
        /// <summary>
        /// 어셈블리 버전에 영향받지 않는 조건 enum 타입 식별자 생성
        /// </summary>
        public static string GetID(Type conditionType)
        {
            if (conditionType == null)
                return string.Empty;

            return $"{conditionType.Assembly.GetName().Name}:{conditionType.FullName}";
        }

        /// <summary>
        /// int 기반이며 중복 숫자가 없는 조건 ID enum인지 검사
        /// </summary>
        public static bool IsValid(Type conditionType)
        {
            if (conditionType == null || !conditionType.IsEnum ||
                Enum.GetUnderlyingType(conditionType) != typeof(int) ||
                conditionType.IsDefined(typeof(FlagsAttribute), false) ||
                !conditionType.IsDefined(typeof(FSMConditionAttribute), false))
                return false;

            Array enumValues = Enum.GetValues(conditionType);
            for (int i = 0; i < enumValues.Length; i++)
            {
                int conditionID = (int)enumValues.GetValue(i);
                for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
                {
                    if ((int)enumValues.GetValue(duplicateIndex) == conditionID)
                        return false;
                }
            }

            return true;
        }
    }
#endif

    /// <summary>
    /// FSM Editor의 State ID Type 목록에 표시할 enum 지정
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public sealed class FSMStateIDAttribute : Attribute
    {
    }

#if UNITY_EDITOR
    public static class FSMStateIDType
    {
        public static string GetID(Type stateIDType)
        {
            if (stateIDType == null)
                return string.Empty;

            return $"{stateIDType.Assembly.GetName().Name}:{stateIDType.FullName}";
        }

        /// <summary>
        /// int 기반이며 중복 숫자가 없는 State ID enum인지 검사
        /// </summary>
        public static bool IsValid(Type stateIDType)
        {
            if (stateIDType == null || !stateIDType.IsEnum ||
                Enum.GetUnderlyingType(stateIDType) != typeof(int) ||
                stateIDType.IsDefined(typeof(FlagsAttribute), false) ||
                !stateIDType.IsDefined(typeof(FSMStateIDAttribute), false))
                return false;

            Array enumValues = Enum.GetValues(stateIDType);
            for (int i = 0; i < enumValues.Length; i++)
            {
                int stateID = (int)enumValues.GetValue(i);
                for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
                {
                    if ((int)enumValues.GetValue(duplicateIndex) == stateID)
                        return false;
                }
            }

            return true;
        }
    }
#endif
}
