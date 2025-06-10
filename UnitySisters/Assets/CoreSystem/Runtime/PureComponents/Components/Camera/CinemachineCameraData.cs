using UnityEngine;

namespace CoreSystem.Components
{
    public struct CinemachineCameraLookData
    {
        public float minPitch;
        public float maxPitch;
        public float minYaw;
        public float maxYaw;
    }

    [PureComponentData]
    public class CinemachineCameraData 
    {
        public const float MAX_PITCH = 90.0f;
        public const float INVERSE_MAX_PITCH = 1.0f / MAX_PITCH;
        public const float MAX_YAW = 180.0f;

        /// <summary>
        /// 카메라 각도 최대최소값
        /// </summary>
        private CinemachineCameraLookData cinemachineCameraLookData = new CinemachineCameraLookData()
        {
            minPitch = -30.0f,
            maxPitch = 60.0f,
            minYaw = 0.0f,
            maxYaw = 0.0f,
        };

        public CinemachineCameraLookData CinemachineCameraLookData => cinemachineCameraLookData;
        [PureComponentField]
        private Vector2 rotationValue = Vector2.zero;
        public Vector2 RotationValue => rotationValue;
        public void SetRotationValue(Vector2 vector2)
        {
            rotationValue = vector2;    
        }

        /// <summary>
        /// 카메라 회전 속도
        /// </summary>
        private float speed = 5.0f;
        public float Speed => speed;

        /// <summary>
        /// 카메라와 타겟의 거리
        /// </summary
        private float distance = 2.0f;
        public void SetDistance(float distance)
        {
            this.distance = Mathf.Abs(distance);
        }
        public float Distance => distance;  
    }

}