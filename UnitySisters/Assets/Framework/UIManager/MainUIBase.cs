using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.UI
{

    [RequireComponent(typeof(Canvas))]
    public class MainUIBase : UIBase
    {
        public void AddListener(UIManager.UIController uIController)
        {
            uIController.Show = Show;
            uIController.Hide = Hide;
            uIController.Close = Close;
        }

        /// <summary>
        /// 백버튼을 눌렀을때 다른 SubUI가 꺼져야하는 경우
        /// </summary>
        /// <returns>true MainUI가 Hide 가능할때</returns>
        public virtual bool ExecuteButton()
        {
            return true;
        }
    }

}