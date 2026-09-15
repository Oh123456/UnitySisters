using UnityEngine;
using UnityFramework.Animation;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{
    [System.Serializable]
    public abstract class AnimationController : MonoBehaviour, IModelBinder<AnimationModel>
    {
        [SerializeField] protected Animator animator;
        [SerializeField] protected AnimationEventReceiver animationEventReceiver;

        public abstract void SetModel(AnimationModel t);
        public abstract void UpdateAnimation();
    }

}