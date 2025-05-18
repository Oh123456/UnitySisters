using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreSystem
{

    [CreateAssetMenu(fileName = "ProjectSetting", menuName = "Scriptable Objects/ProjectSetting")]
    public class ProjectSetting : ScriptableObject  ,IInitialize
    {
        [SerializeField] InputActionAsset inputAsset;

        public void Initialize()
        {
            InputManager.Instance.LoadInputSystem(inputAsset);  
        }
    } 
}
