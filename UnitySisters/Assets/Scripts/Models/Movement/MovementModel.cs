using UnityEngine;

namespace UnitySisters.Model
{
    public class MovementModel
    {
        public GameObject controlObject;
        public Transform rotationTarget;
        private Vector3 velocity;
        public bool isGrounded;
        public bool additionalJump;

        public Vector3 Velocity
        {
            set { velocity = value; }
            get { return velocity; }
        }

        public ref Vector3 refVelocity => ref velocity;

    }
}