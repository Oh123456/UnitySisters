using CoreSystem.PureComponents.Interfaces;
using UnityEngine;

namespace CoreSystem.PureComponents
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
            
            PureComponentManager.Instance.EnqueueDestroyComponent(this);
        }

        public static void Destroy(PureComponent pureComponent)
        {
            var mono = pureComponent.customMonoBehaviour;
            if (mono == null)
                return;

            pureComponent.Destroy();
        }
    }

}