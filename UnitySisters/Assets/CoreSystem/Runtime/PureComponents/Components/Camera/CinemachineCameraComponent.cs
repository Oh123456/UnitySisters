using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using Unity.Cinemachine;
using UnityEngine;

namespace CoreSystem.Components
{
    public class CinemachineCameraComponent : PureComponent, IAwakeHandle, ILateUpdateHandle, System.IDisposable
    {
        [PureComponentField]
        private CinemachineCamera cameraObject;
        private Transform targetObject;
        private CinemachineFollow cinemachineFollow;
        [PureComponentField]
        private CinemachineCameraData cinemachineCameraData = new CinemachineCameraData();

        [PureComponentField]
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
            currentRotation.y = (Mathf.Asin(offset.y * inv_Distance) * Mathf.Rad2Deg);

            currentRotation = ClampAngle(currentRotation);

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


            Vector2 rotationValue = cinemachineCameraData.RotationValue;
            if (rotationValue.Equals(Vector2.zero))
                return;


            float speed = cinemachineCameraData.Speed;

            Vector3 followOffset = cinemachineFollow.FollowOffset;

            currentRotation.x += speed * rotationValue.x * Time.deltaTime;                
            currentRotation.y += speed * rotationValue.y * Time.deltaTime;

            currentRotation = ClampAngle(currentRotation);


            float distance = cinemachineCameraData.Distance;

            followOffset.x = Mathf.Cos(currentRotation.x * Mathf.Deg2Rad) * distance;
            followOffset.z = Mathf.Sin(currentRotation.x * Mathf.Deg2Rad) * distance;

            followOffset.y = Mathf.Sin(currentRotation.y * Mathf.Deg2Rad) * distance;

            cinemachineFollow.FollowOffset = followOffset;
        }

        private Vector2 ClampAngle(Vector2 angle)
        {
            CinemachineCameraLookData cinemachineCameraLookData = cinemachineCameraData.CinemachineCameraLookData;


            float maxYaw = cinemachineCameraLookData.maxYaw;
            float minYaw = cinemachineCameraLookData.minYaw;
            float maxPitch = cinemachineCameraLookData.maxPitch;
            float minPitch = cinemachineCameraLookData.minPitch;

            if (!maxYaw.Equals(minYaw))
                angle.x = Mathf.Clamp(currentRotation.x, minYaw, maxYaw);

            if (!maxPitch.Equals(minPitch))
                angle.y = Mathf.Clamp(currentRotation.y, minPitch, maxPitch);

            return angle;
        }

        public void Dispose()
        {
            cameraObject = null;
            targetObject = null;
            cinemachineFollow = null;
        }


    }

}