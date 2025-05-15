using System.Collections;
using System.Collections.Generic;
using System.Threading;

using Cysharp.Threading.Tasks;

using UnityEngine;

using UnityFramework.PoolObject;

namespace UnityFramework.Timer
{

    public struct TimerHandle
    {
        private TimerTaskHandle timerTaskHandle;
        private Coroutine timerCoroutine;
        private MonoBehaviour targetMono;

        public TimerHandle(TimerTaskHandle timerTaskHandle)
        {
            this.timerTaskHandle = timerTaskHandle;
            this.timerCoroutine = null;
            this.targetMono = null;
        }

        public TimerHandle(MonoBehaviour targetMono, Coroutine timerCoroutine)
        {
            this.timerTaskHandle = null;
            this.timerCoroutine = timerCoroutine;
            this.targetMono = targetMono;
        }


        /// <summary>
        /// 타이며 캔슬
        /// </summary>
        public void Cancel()
        {
            if (timerTaskHandle != null)
            {
                CancelTask();
                return;
            }

            if (targetMono != null && timerCoroutine != null)
            {
                CancelCoroutineTimer();
                return;
            }

        }

        /// <summary>
        /// Task 기반 타이머 캔슬
        /// </summary>
        private void CancelTask()
        {
            timerTaskHandle.Cancel();
            timerTaskHandle = null;
        }

        /// <summary>
        /// Coroutime 기반 타이머 캔슬
        /// </summary>
        private void CancelCoroutineTimer()
        {
            targetMono.StopCoroutine(timerCoroutine);
            targetMono = null;
            timerCoroutine = null;
        }
    }


    public class TimerTaskHandle : IPoolObject, System.IDisposable
    {

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool isActive = false;
        private bool isDispose = false;
        public bool IsDispose => isDispose;
        public CancellationToken Token => cancellationTokenSource.Token;

        /// <summary>
        /// 활성화
        /// </summary>
        public void Activate()
        {
            isActive = true;
        }

        /// <summary>
        /// 비활성화
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
        }

        /// <summary>
        /// 존재 여부
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return !isDispose && isActive;
        }

        /// <summary>
        /// 타이며 캔슬
        /// </summary>
        public void Cancel()
        {
            cancellationTokenSource.Cancel();
            Dispose();
        }

        public void Dispose()
        {
            isDispose = true;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}
