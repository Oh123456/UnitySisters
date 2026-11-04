using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    [DisallowMultipleComponent]
    public sealed class BehaviorTreeComponent : MonoBehaviour, IBehaviorTreeOwner
    {
        [SerializeField] private BehaviorTreeData data;
        [SerializeField] private MonoBehaviour owner;
        private BehaviorTreeController controller;
        public BehaviorTreeController Controller { get => controller; }

        private void OnEnable()
        {
            if (data == null) return;
            if (controller == null) controller = data.CreateController(owner as IBehaviorTreeOwner ?? this);
            controller.Start();
        }

        private void Update() { controller?.Tick(Time.deltaTime); }
        private void OnDisable() { controller?.Stop(); }
        T IBehaviorTreeOwner.GetInterface<T>()
        {
            if (owner is T capability) return capability;
            return owner != null ? owner.GetComponent<T>() : GetComponent<T>();
        }
    }
}
