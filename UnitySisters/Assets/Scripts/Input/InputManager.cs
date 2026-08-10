using UnityEngine;

public class InputManager : UnityFramework.Singleton.LazySingleton<InputManager>
{
    private InputSystem_Actions inputActions;
    private PlayerInputSystem playInputSystem;

    public InputManager()
    {
        inputActions = new InputSystem_Actions();
    }

    public void EnableInput()
    {
        inputActions.Enable();
    }

    public void DisableInput()
    {
        inputActions.Disable();
    }

    public PlayerInputSystem GetPlayerInputSystem()
    {
        if (playInputSystem == null)
            playInputSystem = new PlayerInputSystem(inputActions);
        return playInputSystem;
    }
}
