using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityFramework.UI
{
    public class SafeArea : MonoBehaviour
    {
#if UNITY_EDITOR || UNITY_IOS
        RectTransform rectTransform;
        [SerializeField] bool ignoreBottom = false;

        private void Start()
        {
            this.rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }


        void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            if (ignoreBottom)
                anchorMin.y = 0.0f;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;



            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }

#endif
    }


#if UNITY_EDITOR
    public static class SafeAreaCreater
    {
        [UnityEditor.MenuItem("GameObject/UnityFramework/SafeArea", false, 1)]
        public static void Create()
        {
            if (Selection.activeGameObject == null)
                return;
            GameObject gameObject = new GameObject("IOSSafeArea", new System.Type[] { typeof(RectTransform) , typeof(SafeArea) });
            gameObject.transform.parent = Selection.activeGameObject.transform;
            gameObject.transform.localScale = Vector3.one;
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero; 

        }
    } 
#endif

}