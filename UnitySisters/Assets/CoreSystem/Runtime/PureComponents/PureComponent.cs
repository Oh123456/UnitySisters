using CoreSystem.PureComponents.Interfaces;
using UnityEngine;

namespace CoreSystem.PureComponents
{
    public abstract class PureComponent 
    {
        protected bool isValid = true;
        private bool enabled = true;
        internal CustomMonoBehaviour customMonoBehaviour;

        public CustomMonoBehaviour CustomMonoBehaviour => customMonoBehaviour;

        public bool IsValid => isValid;

        public bool Enabled => enabled;

        internal void Destroy()
        {
            if (!isValid) return;

            isValid = false;
            
            PureComponentManager.Instance.EnqueueDestroyComponent(this);
        }

        public void SetEnabled(bool enabled)
        {
            if (!isValid || this.enabled == enabled)
                return;

            this.enabled = enabled;

            if (enabled && this is IEnableHandle enableHandle)
                enableHandle.OnEnable();
            else if (!enabled && this is IDisableHandle disableHandle)
                disableHandle.OnDisable();
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