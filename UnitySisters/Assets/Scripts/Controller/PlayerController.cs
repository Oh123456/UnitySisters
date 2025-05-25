using CoreSystem.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class PlayerController : BaseController
{
    protected override void ClearInputAction(IInputActionCollection2 inputActions)
    {
        if (inputActions is PlayerInputSystem input)
        {
            PlayerInputSystem.MainGameActions mainGameActions = input.MainGame;
            InputAction move = mainGameActions.Move;
            move.performed -= MoveCharacter;
            move.Disable();
        }
    }

    protected override void SetInputAction(IInputActionCollection2 inputActions)
    {
        if (inputActions is PlayerInputSystem input)
        {
            PlayerInputSystem.MainGameActions mainGameActions = input.MainGame;
            InputAction move = mainGameActions.Move;
            move.performed += MoveCharacter;
            move.Enable();
        }
    }

    private void MoveCharacter(CallbackContext callbackContext)
    {
        Vector2 moveValue = callbackContext.ReadValue<Vector2>();
        //TODO:: 캐릭터 컴포넌트랑 합치기
    }

}
