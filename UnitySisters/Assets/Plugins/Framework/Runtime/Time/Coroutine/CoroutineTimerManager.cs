namespace UnityFramework.Timer
{
    using System.Collections;

    using Cysharp.Threading.Tasks;
    using Extensions;
    using UnityEngine;

    using UnityFramework.CoroutineUtility;

    public partial class TimerManager
    {
        private class CoroutineTimer
        {
            /// <summary>
            /// 코루틴 딜레이
            /// </summary>
            /// <param name="time">1.0f = 1초 </param>
            /// <param name="callback"> 콜백 </param>
            /// <param name="playerLoopTiming">Update, FixedUpdate, LastUpdate 만 지원 나머지 값은 Update </param>
            /// <returns></returns>
            public IEnumerator Delay(float time, System.Action callback, bool ignoreTimeScale = false, PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update)
            {

                // 대기 방식 결정
                YieldInstruction yieldInstruction = playerLoopTiming switch
                {
                    PlayerLoopTiming.FixedUpdate => CoroutineManager.Instance.WaitForFixedUpdate,
                    PlayerLoopTiming.Update => null,
                    PlayerLoopTiming.LastUpdate => CoroutineManager.Instance.WaitForEndOfFrame,
                    _ => null
                };

                //타임 스케일 무시여부에따라 값 변경
                System.Func<float> getTime = ignoreTimeScale ? GetUnscaledTime : GetTime;

                float currentTimeDeltaTime = 0.0f;
                while (time > currentTimeDeltaTime)
                {
                    float start = getTime();
                    yield return yieldInstruction;
                    // 코루틴간의 시간 측정
                    currentTimeDeltaTime += getTime() - start;
                }

                callback();
            }

            /// <summary>
            /// 타임 스케일 영향 받는 시간
            /// </summary>
            /// <returns>누적 시간 값</returns>
            private float GetTime()
            {
                return Time.time;
            }

            /// <summary>
            /// 타임 스케일 영향 안받는 시간
            /// </summary>
            /// <returns>누적 시간 값</returns>
            private float GetUnscaledTime()
            {
                return Time.unscaledTime;
            }

        }
        private CoroutineTimer coroutineTimer = new CoroutineTimer();



        /// <summary>
        /// 코루틴 기반 Timer 취소가 가능하다 
        /// </summary>
        /// <param name="monoBehaviour">코루틴 돌릴 Mono </param>
        /// <param name="timerHandle"> 핸들 </param>
        /// <param name="time">시간</param>
        /// <param name="callback">콜백</param>
        /// <param name="ignoreTimeScale">타임스케일 무시</param>
        /// <param name="delayTiming">타이밍</param>
        /// <returns>성공 여부</returns>
        public bool SetCoroutineTimer(MonoBehaviour monoBehaviour, float time, out TimerHandle timerHandle, System.Action callback, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {

            // 게임 오브젝트가 비활성화 상태면 코루틴이 작동안하기에 체크
            if (!monoBehaviour.gameObject.activeInHierarchy)
            {
                timerHandle = new TimerHandle();
                return false;
            }


            timerHandle = new TimerHandle(monoBehaviour, monoBehaviour.StartCoroutine(coroutineTimer.Delay(time, callback, ignoreTimeScale, delayTiming)));

            return true;
        }

        /// <summary>
        /// 코루틴 기반 Timer
        /// </summary>
        /// <param name="monoBehaviour">코루틴 돌릴 Mono</param>
        /// <param name="time">타임</param>
        /// <param name="callback">콜백</param>
        /// <param name="ignoreTimeScale"> 타임스케일 무시</param>
        /// <param name="delayTiming">딜레이 타임</param>
        /// <returns>성공 여부</returns>
        public bool SetCoroutineTimer(MonoBehaviour monoBehaviour, float time, System.Action callback, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            // 게임 오브젝트가 비활성화 상태면 코루틴이 작동안하기에 체크
            if (!monoBehaviour.gameObject.activeInHierarchy)
                return false;

            monoBehaviour.StartCoroutine(coroutineTimer.Delay(time, callback, ignoreTimeScale, delayTiming));
            return true;
        }
    }

}