using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityFramework.Singleton;

namespace CoreSystem
{
	public class PureComponentManager : LazySingleton<PureComponentManager>
	{
        #region Update
        private List<IUpdateHandle> updateHandles = new List<IUpdateHandle>();
        private List<ILateUpdateHandle> lateUpdateHandles = new List<ILateUpdateHandle>();
        private List<IFixedUpdateHandle> fixedUpdateHandles = new List<IFixedUpdateHandle>();

        internal List<IUpdateHandle> GetUpdateHandles() => updateHandles;
        internal List<ILateUpdateHandle> GetLateUpdateHandles() => lateUpdateHandles;
        internal List<IFixedUpdateHandle> GetFixedUpdateHandles() => fixedUpdateHandles;
        #endregion


        #region Destroy
        private Queue<PureComponent> destroyComponentQueue = new Queue<PureComponent>(4);

        internal event System.Action OnDestroyComponentQueue;

        internal Queue<PureComponent> GetDestroyComponentQueue() => destroyComponentQueue;

        internal void EnqueueDestroyComponent(PureComponent pureComponent)
        {
            if (destroyComponentQueue.Count == 0)
                OnDestroyComponentQueue?.Invoke();
            destroyComponentQueue.Enqueue(pureComponent);
            
        }

        #endregion

    }

}