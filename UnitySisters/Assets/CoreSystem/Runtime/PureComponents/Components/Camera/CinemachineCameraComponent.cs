using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using Unity.Cinemachine;
using UnityEngine;

namespace CoreSystem.Components
{
    public class CinemachineCameraComponent : PureComponent, IAwakeHandle , System.IDisposable
    {
        private CinemachineCamera carmeraObject;
        private Transform targetObject;

        public void Awake()
        {
            targetObject = CustomMonoBehaviour.transform;
        }

        public void SetCaermea(CinemachineCamera camera)
        {
            carmeraObject = camera;

            SetTarget(targetObject);
        }

        public void SetTarget(Transform target)
        {
            targetObject = target;
            CameraTarget cameraTarget = carmeraObject.Target;
            cameraTarget.TrackingTarget = targetObject;
            carmeraObject.Target = cameraTarget;
        }

        public void Dispose()
        {
            carmeraObject = null;
            targetObject = null;
        }

      
    }

}