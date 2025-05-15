using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem
{
    public partial class SceneController : MonoBehaviour
    {

        private List<IUpdateHandle> updateHandles;
        private List<ILateUpdateHandle> lateUpdateHandles;
        private List<IFixedUpdateHandle> fixedUpdateHandles;
        private bool isDestructionScheduled;

        private void Awake()
        {
            Initialize();
        }

        protected virtual void Update()
        {
            int count = updateHandles.Count;
            for (int i = 0; i < count; i++)
                updateHandles[i].Update();
        }

        protected virtual void FixedUpdate()
        {
            int count = fixedUpdateHandles.Count;
            for (int i = 0; i < count; i++)
                fixedUpdateHandles[i].FixedUpdate();
        }

        protected virtual void LateUpdate()
        {
            int count = lateUpdateHandles.Count;
            for (int i = 0; i < count; i++)
                lateUpdateHandles[i].LateUpdate();

            if (isDestructionScheduled) 
                DestroyPureComponent();
        }

        protected virtual void Initialize()
        {
            PureComponentManager updateManager = PureComponentManager.Instance;
            updateHandles = updateManager.GetUpdateHandles();
            lateUpdateHandles = updateManager.GetLateUpdateHandles();
            fixedUpdateHandles = updateManager.GetFixedUpdateHandles();
            isDestructionScheduled = false;
            updateManager.OnDestroyComponentQueue += OnDestroyComponentQueue;
        }

        private void OnDestroyComponentQueue()
        {
            isDestructionScheduled = true;
        }

        private void DestroyPureComponent()
        {
            var destroyComponentQueue = PureComponentManager.Instance.GetDestroyComponentQueue();
            if (destroyComponentQueue.Count < 1)
                return;

            while (destroyComponentQueue.Count > 0)
            {
                PureComponent pureComponent = destroyComponentQueue.Dequeue();
                if (pureComponent is IDestroyHandle destroyHandle)
                    destroyHandle.OnDestroy();

                pureComponent.customMonoBehaviour.RemovePureComponent(pureComponent);

                if (pureComponent is System.IDisposable disposable)
                    disposable.Dispose();
            }

            isDestructionScheduled = false;
        }


    }
}