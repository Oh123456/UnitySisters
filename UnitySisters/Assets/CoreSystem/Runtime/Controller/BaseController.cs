using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreSystem.Controllers
{
    public abstract class BaseController: System.IDisposable
    {
        private Pawn controlPawn;
        public Pawn ControlPawn => controlPawn;

        public BaseController()
        {
            SetInputAction(InputManager.Instance.ActionCollection);
        }

        public virtual void Dispose()
        {
            if (controlPawn != null)
                controlPawn.RemoveController();

            ClearInputAction(InputManager.Instance.ActionCollection);
        }

        public virtual void SetControlPawn(Pawn pawn)
        {
            //이전에 컨트롤 중이면 컨트롤러 제거
            if (controlPawn != null)                
                controlPawn.RemoveController();
            

            controlPawn = pawn;
        }

        protected abstract void SetInputAction(IInputActionCollection2 inputActions);
        protected abstract void ClearInputAction(IInputActionCollection2 inputActions);

    }

}