using System;

using UnityEngine;

namespace UnityFramework
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializeReferenceSelectorAttribute : PropertyAttribute
    {
        public SerializeReferenceSelectorAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SerializeReferenceTooltipAttribute : Attribute
    {
        public string Tooltip { get; }

        public SerializeReferenceTooltipAttribute(string tooltip)
        {
            Tooltip = tooltip;
        }
    }
}
