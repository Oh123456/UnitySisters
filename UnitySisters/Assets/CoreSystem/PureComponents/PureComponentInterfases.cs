using UnityEngine;

namespace PureComponents.Interfaces
{
    public interface IUpdateHandle
    {
        public void Update();
    }

    public interface IDestroyHandle
    {
        public void OnDestroy();
    }

}
