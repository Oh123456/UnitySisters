using System;
using System.Diagnostics;

namespace UnityFramework.FSM
{
    /// <summary>
    /// FSM Parameter로 자동 동기화할 bool, int, float 인스턴스 필드 지정
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    [Conditional("UNITY_EDITOR")]
    public sealed class FSMParameterAttribute : Attribute
    {
    }

    /// <summary>
    /// true 요청을 FSM Trigger로 전달할 bool 인스턴스 필드 지정
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    [Conditional("UNITY_EDITOR")]
    public sealed class FSMTriggerAttribute : Attribute
    {
    }

    /// <summary>
    /// 에디터가 생성한 바인딩 코드를 StateMachine과 연결하는 런타임 계약
    /// </summary>
    public interface IFSMParameterBinder
    {
        void SyncFSMParameters(IStateMachine stateMachine);
    }

    /// <summary>
    /// 필드 바인딩 키를 런타임 Parameter ID로 변환하는 결정적 Hash
    /// </summary>
    public static class FSMParameterKey
    {
        public static int GetHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("FSM Parameter key cannot be null or empty.", nameof(value));

            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
                return (int)hash;
            }
        }

#if UNITY_EDITOR
        public static string GetSourceTypeID(Type sourceType)
        {
            if (sourceType == null)
                return string.Empty;

            return $"{sourceType.Assembly.GetName().Name}:{sourceType.FullName}";
        }

        public static string GetFieldKey(Type sourceType, string fieldName)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));

            return $"{GetSourceTypeID(sourceType)}:{fieldName}";
        }

        public static int GetFieldID(Type sourceType, string fieldName)
        {
            return GetHash(GetFieldKey(sourceType, fieldName));
        }
#endif
    }
}
