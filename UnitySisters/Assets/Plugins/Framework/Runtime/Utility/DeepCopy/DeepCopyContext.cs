using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityFramework.Utility
{
    public sealed class DeepCopyContext
    {
        private readonly Dictionary<object, object> copies =
            new Dictionary<object, object>(ReferenceComparer.Instance);

        public bool TryGetCopy(object source, out object copy)
        {
            return copies.TryGetValue(source, out copy);
        }

        public void RegisterCopy(object source, object copy)
        {
            copies.Add(source, copy);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object instance)
            {
                return RuntimeHelpers.GetHashCode(instance);
            }
        }
    }
}
