using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffInstance
    {
        /// <summary>
        /// 버프 스택
        /// </summary>
        public int Stack { get; }
        /// <summary>
        /// 버프 데이터
        /// </summary>
        public BuffData BuffData { get; }

        /// <summary>
        /// 인스턴스 존재 여부
        /// </summary>        
        public bool IsValid();

        /// <summary>
        /// 버프 타겟
        /// </summary>
        public IReadOnlyBattleComponent BuffTaget { get; }

        /// <summary>
        /// 버프 소스
        /// </summary>
        public IReadOnlyBattleComponent BuffSource { get; }

        /// <summary>
        /// 히트 요청
        /// </summary>
        THitResult ExecuteHit<THitResult>(HitInfo hitInfo) where THitResult : struct, IHitResult;
    }

}
