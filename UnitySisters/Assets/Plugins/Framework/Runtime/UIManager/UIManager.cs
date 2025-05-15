using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityFramework.Singleton;

namespace UnityFramework.UI
{
    public partial class UIManager : LazySingleton<UIManager>
    {
        public class UIController 
        {
            MainUIBase mainUIBase;

            public System.Action Show;
            public System.Action Hide;
            public System.Action Close;

            public MainUIBase MainUIBase => this.mainUIBase;

            public void Initialize(MainUIBase uIBase)
            {
                this.mainUIBase = uIBase;
                this.mainUIBase.AddListener(this);
            }

            public void Release()
            {
                mainUIBase = null;
                Show = null;
                Hide = null;     
                Close = null;
            }

        }

        private Dictionary<System.Type, UIBase> uis = new Dictionary<System.Type, UIBase>();
        private Stack<UIController> showUIStack = new Stack<UIController>(4);
        private Stack<UIController> controllerPool = new Stack<UIController>(4);

        public UIManager()
        {
#if !CUSTOM_UI_RELEASE
            SceneManager.sceneUnloaded += UnloadUIs;
#endif
        }

        public T Show<T>(string name, int sortOrder = 0) where T : MainUIBase
        {
            T ui = GetCachedUI<T>(name);
            ui.SetSortOrder(sortOrder);

            UIController uIController = GetUIController();
            uIController.Initialize(ui);
            uIController.Show();
            showUIStack.Push(uIController);
            return ui;
        }

        public void Hide()
        {

            if (GetActiveUIController(out UIController uIController))
            {
                uIController.Hide();
                uIController.Release();
                controllerPool.Push(uIController);
            }
          
        }

        public void Close()
        {
            if (GetActiveUIController(out UIController uIController))
            {
                uIController.Hide();
                uIController.Close();
                uIController.Release();
                controllerPool.Push(uIController);
            }
        }

        private bool GetActiveUIController(out UIController uIController)
        {
            uIController = null;
            if (showUIStack.Count == 0)
                return false;
            uIController = showUIStack.Pop();
            return true;
        }

        public bool TryGetCachedUI<T>(out T ui) where T : MainUIBase
        {
            bool result = uis.TryGetValue(typeof(T), out var baseUI);
            if (baseUI == null)
                result = false;
            ui = result ? (T)baseUI : null;
            return result;
        }

        private T GetCachedUI<T>(string name) where T : MainUIBase
        {
            T ui = null;
            if (!TryGetCachedUI(out ui))
            {
                T prb = Resources.Load<T>(name);
                ui = GameObject.Instantiate<T>(prb);
                uis[typeof(T)]= ui;
            }

            return ui;
        }

        private UIController GetUIController()
        {
            if (controllerPool.Count == 0)
                return new UIController();

            return controllerPool.Pop();
        }

        public void ExecuteBackButton()
        {
            if (showUIStack.Count == 0)
                return;

            UIController uIController = showUIStack.Peek();
            if (uIController.MainUIBase.ExecuteButton())
            {
                showUIStack.Pop();
                uIController.Hide();
                uIController.Release();
                controllerPool.Push(uIController);
            }
        }

        public void UnloadUIs(Scene scene)
        {
            
            while (showUIStack.Count > 0)
            {
                UIController uIController = showUIStack.Pop();
                uIController.Release();
                controllerPool.Push(uIController);
            }
        }
    }
}
