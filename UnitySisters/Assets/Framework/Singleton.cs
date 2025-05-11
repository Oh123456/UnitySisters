using System;

using UnityEngine;

namespace UnityFramework.Singleton
{
    public class LazySingleton<T> where T : class, new()
    {
        private readonly static Lazy<T> instance = new Lazy<T>(CreateInstance);

        public static T Instance => instance.Value;

        private static T CreateInstance()
        {
            return new T();
        }
    }

    /// <summary>
    /// Not Thread Safe
    /// </summary>
    public class Singleton<T> where T : class, new()
    {
        static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                    instance = new T();

                return instance;
            }
        }

    }

    namespace UnSafe
    {
        public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
        {
            protected static T instance;

            public static T Instance
            {
                get
                {
                    if (instance == null)
                        Debug.Log("Not Call Awake");

                    return instance;
                }
            }

            private void Awake()
            {
                Initiation();
            }

            /// <summary>
            /// Call Awake
            /// </summary>
            protected virtual void Initiation()
            {
                if (instance == null)
                {
                    instance = this as T;
                    DontDestroyOnLoad(gameObject);
                }
            }

        }
    }

}