using System;
using UnityEngine;

namespace UnityFramework.FSM
{
    public enum FSMTransitionMode
    {
        Manual = 0,
        Automatic = 1
    }

    public enum FSMParameterType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        Trigger = 3
    }

    public enum FSMConditionKind
    {
        Parameter = 0,
        Custom = 1
    }

    public enum FSMParameterComparison
    {
        Equal = 0,
        NotEqual = 1,
        Greater = 2,
        Less = 3,
        GreaterOrEqual = 4,
        LessOrEqual = 5
    }

    [Serializable]
    public sealed class FSMParameterData
    {
        [SerializeField] private int id;
        [SerializeField] private string name;
        [SerializeField] private FSMParameterType type;
        [SerializeField] private bool defaultBoolValue;
        [SerializeField] private int defaultIntValue;
        [SerializeField] private float defaultFloatValue;
#if UNITY_EDITOR
        [SerializeField] private string bindingKey;
#endif

        internal FSMParameterData(int id, string name, FSMParameterType type)
        {
            this.id = id;
            this.type = type;
            SetName(name);
        }

        /// <summary>
        /// 에디터 표시 이름과 무관하게 런타임에서 사용할 안정적인 Parameter ID 반환
        /// </summary>
        public int GetID() => this.id;
        public string GetName() => this.name;
        public FSMParameterType GetParameterType() => this.type;
        public bool GetDefaultBoolValue() => this.defaultBoolValue;
        public int GetDefaultIntValue() => this.defaultIntValue;
        public float GetDefaultFloatValue() => this.defaultFloatValue;
#if UNITY_EDITOR
        public string GetBindingKey() => this.bindingKey;
        public bool GetIsFieldBound() => !string.IsNullOrEmpty(this.bindingKey);
#endif

        public void SetName(string name)
        {
            this.name = string.IsNullOrWhiteSpace(name)
                ? $"Parameter {this.id}"
                : name.Trim();
        }

        public void SetParameterType(FSMParameterType type)
        {
            this.type = type;
        }

        public void SetDefaultBoolValue(bool value)
        {
            this.defaultBoolValue = value;
        }

        public void SetDefaultIntValue(int value)
        {
            this.defaultIntValue = value;
        }

        public void SetDefaultFloatValue(float value)
        {
            this.defaultFloatValue = value;
        }

#if UNITY_EDITOR
        public void SetFieldBinding(string bindingKey)
        {
            this.bindingKey = bindingKey;
        }
#endif
    }

    [Serializable]
    public sealed class FSMConditionData
    {
        [SerializeField] private FSMConditionKind kind;
        [SerializeField] private int parameterID;
        [SerializeField] private FSMParameterComparison comparison;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private int customConditionID;
        [SerializeField] private bool customExpectedResult = true;

        internal FSMConditionData(FSMConditionKind kind)
        {
            this.kind = kind;
        }

        public FSMConditionKind GetConditionKind() => this.kind;
        public int GetParameterID() => this.parameterID;
        public FSMParameterComparison GetComparison() => this.comparison;
        public bool GetBoolValue() => this.boolValue;
        public int GetIntValue() => this.intValue;
        public float GetFloatValue() => this.floatValue;
        public int GetCustomConditionID() => this.customConditionID;
        public bool GetCustomExpectedResult() => this.customExpectedResult;

        /// <summary>
        /// 조건을 지정한 Parameter의 값 비교 방식으로 설정
        /// </summary>
        public void SetParameter(int parameterID)
        {
            this.kind = FSMConditionKind.Parameter;
            this.parameterID = parameterID;
        }

        public void SetComparison(FSMParameterComparison comparison)
        {
            this.comparison = comparison;
        }

        public void SetBoolValue(bool value)
        {
            this.boolValue = value;
        }

        public void SetIntValue(int value)
        {
            this.intValue = value;
        }

        public void SetFloatValue(float value)
        {
            this.floatValue = value;
        }

        /// <summary>
        /// 조건을 게임 코드에서 제공하는 Custom Condition 방식으로 설정
        /// </summary>
        public void SetCustomCondition(int conditionID)
        {
            this.kind = FSMConditionKind.Custom;
            this.customConditionID = conditionID;
        }

        public void SetCustomExpectedResult(bool expectedResult)
        {
            this.customExpectedResult = expectedResult;
        }
    }
}
