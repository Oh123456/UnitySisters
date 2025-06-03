using CoreSystem.Controllers;
using CoreSystem.Scenes;
using UnityEngine;

public class MainGameSceneController : SceneController
{
    // 임시
    [SerializeField] GameObject mainPlayerObject;
    protected override BaseController CreateController()
    {
        return new PlayerController();
    }

    protected override void Initialize()
    {
        base.Initialize();
        Transform controlPawn = controller.ControlPawn.transform;
        controlPawn.SetParent(mainPlayerObject.transform);
        controlPawn.localPosition = Vector3.zero;
        controlPawn.localRotation = Quaternion.identity;
    }
}
