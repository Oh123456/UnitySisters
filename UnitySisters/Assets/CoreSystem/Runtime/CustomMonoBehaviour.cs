using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem.PureComponents
{
    public partial class CustomMonoBehaviour : MonoBehaviour
    {
        internal PureComponentData pureComponentData = new PureComponentData();

        public T AddPureComponent<T>() where T : PureComponent, new()
        {
            return pureComponentData.AddPureComponent<T>(this);
        }

        public T GetPureComponent<T>() where T : PureComponent
        {
            return pureComponentData.GetPureComponent<T>();
        }

        public bool GetPureComponent<T>(out T pureComponent) where T : PureComponent
        {
            return pureComponentData.GetPureComponent<T>(out pureComponent);
        }

        protected virtual void OnDestroy()
        {
            pureComponentData.RemoveAllPureComponent();
        }
    }

}