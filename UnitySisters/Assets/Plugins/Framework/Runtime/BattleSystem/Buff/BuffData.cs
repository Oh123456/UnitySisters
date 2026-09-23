using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public class BuffData
    {
        [SerializeField] private int buffID;
        [SerializeField] private BuffLifetimeType buffLifetimeType;
        [SerializeField] private int maxStack;
        public int BuffID => buffID;
        public int MaxStack => maxStack;
        public BuffLifetimeType BuffLifetimeType => buffLifetimeType;

    }

}