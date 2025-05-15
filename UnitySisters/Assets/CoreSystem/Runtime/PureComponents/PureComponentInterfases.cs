using UnityEngine;

namespace CoreSystem.PureComponents.Interfaces
{
    public interface IUpdateHandle
    {
        public void Update();
    }

    public interface IFixedUpdateHandle
    {
        public void FixedUpdate();
    }

    public interface ILateUpdateHandle
    {
        public void LateUpdate();
    }

    public interface IDestroyHandle
    {
        public void OnDestroy();
    }

}
