using FSM;
using UnityEngine;
using UnityFramework.FSM;
using UnitySisters.Controller.Interface;

namespace UnitySisters.FSM.States
{
    public class AttackState : State
    {
        public override void Enter<T>(T owner)
        {
            if ((owner is CharacterFSMCotnroller controller) &&
                controller.ControlOwner is IMoveControl moveControl)
            {
                Debug.Log("앙 기모리");
                moveControl.LockMove();
            }
        }

        public override void Exit<T>(T owner)
        {
            if ((owner is CharacterFSMCotnroller controller) &&
                controller.ControlOwner is IMoveControl moveControl)
            {
                Debug.Log("기모리");
                moveControl.UnlockMove();
            }
        }

        float temp ;

        public override void Update<T>(T owner)
        {
            // 임시 기능실제로 안쓸것

            temp += Time.deltaTime;
            if (temp > 2.0f)
                if (owner is CharacterFSMCotnroller characterFSMCotnroller)
                {
                    characterFSMCotnroller.ChangeState(0);
                    temp = 0.0f;
                }
        }
    }

}