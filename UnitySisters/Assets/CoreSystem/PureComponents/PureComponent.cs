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

        public void Destroy()
        {
            isValid = false;

            if (this is IDestroyHandle destroyHandle)
                destroyHandle.OnDestroy();
        }
    }

}