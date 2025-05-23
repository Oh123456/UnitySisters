using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreSystem.Controllers
{
    public abstract class BaseController
    {
        internal Pawn controlPawn;

        public BaseController()
        {
            SetInputAction(InputManager.Instance.ActionCollection);
        }

        protected abstract void SetInputAction(IInputActionCollection2 inputActions);

    }

}