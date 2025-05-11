using System.Threading;

using Cysharp.Threading.Tasks;

using UnityFramework.Singleton;

using UnityEngine;


namespace UnityFramework.Timer
{

    using Extensions;



    public partial class TimerManager : LazySingleton<TimerManager>
    {
        /// <summary>
        /// 취소 가능한 Task 기반 타이머 콜백을 위한 데이터
        /// </summary>
        public struct TimerData
        {
            /// <summary>
            /// TaskHandle
            /// </summary>
            public TimerTaskHandle timerTaskHandle;
            /// <summary>
            /// 콜백 함수
            /// </summary>
            public System.Action continuationFunction;
            /// <summary>
            /// 캔슬시 호출 함수
            /// </summary>
            public System.Action cancelFunction;
        }

        

        private SimpleClassPool<TimerTaskHandle> simpleClassPool = new SimpleClassPool<TimerTaskHandle>();


        /// <summary>
        /// UniTask 기반 Timer 취소가 가능하다 비동기는 캔슬시 비용이 큼으로 최대한 캔슬 안하는 방향으로 가거나 빈조가 적을경우 사용
        /// </summary>
        /// <param name="time"> 1.0 == 1초  </param>
        /// <param name="ignoreTimeScale"> 타임 스케일 무시여부 </param>
        /// <param name="delayTiming"> 딜레이타이밍 </param>        
        /// <param name="cancelImmediately">캔슬후 바로 반환 될건지 </param>
        public void SetTimer(float time, out TimerHandle timerHandle, System.Action callback, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update, bool cancelImmediately = false)
        {
            //풀에서 TaskHandle을 가져옴
            TimerTaskHandle timerTaskHandle = simpleClassPool.Get();

            CancellationToken cancellationToken = timerTaskHandle.Token;

            //Task 생성
            UniTask uniTask = UniTask.Delay((int)(time * 1000.0f), ignoreTimeScale, delayTiming, cancellationToken, cancelImmediately);

            timerHandle = new TimerHandle(timerTaskHandle);
            // 비동기 타이머 작동
            _ = uniTask.ContinueWith(new TimerData()
            {
                timerTaskHandle = timerTaskHandle,
                continuationFunction = callback,
                cancelFunction = timerTaskHandle.Dispose,
            });
        }

        /// <summary>
        /// 취소 불가 딜레이
        /// </summary>
        /// <param name="time">1.0 == 1초 </param>
        /// <param name="callback"> 콜백 </param>
        /// <param name="ignoreTimeScale"> 타임 스케일 무시 여부 </param>
        /// <param name="delayTiming"> 딜레이 타이밍 </param>
        public void SetTimer(float time, System.Action callback, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = PlayerLoopTiming.Update)
        {
            UniTask uniTask = UniTask.Delay((int)(time * 1000.0f), ignoreTimeScale, delayTiming);
            // 비동기 타이머 작동
            uniTask.ContinueWith(callback);
        }

        /// <summary>
        /// TimerTaskHandle 반환
        /// </summary>
        /// <param name="timerTaskHandle"></param>
        public void ReturnTaskHandle(TimerTaskHandle timerTaskHandle)
        {
            // 이 핸들러가 Dispose 호출이됬는지 검사
            if (timerTaskHandle.IsDispose)
                return;
            Debug.Log("test");
            simpleClassPool.Set(timerTaskHandle);
        }

    }


    namespace Extensions
    {
        public static class TimerUniTaskExtensions
        {
            /// <summary>
            /// try-Catch 구조이기에 취소를 하지 않기를 권장 , 중간에 취소가 일어날경우 코루틴 사용할것..
            /// </summary>
            /// <returns></returns>
            public static async UniTask ContinueWith(this UniTask uniTask, TimerManager.TimerData timerData)
            {
                //캔슬하면 왜 무조건 예외 던지냐...
                try
                {
                    await uniTask;
                    timerData.continuationFunction();
                    TimerManager.Instance.ReturnTaskHandle(timerData.timerTaskHandle);
                }
                catch (System.OperationCanceledException)
                {
                    timerData.cancelFunction();
                }
            }
        }
    }

}