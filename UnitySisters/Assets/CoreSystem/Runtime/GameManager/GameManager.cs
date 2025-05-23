using CoreSystem.GameMode;
using UnityEngine;
using UnityFramework.Singleton;

namespace CoreSystem
{
    public class GameManager : LazySingleton<GameManager>
    {
        internal SceneController currentSceneController;
        internal BaseGameMode currentGameMode;

        public SceneController GetSceneController() { return currentSceneController; }  
        public BaseGameMode GetGameMode() { return currentGameMode; }

    }

}