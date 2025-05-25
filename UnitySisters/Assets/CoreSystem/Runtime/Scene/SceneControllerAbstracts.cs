using CoreSystem.Controllers;
using UnityEngine;

namespace CoreSystem.Scenes
{
    public abstract partial class SceneController
    {
        protected abstract BaseController CreateController();
    }
}
