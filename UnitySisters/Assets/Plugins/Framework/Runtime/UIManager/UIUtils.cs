using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace UnityFramework.UI
{

    public struct UIUtils
    {
        public static UIBase FindParentUIBase(RectTransform rectTransform)
        {
            Transform currentParent = rectTransform.parent;
            UIBase uIBase = null;
            while (!currentParent.TryGetComponent<UIBase>(out uIBase))
            {
                currentParent = currentParent.parent;
                if (currentParent == null)
                    break;
            }

            return uIBase;
        }

        public static UIBase FindParentIndependentUIBase(RectTransform rectTransform)
        {
            Transform currentParent = rectTransform.parent;
            UIBase uIBase = null;


            Transform temp = null;
            while (true)
            {
                temp = currentParent;

                if (currentParent == null)
                {
                    if (temp == null)
                        break;
                    uIBase = temp.GetComponent<UIBase>();
                    break;
                }

                currentParent = currentParent.parent;

                if (!temp.TryGetComponent<UIBase>(out uIBase))
                    continue;

                break;
            }

            return uIBase;
        }
    }
}