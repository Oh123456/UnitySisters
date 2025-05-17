using CoreSystem.PureComponents;
using System.Collections.Generic;
using UnityFramework.Singleton;

namespace CoreSystem
{
	public class PureComponentManager : LazySingleton<PureComponentManager>
	{
        #region Update
        private UpdateHandleData updateHandleData = new UpdateHandleData();

        internal UpdateHandleData UpdateHandleData => updateHandleData;
        #endregion


        #region Destroy
        private Queue<PureComponent> destroyComponentQueue = new Queue<PureComponent>(4);

        internal event System.Action OnDestroyComponentQueue;

        internal void EnqueueDestroyComponent(PureComponent pureComponent)
        {
            if (destroyComponentQueue.Count == 0)
                OnDestroyComponentQueue?.Invoke();
            destroyComponentQueue.Enqueue(pureComponent);
            
        }

        internal PureComponent DequeueDestroyComponent()
        {
            if (destroyComponentQueue.Count == 0)
                return null;
            return destroyComponentQueue.Dequeue();
        }


        #endregion

    }

}