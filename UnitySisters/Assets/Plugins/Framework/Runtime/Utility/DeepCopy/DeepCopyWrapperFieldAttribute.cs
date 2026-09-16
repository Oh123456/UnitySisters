using System;

namespace UnityFramework.Utility
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public sealed class DeepCopyWrapperFieldAttribute : Attribute
    {
        public string SetterName { get; }
        public string GetterName { get; }

        public DeepCopyWrapperFieldAttribute(string setterName)
            : this(setterName, null)
        {
        }

        public DeepCopyWrapperFieldAttribute(string setterName, string getterName)
        {
            SetterName = setterName;
            GetterName = getterName;
        }
    }
}
