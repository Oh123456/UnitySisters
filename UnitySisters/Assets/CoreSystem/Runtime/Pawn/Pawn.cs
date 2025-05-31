using CoreSystem.PureComponents;
using UnityEngine;

namespace CoreSystem
{
    using Controllers;
    using CoreSystem.Components;
    using Unity.Cinemachine;

    public class Pawn : CustomMonoBehaviour
    {
        private BaseController baseController;

        internal void RemoveController()
        {
            baseController = null;
        }

        public void SetCamera(CinemachineCamera camera)
        {
            if (!GetPureComponent<CinemachineCameraComponent>(out var component))
                return;

            component.SetCaermea(camera);
        }
    }

}