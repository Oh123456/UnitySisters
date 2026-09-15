using UnityEngine;
using UnityFramework.FSM;

namespace UnitySisters.FSM
{
    public interface IFSMController
    {
        public void ChangeState(int id);
        public int? GetCurrentStateID();

        /// <summary>
        /// 런타임중의 기본 상태 id로 되돌림
        /// </summary>
        public void SetDefaultState();

    }
}
