using CoreSystem.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreSystem
{

    [CreateAssetMenu(fileName = "ProjectSetting", menuName = "Scriptable Objects/ProjectSetting")]
    public class ProjectSetting : ScriptableObject, IInitialize
    {
        [SerializeField] InputActionAsset inputAsset;
        [SerializeField] InputDefaultData inputDefaultData = new InputDefaultData()
        {
            move = "Move",
            lockAt = "LockAt",
        };


        public void Initialize()
        {
            InputManager.Instance.LoadInputSystem(inputAsset, in inputDefaultData);  
        }
    } 
}
