using UnityEngine;

namespace UnityFramework.Utility
{
    [System.Serializable]
    public class InterfaceReference<TInterface> where TInterface : class
    {
        [SerializeField] private MonoBehaviour component;

        private InterfaceReference()
        {
        }

        public TInterface GetInterface() => component as TInterface;

        public static implicit operator TInterface(InterfaceReference<TInterface> reference)
        {
            return reference.GetInterface();
        }
    }

}