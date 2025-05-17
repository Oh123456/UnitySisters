using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem.PureComponents
{
    public partial class CustomMonoBehaviour
    {
        public class PureComponentData
        {
            private Dictionary<System.Type, PureComponent> pureComponents = new Dictionary<System.Type, PureComponent>();
            /// <summary>
            /// 컴포넌트 추가
            /// </summary>
            public T AddPureComponent<T>(CustomMonoBehaviour customMonoBehaviour) where T : PureComponent, new()
            {
                System.Type type = typeof(T);
                if (pureComponents.TryGetValue(type, out PureComponent component))
                {
                    Debug.LogWarning($"{customMonoBehaviour.name} 에 {type.Name}은 이미 있습니다.");
                    return component as T;
                }

                T TComponent = new T()
                {
                    customMonoBehaviour = customMonoBehaviour,
                };

                RegisterUpdate(TComponent);

                pureComponents.Add(typeof(T), TComponent);

                if (TComponent is IEnableHandle enableHandle)
                    enableHandle.OnEnable();

                return TComponent;
            }

            /// <summary>
            /// 업데이트 등록
            /// </summary>
            private void RegisterUpdate(PureComponent component)
            {
                PureComponentManager updateManager = PureComponentManager.Instance;
                UpdateHandleData updateHandleData = updateManager.UpdateHandleData;

                updateHandleData.AddUpdateHandle(component);
                updateHandleData.AddFixedUpdateHandle(component);
                updateHandleData.AddLateUpdateHandle(component);
            }

            /// <summary>
            /// 업데이트 해지
            /// </summary>
            private void Unregister(PureComponent component)
            {
                PureComponentManager updateManager = PureComponentManager.Instance;
                UpdateHandleData updateHandleData = updateManager.UpdateHandleData;

                updateHandleData.RemoveUpdateHandle(component);
                updateHandleData.RemoveFixedUpdateHandle(component);
                updateHandleData.RemoveLateUpdateHandle(component);
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

            internal void RemoveAllPureComponent()
            {
                var enumerator = pureComponents.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    Unregister(enumerator.Current.Value);
                }

                pureComponents.Clear();

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
}