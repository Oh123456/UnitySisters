using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityFramework.PoolObject;

namespace UnityFramework.Pool.Manager
{
    public class MonoPoolManager
    {
        Dictionary<PoolKey, Pool> monoPool = new Dictionary<PoolKey, Pool>();

        Transform monoPoolRepository;

        public MonoPoolManager()
        {
            GameObject gameObject = new GameObject("MonoPoolRepository");
            monoPoolRepository = gameObject.transform;
            GameObject.DontDestroyOnLoad(monoPoolRepository);
        }

        public T GetObject<T>(IPoolObject prefab, Transform parents = null, bool isAutoActivate = true) where T : MonoBehaviour, IMonoPoolObject, new()
        {
            Pool pool = GetPool<T>(prefab);
            IPoolObject poolObject = pool.GetObject();
            if (isAutoActivate)
                poolObject.Activate();
            T tPoolObject = ((T)poolObject);
            Transform transform = tPoolObject.transform;
            transform.SetParent(parents);
            transform.localScale = ((T)prefab).transform.localScale;
            return tPoolObject;
        }

        public void SetObject<T>(IPoolObject poolObject, bool isAutoDeactivate = true) where T : MonoBehaviour, IMonoPoolObject
        {
            MonoBehaviour mono = (MonoBehaviour)poolObject;
            if (!FIndPool(mono, out Pool pool))
            {
                Debug.Log("임시");
                return;
            }
            if (isAutoDeactivate)
                poolObject.Deactivate();
            mono.transform.parent = monoPoolRepository;
            pool.SetObject(poolObject);
        }

        Pool GetPool<T>(IPoolObject poolObject) where T : MonoBehaviour, IMonoPoolObject, new()
        {
            PoolKey poolKey = new PoolKey((MonoBehaviour)poolObject);

            if (!monoPool.TryGetValue(poolKey, out Pool pool))
            {
                ((IMonoPoolObject)poolObject).KeyCode = poolKey.GetHashCode();
                pool = new MonoPool<T>(poolObject);
                monoPool.Add(poolKey, pool);
            }

            return pool;
        }

        bool FIndPool(MonoBehaviour monoBehaviour, out Pool pool)
        {
            PoolKey poolKey = new PoolKey(monoBehaviour);

            return monoPool.TryGetValue(poolKey, out pool);
        }
    }
}