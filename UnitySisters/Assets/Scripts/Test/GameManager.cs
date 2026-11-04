using UnityEngine;
using UnityEngine.AddressableAssets;
using UnitySisters.Manager;
using UnitySisters.Controller;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Character testCharacter;
    [SerializeField] private AIController ai;
    [SerializeField] private Character aiCharacter;
    [SerializeField] private Key swapKey = Key.Tab;
    private bool swapped;

    void Start()
    {

        DataManager.Instance.LoadDatas();
        player.ConnectCharacter(testCharacter);
        if (ai != null && aiCharacter != null)
            ai.ConnectCharacter(aiCharacter, testCharacter.transform);

    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[swapKey].wasPressedThisFrame)
            SwapControllers();
    }

    public void SwapControllers()
    {
        if (player == null || ai == null || testCharacter == null || aiCharacter == null)
            return;
        player.DisconnectCharacter();
        ai.DisconnectCharacter();
        swapped = !swapped;
        Character playerCharacter = swapped ? aiCharacter : testCharacter;
        Character controlledAI = swapped ? testCharacter : aiCharacter;
        player.ConnectCharacter(playerCharacter);
        ai.ConnectCharacter(controlledAI, playerCharacter.transform);
    }

}
