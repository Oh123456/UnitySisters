using UnityEngine;
using UnitySisters.Model;
namespace UnitySisters.Controller
{
    public class CharacterAnimationController : AnimationController
    {
        private static string STATEID_NAME = "stateID";
        private static string Y_VALUE = "Yvalue";
        private static string ADDITIONAL_JUMP = "AdditionalJump";
        protected CharacterAnimationModel model;


        private bool cacheAadditionalJump = false;
        public override void SetModel(AnimationModel t)
        {
            model = t as CharacterAnimationModel;
        }

        public override void UpdateAnimation()
        {
            animator.SetInteger(STATEID_NAME, model.stateID);
            if (model.isFalling)
                animator.SetFloat(Y_VALUE, model.yValue);

            if (cacheAadditionalJump && !model.additionalJunmp)
                animator.SetBool(ADDITIONAL_JUMP, false);
            else if (!cacheAadditionalJump && model.additionalJunmp)
                animator.SetBool(ADDITIONAL_JUMP, true);

            cacheAadditionalJump = model.additionalJunmp;
        }
    }
}