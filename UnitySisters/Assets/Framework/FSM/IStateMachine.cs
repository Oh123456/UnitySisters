using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.FSM
{
    public interface IStateMachine
    {
        /// <summary>
        /// 상태 업데이트
        /// </summary>
        public void Update();
        /// <summary>
        /// 상태 변경
        /// </summary>
        /// <param name="id"></param>
        public void ChangeState(int id);
        /// <summary>
        /// 소유자 반환 
        /// </summary>
        /// <returns></returns>
        public object GetOwner();
        /// <summary>
        /// 소유자 반환
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetOwner<T>() where T : class;

        /// <summary>
        /// 스테이트 머신 리셋
        /// </summary>
        public void ResetState();
    } 
}
