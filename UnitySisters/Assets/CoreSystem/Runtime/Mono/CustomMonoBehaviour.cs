using UnityEngine;
using UnityFramework.Pool;
using UnityFramework.PoolObject;

namespace CoreSystem.PureComponents
{
    public partial class CustomMonoBehaviour : MonoBehaviour
    {
        internal PureComponentData pureComponentData = new PureComponentData();

        protected virtual void Awake()
        {
            InitializePureComponent();
        }

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

        public bool GetAllPureComponent(out ArrayPoolObject<PureComponent> pureComponents)
        {
            int count = pureComponentData.Count;
            if (count == 0)
            {
                pureComponents = default(ArrayPoolObject<PureComponent>);
                return false;
            }

            pureComponents = PoolManager.GetArray<PureComponent>(count);

            var enumerator = pureComponentData.Enumerator;
            int index = 0;
            while (enumerator.MoveNext())
            {
                pureComponents[index++] = enumerator.Current.Value;
            }

            return true;
        }

        protected virtual void InitializePureComponent()
        {
            
        }

        //TODO:: 에디터랑 연결해보자
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void InitializeEditorPureComponent(bool focusInitialize)
        {
            if (focusInitialize)
                pureComponentData.ClearPureComponent();
            if (pureComponentData.Count == 0)
                InitializePureComponent();
        }

        protected virtual void OnDestroy()
        {
            pureComponentData.RemoveAllPureComponent();
        }
    }

}