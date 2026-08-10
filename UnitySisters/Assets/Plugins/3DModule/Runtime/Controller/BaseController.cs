using UnityEngine;

namespace _3DModule.Controller
{
    public abstract class BaseController : MonoBehaviour
    {
        protected abstract void OnEnable();
        protected abstract void OnDisable();
    }

    public abstract class BaseController<T> : BaseController where T : BaseCharacterCommand , new()
    {
        protected T characterCommand;

        protected virtual void Awake()
        {
            characterCommand = new ();
        }
    }

}