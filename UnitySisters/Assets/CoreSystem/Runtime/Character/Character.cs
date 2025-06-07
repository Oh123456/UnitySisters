using CoreSystem.Components;
using Unity.Cinemachine;
using UnityEngine;

namespace CoreSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class Character : Pawn
    {
        protected override void InitializePureComponent()
        {
            AddPureComponent<MovementComponent>();
        }

        public override void SetCamera(CinemachineCamera camera)
        {
            base.SetCamera(camera);

            if (GetPureComponent<MovementComponent>(out var pureComponent))
            {
                pureComponent.MovementData.SetControlCamera(camera.gameObject);
            }
        }
    }

}