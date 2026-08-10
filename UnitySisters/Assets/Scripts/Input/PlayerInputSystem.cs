using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystem<T> where T : IInputActionCollection
{
    protected T inputActions;

    public InputSystem(T inputActions)
    {
        this.inputActions = inputActions;
    }

}


public class PlayerInputSystem : InputSystem<InputSystem_Actions>
{
    public PlayerInputSystem(InputSystem_Actions inputActions) : base(inputActions)
    {
    }

    public InputSystem_Actions.PlayerActions GetInputAction()
    {
        return inputActions.Player;
    }
}
