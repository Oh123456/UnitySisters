using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem
{
    public partial class SceneController : MonoBehaviour
    {

        //TODO:: 클래스 타입을 넣을수있게 고민해보자
        [SerializeField]
        private class Setting
        { 
     
        }

        [SerializeField]
        private Setting setting;


        private List<IUpdateHandle> cachedUpdateHandles;
        private List<ILateUpdateHandle> cachedLateUpdateHandles;
        private List<IFixedUpdateHandle> cachedFixedUpdateHandles;
        private bool isDestructionScheduled;

        private void Awake()
        {
            Initialize();
        }

        protected virtual void Update()
        {
            int count = cachedUpdateHandles.Count;
            for (int i = 0; i < count; i++)
                cachedUpdateHandles[i].Update();
        }

        protected virtual void FixedUpdate()
        {
            int count = cachedFixedUpdateHandles.Count;
            for (int i = 0; i < count; i++)
                cachedFixedUpdateHandles[i].FixedUpdate();
        }

        protected virtual void LateUpdate()
        {
            int count = cachedLateUpdateHandles.Count;
            for (int i = 0; i < count; i++)
                cachedLateUpdateHandles[i].LateUpdate();

            if (isDestructionScheduled) 
                DestroyPureComponent();
        }

        protected virtual void Initialize()
        {
            // GameManager에 현재 씬정보를 넘겨준다
            GameManager.Instance.currentSceneController = this;

            PureComponentManager updateManager = PureComponentManager.Instance;
            UpdateHandleData updateHandleData = updateManager.UpdateHandleData;

            cachedUpdateHandles = updateHandleData.UpdateHandles;
            cachedLateUpdateHandles = updateHandleData.LateUpdateHandles;
            cachedFixedUpdateHandles = updateHandleData.FixedUpdateHandles;
            isDestructionScheduled = false;
            updateManager.OnDestroyComponentQueue += OnDestroyComponentQueue;
        }

        private void OnDestroyComponentQueue()
        {
            isDestructionScheduled = true;
        }

        private void DestroyPureComponent()
        {
            PureComponentManager pureComponentManager = PureComponentManager.Instance;
            PureComponent pureComponent = pureComponentManager.DequeueDestroyComponent();

            while (pureComponent != null)
            {
                if (pureComponent is IDestroyHandle destroyHandle)
                    destroyHandle.OnDestroy();

                pureComponent.customMonoBehaviour.pureComponentData.RemovePureComponent(pureComponent);

                if (pureComponent is System.IDisposable disposable)
                    disposable.Dispose();

                pureComponent = pureComponentManager.DequeueDestroyComponent();
            }

            isDestructionScheduled = false;
        }


    }
}