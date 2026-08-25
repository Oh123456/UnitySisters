using UnityEngine;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{
    [System.Serializable]
    public abstract class AnimationController : MonoBehaviour, IModelBinder<AnimationModel>
    {
        [SerializeField] protected Animator animator;

        public abstract void SetModel(AnimationModel t);
        public abstract void UpdateAnimation();
    }

}