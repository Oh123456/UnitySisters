using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public readonly struct BuffModifierHandler
    {
        private readonly IBuffInstance buffInstance;         

        public bool IsValid()
        {
            return (buffInstance != null) && buffInstance.IsValid();
        }

        public void Release()
        {

        }
    } 
}
