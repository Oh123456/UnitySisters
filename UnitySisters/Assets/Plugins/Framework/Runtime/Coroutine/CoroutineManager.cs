using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.Singleton;

namespace UnityFramework.CoroutineUtility
{

    public class CoroutineManager : LazySingleton<CoroutineManager>
    {
#if RUN_TIME_ALLOC_YIELD_OBJECTS
        WaitForEndOfFrame waitForEndOfFrame = null;
        WaitForFixedUpdate waitForFixedUpdate = null;
#else
        readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
#endif
        // 보통 이 매니저를 사용하는 주목적이기에 바로 생성한다
        Dictionary<float, WaitForSeconds> waitForSecondDictionary = new Dictionary<float, WaitForSeconds>();
        //이거는 거의 거의 거의 사용 안하기에 Lazy로 초동 메모리를 잡는다
        System.Lazy<Dictionary<float, WaitForSecondsRealtime>> waitForSecondsRealtimeDictionary = new System.Lazy<Dictionary<float, WaitForSecondsRealtime>>(() => new Dictionary<float, WaitForSecondsRealtime>());

        public WaitForEndOfFrame WaitForEndOfFrame
        {
            get
            {
                CreateWaitForEndOfFrame();
                return waitForEndOfFrame;
            }
        }

        public WaitForFixedUpdate WaitForFixedUpdate
        {
            get
            {
                CreateWaitForFixedUpdate();
                return waitForFixedUpdate;
            }
        }

        /// <summary>
        /// 저장된 WaitForSeconds 를 가져옵니다.
        /// </summary>        
        public void WaitForSecond(float time, out WaitForSeconds waitForSeconds)
        {
            if (!waitForSecondDictionary.TryGetValue(time, out waitForSeconds))
            {
                waitForSeconds = new WaitForSeconds(time);
                waitForSecondDictionary.Add(time, waitForSeconds);
            }
        }

        /// <summary>
        /// 저장된 WaitForSecondsRealtime 를 가져옵니다.
        /// </summary>
        public void WaitForSecondsRealtime(float time, out WaitForSecondsRealtime waitForSecondsRealtime)
        {
            if (!waitForSecondsRealtimeDictionary.Value.TryGetValue(time, out waitForSecondsRealtime))
            {
                waitForSecondsRealtime = new WaitForSecondsRealtime(time);
                waitForSecondsRealtimeDictionary.Value.Add(time, waitForSecondsRealtime);
            }
        }

        public void GetWaitForEndOfFrame(out WaitForEndOfFrame waitForEndOfFrame)
        {
            CreateWaitForEndOfFrame();
            waitForEndOfFrame = this.waitForEndOfFrame;
        }

        public void GetWaitForFixedUpdate(WaitForFixedUpdate waitForFixedUpdate)
        {
            CreateWaitForFixedUpdate();
            waitForFixedUpdate = this.waitForFixedUpdate;

        }

        [System.Diagnostics.Conditional("RUN_TIME_ALLOC_YIELD_OBJECTS")]
        private void CreateWaitForFixedUpdate()
        {
#if RUN_TIME_ALLOC_YIELD_OBJECTS
            if (waitForFixedUpdate == null)
                waitForFixedUpdate = new WaitForFixedUpdate();
#endif
        }

        [System.Diagnostics.Conditional("RUN_TIME_ALLOC_YIELD_OBJECTS")]
        private void CreateWaitForEndOfFrame()
        {
#if RUN_TIME_ALLOC_YIELD_OBJECTS
            if (waitForEndOfFrame == null)
                waitForEndOfFrame = new WaitForEndOfFrame();
#endif
        }

    }

}