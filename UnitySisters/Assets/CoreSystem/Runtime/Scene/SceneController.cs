using CoreSystem.Controllers;
using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem.Scenes
{
    public abstract partial class SceneController : MonoBehaviour
    {
        private List<IUpdateHandle> cachedUpdateHandles;
        private List<ILateUpdateHandle> cachedLateUpdateHandles;
        private List<IFixedUpdateHandle> cachedFixedUpdateHandles;
        private bool isDestructionScheduled;

        [SerializeField] private Character defaultCharacter;

        protected BaseController controller;

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

            InitializeUpdate();
            controller = CreateController();
            CreateCharacter();
        }

        private void InitializeUpdate()
        {

            PureComponentManager updateManager = PureComponentManager.Instance;
            UpdateHandleData updateHandleData = updateManager.UpdateHandleData;

            cachedUpdateHandles = updateHandleData.UpdateHandles;
            cachedLateUpdateHandles = updateHandleData.LateUpdateHandles;
            cachedFixedUpdateHandles = updateHandleData.FixedUpdateHandles;
            isDestructionScheduled = false;
            updateManager.OnDestroyComponentQueue += OnDestroyComponentQueue;
        }

        private void CreateCharacter()
        {
            if (defaultCharacter == null)
                return;

            Character createCharacter = GameObject.Instantiate<Character>(defaultCharacter);
            controller.SetControlPawn(createCharacter);
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

                pureComponent = pureComponentManager.DequeueDestroyComponent();
            }

            isDestructionScheduled = false;
        }


        private void OnDestroy()
        {
            controller.Dispose();
        }

    }
}