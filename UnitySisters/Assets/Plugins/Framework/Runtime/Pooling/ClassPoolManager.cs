using System.Collections.Generic;
using UnityEngine;
using UnityFramework.PoolObject;

namespace UnityFramework.Pool.Manager
{
    public class ClassPoolManager
	{
		Dictionary<PoolKey, Pool> classPool = new Dictionary<PoolKey, Pool>();

        public T GetObject<T>(bool isAutoActivate = true) where T : class, IPoolObject, new()
        {
			Pool pool = GetPool<T>();
            IPoolObject poolObject = pool.GetObject();
			if (isAutoActivate)
                poolObject.Activate();
            return poolObject as T;
        }

        public void SetObject(IPoolObject poolObject, bool isAutoDeactivate = true)
        {
            if (!FIndPool(poolObject.GetType(), out Pool pool))
            {
                Debug.Log("임시");
                return;
            }
            if (isAutoDeactivate)
                poolObject.Deactivate();
            pool.SetObject(poolObject);
        }

        Pool GetPool<T>() where T : class, IPoolObject , new()
        {
			System.Type poolType = typeof(T);
            PoolKey poolKey = new PoolKey(poolType);

			if (!classPool.TryGetValue(poolKey, out Pool pool))
			{
				pool = new ClassPool<T>(null);
				classPool.Add(poolKey, pool);
            }

            return pool;
        }

        bool FIndPool<T>(out Pool pool) where T : class, IPoolObject
        {
            System.Type poolType = typeof(T);
            PoolKey poolKey = new PoolKey(poolType);

            return classPool.TryGetValue(poolKey, out pool);
        }


        bool FIndPool(System.Type type,  out Pool pool)
        {
            PoolKey poolKey = new PoolKey(type);

            return classPool.TryGetValue(poolKey, out pool);
        }
    } 
}
