using Unity.Cinemachine;
using UnityEngine;
using UnitySisters.Command;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{

    [System.Serializable]
    public class CameraControiller : IModelBinder<CameraCotrolModel>
    {
        [Header("Angle")]
        [SerializeField] private float minPitch = -60.0f;
        [SerializeField] private float maxPitch = 20.0f;

        private CameraCotrolModel cameraCotrolModel;
        public void SetModel(CameraCotrolModel cameraCotrolModel) => this.cameraCotrolModel = cameraCotrolModel;

        public void ExecuteRotation(CharacterCommand command)
        {
            if (cameraCotrolModel == null ||
                !command.isCameraControlAble)
                return;
            Vector2 rotation = command.cameraRotation * Time.deltaTime;

            Vector3 local = cameraCotrolModel.carmeraTarget.localEulerAngles;

            local.x += rotation.y;
            if (local.x > 180.0f)
                local.x -= 360.0f;
            local.x = Mathf.Clamp(local.x, minPitch, maxPitch);
            local.y += rotation.x;

            cameraCotrolModel.carmeraTarget.localEulerAngles = local;
        }
    } 
}
