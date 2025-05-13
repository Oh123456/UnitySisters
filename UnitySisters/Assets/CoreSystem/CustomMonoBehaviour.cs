using PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace PureComponents
{
    public class CustomMonoBehaviour : MonoBehaviour
    {
        protected Dictionary<System.Type, PureComponent> pureComponents = new Dictionary<System.Type, PureComponent>();
        private Queue<IDestroyHandle> destroyComponentQueue = null;
        internal Queue<IDestroyHandle> DestroyComponentQueue
        {
            get
            {
                if (destroyComponentQueue == null)
                    destroyComponentQueue = new Queue<IDestroyHandle>(4);
                return destroyComponentQueue;
            }
        }

        public T AddComponent<T>() where T : PureComponent, new()
        {
            System.Type type = typeof(T);
            if (pureComponents.TryGetValue(type, out PureComponent component))
            {
                Debug.LogWarning($"{name} 에 {type.Name}은 이미 있습니다.");
                return component as T;
            }

            T TComponent = new T()
            {
                customMonoBehaviour = this,
            };

            pureComponents.Add(typeof(T), TComponent);
            return TComponent;
        }

        public bool RemoveComponent(PureComponent pureComponent)
        {
            System.Type type = pureComponent.GetType();
            if (!pureComponents.Remove(type))
                return false;
            pureComponent.Destroy();
            return true;
        }

        private void DestroyComponent()
        {
            if (destroyComponentQueue == null || destroyComponentQueue.Count < 1)
                return;

            while (destroyComponentQueue.Count > 0)
            {
                destroyComponentQueue.Dequeue().OnDestroy();
            }
        }

        protected virtual void LateUpdate()
        {
            DestroyComponent();
        }
    }

}