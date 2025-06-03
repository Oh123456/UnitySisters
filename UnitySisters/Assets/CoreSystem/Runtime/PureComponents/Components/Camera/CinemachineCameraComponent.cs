using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using Unity.Cinemachine;
using UnityEngine;

namespace CoreSystem.Components
{
    public class CinemachineCameraComponent : PureComponent, IAwakeHandle, ILateUpdateHandle, System.IDisposable
    {
        private CinemachineCamera cameraObject;
        private Transform targetObject;
        private CinemachineFollow cinemachineFollow;
        private CinemachineCameraData cinemachineCameraData = new CinemachineCameraData();

        private Vector2 currentRotation;

        public CinemachineCameraData CinemachineCameraData => cinemachineCameraData;

        public void Awake()
        {
            targetObject = CustomMonoBehaviour.transform;
        }

        public void SetCaermea(CinemachineCamera camera)
        {
            cameraObject = camera;
            cameraObject.TryGetComponent<CinemachineFollow>(out cinemachineFollow);

            var offset = cinemachineFollow.FollowOffset;

            cinemachineCameraData.SetDistance(offset.z);

            float distance = cinemachineCameraData.Distance;

            float inv_Distance = 1 / distance;

            currentRotation.x = (Mathf.Atan2(offset.z, offset.x)) * Mathf.Rad2Deg;
            currentRotation.y = ((offset.y * inv_Distance) * CinemachineCameraData.MAX_PITCH);

            SetTarget(targetObject);
        }

        public void SetTarget(Transform target)
        {
            targetObject = target;
            CameraTarget cameraTarget = cameraObject.Target;
            cameraTarget.TrackingTarget = targetObject;
            cameraObject.Target = cameraTarget;
        }
        public void LateUpdate()
        {
            if (cinemachineFollow == null)
                return;


            Vector2 inputValue = cinemachineCameraData.RotationValue;
            if (inputValue.Equals(Vector2.zero))
                return;

            CinemachineCameraLookData cinemachineCameraLookData = cinemachineCameraData.CinemachineCameraLookData;

            float speed = cinemachineCameraData.Speed;

            Vector3 followOffset = cinemachineFollow.FollowOffset;

            float maxYaw = cinemachineCameraLookData.maxYaw;
            float minYaw = cinemachineCameraLookData.minYaw;
            float maxPitch = cinemachineCameraLookData.maxPitch;
            float minPitch = cinemachineCameraLookData.minPitch;

            currentRotation.x += speed * inputValue.x * Time.deltaTime;
            if (!maxYaw.Equals(minYaw))
                currentRotation.x = Mathf.Clamp(currentRotation.x, minYaw, maxYaw);

            currentRotation.y += speed * inputValue.y * Time.deltaTime;
            if (!maxPitch.Equals(minPitch))
                currentRotation.y = Mathf.Clamp(currentRotation.y, minPitch, maxPitch);


            float distance = cinemachineCameraData.Distance;

            followOffset.x = Mathf.Cos(currentRotation.x * Mathf.Deg2Rad) * distance;
            followOffset.z = Mathf.Sin(currentRotation.x * Mathf.Deg2Rad) * distance;

            followOffset.y = (currentRotation.y * CinemachineCameraData.INVERSE_MAX_PITCH) * distance;

            cinemachineFollow.FollowOffset = followOffset;
        }

        public void Dispose()
        {
            cameraObject = null;
            targetObject = null;
            cinemachineFollow = null;
        }


    }

}