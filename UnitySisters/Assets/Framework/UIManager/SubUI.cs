using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityFramework.UI
{
    interface ISubUI 
    {
        public void Show();
        public void Hide();
        public bool IsIndependent();
        UIBase FindParentUIBase();
    }

	[RequireComponent(typeof(Canvas))]
	public class SubUI : UIBase , ISubUI
    {
        enum ShowType
        {
            Auto,
            Custom,
        }

        [SerializeField] UIBase parentUI;
        [SerializeField][Tooltip("Auto ??Automatically shows when the Root is shown. \nCustom ??Remains hidden even when the Root is shown; the user must manually show it.")] ShowType showType = ShowType.Auto;

        protected override void Reset()
        {
            base.Reset();
            parentUI = FindParentUIBase();
        }

        protected override void Initialize()
        {
            base.Initialize();  
            if (showType == ShowType.Auto)
                parentUI.OnShow += base.Show;
            parentUI.OnHide += base.Hide;
        }

        public new void Show()
		{ 
			base.Show();
		}

        public new void Hide()
        {
            base.Hide();
        }

        public new void Close()
        { 
            base.Close();
        }

        public UIBase FindParentUIBase()
        {
            return UIUtils.FindParentIndependentUIBase(transform as RectTransform);
        }

        public bool IsIndependent()
        {
            return canvas.overrideSorting;  
        }
    } 
}
 