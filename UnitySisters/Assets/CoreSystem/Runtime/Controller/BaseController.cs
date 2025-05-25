using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreSystem.Controllers
{
    public abstract class BaseController: System.IDisposable
    {
        internal Pawn controlPawn;
        public Pawn ControlPawn => controlPawn;

        public BaseController()
        {
            SetInputAction(InputManager.Instance.ActionCollection);
        }

        public void Dispose()
        {
            ClearInputAction(InputManager.Instance.ActionCollection);
        }

        protected abstract void SetInputAction(IInputActionCollection2 inputActions);
        protected abstract void ClearInputAction(IInputActionCollection2 inputActions);

    }

}