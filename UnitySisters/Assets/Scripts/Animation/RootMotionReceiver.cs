using UnityEngine;

namespace UnitySisters
{
    public class RootMotionReceiver : MonoBehaviour
    {
        [SerializeField] private CharacterController characterController;
        private Animator animator;

        private void Reset()
        {
            characterController = transform.parent?.GetComponent<CharacterController>();
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            Vector3 deltaPosition = animator.deltaPosition;
            characterController?.Move(animator.deltaPosition);
        }

    }

}