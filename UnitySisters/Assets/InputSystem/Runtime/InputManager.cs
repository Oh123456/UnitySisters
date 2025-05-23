using UnityEngine;
using UnityEngine.InputSystem;
using UnityFramework.Singleton;

namespace CoreSystem.Input
{
    public class InputManager : LazySingleton<InputManager>
    {
        private IInputActionCollection2 actionCollection;
        public IInputActionCollection2 ActionCollection => actionCollection;
        public T GetActionCollection<T>() where T : IInputActionCollection2 => (T)actionCollection;

        public void LoadInputSystem(InputActionAsset inputAsset)
        {
            string className = $"{inputAsset.name}";

            System.Type type = System.Type.GetType(className);

            if (type == null)
            {
                Debug.LogError($"타입을 찾을 수 없습니다: {className}");
                return;
            }

            actionCollection = System.Activator.CreateInstance(type, inputAsset) as IInputActionCollection2;
            if (actionCollection == null)
            {
                Debug.LogError($"타입을 생성 할 수 없습니다: {className}");
                return;
            }

            actionCollection.Enable();
            LogUtility.Log($" 성공적으로 {type.Name} 인스턴스를 생성하고 Enable 했습니다.");
        }

    }
}
