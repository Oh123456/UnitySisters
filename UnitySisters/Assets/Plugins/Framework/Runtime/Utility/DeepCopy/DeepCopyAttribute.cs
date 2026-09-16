using System;

namespace UnityFramework.Utility
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public sealed class DeepCopyAttribute : Attribute
    {
    }
}
