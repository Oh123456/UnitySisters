using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.PoolObject;


namespace UnityFramework.Pool
{


    public struct PoolKey : System.IEquatable<PoolKey>
    {

        private readonly System.Type typeKey;
        private readonly MonoBehaviour prefab;

        public PoolKey(System.Type type)
        {
            this.typeKey = type;    
            this.prefab = null;   
        }


        public PoolKey(MonoBehaviour prefab)
        {
            this.typeKey = prefab.GetType();
            this.prefab = prefab;
        }

        public bool Equals(PoolKey other)
        {
            if (prefab == null)
                return typeKey.Equals(other.typeKey);
            else
                return ((IMonoPoolObject)prefab).KeyCode.Equals(((IMonoPoolObject)other.prefab).KeyCode);
        }

        public override int GetHashCode()
        {
            int typeHash = typeKey.GetHashCode();
            if (prefab == null)
                return typeHash;

            if (prefab.gameObject.scene.IsValid() &&
                prefab is IMonoPoolObject monoPoolObject)
                return monoPoolObject.KeyCode;

            int prefabHash = prefab.GetHashCode();

            unchecked
            {
                const int FNV_OFFSET_BASIS = (int)2166136261;
                const int FNV_PRIME = 16777619;
                int hash = FNV_OFFSET_BASIS;
                hash = (hash ^ typeHash) * FNV_PRIME;
                hash = (hash ^ prefabHash) * FNV_PRIME;

                return hash;
            }
        }

    }

    public abstract class Pool
    {
        protected Stack<IPoolObject> objects = new Stack<IPoolObject>(4);

        public Pool(IPoolObject instance)
        {
        }

        public IPoolObject GetObject()
        {
            IPoolObject poolObject = null;
            bool isValid = true;
            while (isValid)
            {
                if (objects.Count == 0)
                    poolObject = CreateObject();
                else
                    poolObject = objects.Pop();
                // 혹시라도 생성되있는애가 풀에 들어와 있을경우
                // 혹은 오브젝트가 널이라면
                isValid = poolObject == null ? true : poolObject.IsValid();
            }
            return poolObject;
        }

        protected abstract IPoolObject CreateObject();

        public void SetObject(IPoolObject classObject)
        {
            objects.Push(classObject);
        }
    }

    public class ClassPool<T> : Pool where T : class, IPoolObject, new()
    {
        public ClassPool(IPoolObject instance) : base(instance)
        {
        }

        protected override IPoolObject CreateObject()
        {
            return new T();
        }
    }

    public class MonoPool<T> : Pool where T : MonoBehaviour, IMonoPoolObject
    {
        T monoInstance;
        public MonoPool(IPoolObject instance) : base(instance)
        {
            monoInstance = instance as T;
            if (monoInstance == null)
                throw new System.Exception($"{((MonoBehaviour)instance).name} Not {typeof(T).Name} is different type");
        }

        protected override IPoolObject CreateObject()
        {
            IMonoPoolObject monoPoolObject = GameObject.Instantiate(monoInstance);
            monoPoolObject.KeyCode = monoInstance.KeyCode;
            return monoPoolObject;
        }
    }

}