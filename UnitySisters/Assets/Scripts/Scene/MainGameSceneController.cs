using CoreSystem.Controllers;
using CoreSystem.Scenes;
using UnityEngine;

public class MainGameSceneController : SceneController
{
    protected override BaseController CreateController()
    {
        return new PlayerController();
    }
}
