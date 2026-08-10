using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Character testCharacter;

    void Start()
    {
        player.ConnectCharacter(testCharacter);

    }

}
