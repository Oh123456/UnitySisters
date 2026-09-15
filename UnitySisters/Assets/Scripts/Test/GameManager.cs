using UnityEngine;
using UnityEngine.AddressableAssets;
using UnitySisters.Manager;

public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Character testCharacter;

    void Start()
    {

        DataManager.Instance.LoadDatas();
        player.ConnectCharacter(testCharacter);

    }

}
