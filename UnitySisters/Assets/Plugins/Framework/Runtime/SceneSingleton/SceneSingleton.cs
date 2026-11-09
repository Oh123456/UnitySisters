using UnityEngine;

namespace UnityFramework.Singleton
{
    public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
    {
        private bool registered;
        public static T Instance { get => SceneSingletonManager.Instance.Get<T>(); }

        protected void Awake()
        {
            if (!SceneSingletonManager.Instance.TryRegister(this))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            registered = true;
            OnSingletonAwake();
        }

        protected void OnDestroy()
        {
            if (!registered) return;
            registered = false;
            SceneSingletonManager.Instance.Unregister(this);
            OnSingletonDestroyed();
        }

        protected virtual void OnSingletonAwake() { }
        protected virtual void OnSingletonDestroyed() { }
    }
}
