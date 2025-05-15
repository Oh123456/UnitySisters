using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace UnityFramework.UI
{
    [RequireComponent(typeof(Canvas))]
    public abstract class UIBase : MonoBehaviour
    {
        [SerializeField] protected Canvas canvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        protected bool isShow = false;

        public event System.Action OnShow;
        public event System.Action OnHide;
        public event System.Action OnClose;

        public bool IsClosed => !gameObject.activeSelf;

        protected virtual void Reset()
        {
            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
        }

        private void Start()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            isShow = canvas.enabled;
        }

        protected virtual void Show()
        {
            if (isShow)
                return;

            canvas.enabled = true;
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = true;
            isShow = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            OnShow?.Invoke();   
        }

        protected virtual void Hide()
        {
            if (!isShow)
                return;

            canvas.enabled = false;
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = false;
            isShow = false;
            OnHide?.Invoke();
        }

        protected virtual void Close()
        {
            if (!isShow)
                return;
            gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        public void SetSortOrder(int oreder)
        {
            canvas.sortingOrder = oreder;
        }


        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public virtual void EditorActiveToggle()
        {
            canvas.enabled = !canvas.enabled;
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = !graphicRaycaster.enabled;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public virtual void EditorShow()
        {
            canvas.enabled = true;
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public virtual void EditorHide()
        {
            canvas.enabled = false;
            if (graphicRaycaster != null)
                graphicRaycaster.enabled = false;
        }
    }

}