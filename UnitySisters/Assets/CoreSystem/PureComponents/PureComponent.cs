using PureComponents.Interfaces;
using UnityEngine;

namespace PureComponents
{
    public abstract class PureComponent
    {
        protected bool isValid = true;
        internal CustomMonoBehaviour customMonoBehaviour;

        public CustomMonoBehaviour CustomMonoBehaviour => customMonoBehaviour;

        public bool IsValid => isValid;

        internal void Destroy()
        {
            if (!isValid) return;

            isValid = false;

            if (this is IDestroyHandle destroyHandle)
            {
                customMonoBehaviour.DestroyComponentQueue.Enqueue(destroyHandle);
            }
        }

        public static void Destroy(PureComponent pureComponent)
        {
            var mono = pureComponent.customMonoBehaviour;
            if (mono == null)
                return;

            mono.RemoveComponent(pureComponent);
        }

    }

}