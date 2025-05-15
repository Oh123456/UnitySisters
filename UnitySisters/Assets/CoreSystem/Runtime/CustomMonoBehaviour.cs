using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem.PureComponents
{
    public class CustomMonoBehaviour : MonoBehaviour
    {
        protected Dictionary<System.Type, PureComponent> pureComponents = new Dictionary<System.Type, PureComponent>();
       

        /// <summary>
        /// 컴포넌트 추가
        /// </summary>
        public T AddPureComponent<T>() where T : PureComponent, new()
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

            RegisterUpdate(TComponent);

            pureComponents.Add(typeof(T), TComponent);
            return TComponent;
        }

        /// <summary>
        /// 업데이트 등록
        /// </summary>
        private void RegisterUpdate(PureComponent component)
        {
            PureComponentManager updateManager = PureComponentManager.Instance;

            if (component is IUpdateHandle updateHandle)
                updateManager.GetUpdateHandles().Add(updateHandle);

            if (component is IFixedUpdateHandle fixedUpdateHandle)
                updateManager.GetFixedUpdateHandles().Add(fixedUpdateHandle);

            if (component is ILateUpdateHandle lateUpdateHandle)
                updateManager.GetLateUpdateHandles().Add(lateUpdateHandle);
        }

        /// <summary>
        /// 업데이트 해지
        /// </summary>
        private void Unregister(PureComponent component)
        {
            PureComponentManager updateManager = PureComponentManager.Instance;

            if (component is IUpdateHandle updateHandle)
                updateManager.GetUpdateHandles().Remove(updateHandle);

            if (component is IFixedUpdateHandle fixedUpdateHandle)
                updateManager.GetFixedUpdateHandles().Remove(fixedUpdateHandle);

            if (component is ILateUpdateHandle lateUpdateHandle)
                updateManager.GetLateUpdateHandles().Remove(lateUpdateHandle);
        }

        /// <summary>
        /// 컴포넌트를 제거한다
        /// </summary>
        /// <param name="pureComponent"></param>
        internal void RemovePureComponent(PureComponent pureComponent)
        {
            System.Type type = pureComponent.GetType();
            if (!pureComponents.Remove(type))
                return;

            Unregister(pureComponent);
            return;
        }

        /// <summary>
        /// 컴포넌트 찾기
        /// </summary>
        public T GetPureComponent<T>() where T : PureComponent
        {
            System.Type type = typeof(T);
            if (pureComponents.TryGetValue(type, out PureComponent component))
            {
                return component as T;
            }

            return null;    
        }

        /// <summary>
        /// 컴포넌트 찾기
        /// </summary>
        public bool GetPureComponent<T>(out T pureComponent) where T : PureComponent
        {
            System.Type type = typeof(T);
            if (pureComponents.TryGetValue(type, out PureComponent component))
            {
                pureComponent = component as T;
                return true;

            }
            pureComponent = null;
            return false;
        }

    }

}