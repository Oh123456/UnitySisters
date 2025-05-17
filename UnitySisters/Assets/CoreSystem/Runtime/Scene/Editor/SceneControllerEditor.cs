using UnityEngine;

namespace CoreSystem
{
    public partial class SceneController
    {
        private void OnValidate()
        {
            var sceneControllers = FindObjectsByType<SceneController>(FindObjectsInactive.Include,FindObjectsSortMode.InstanceID);
            if (sceneControllers.Length > 2)
                Debug.LogError("SceneController only One!!");
        }
    }
}
