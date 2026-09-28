using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public class BuffData
    {
        [SerializeField] private int buffID;
        [SerializeField] private BuffLifetimeType buffLifetimeType;
        [SerializeField] private int maxStack;
        [SerializeField] private bool removeAllOnExpire = false;
        [SerializeField] private bool resetDurationOnStack = true;
        [SerializeField] private float duration;

        public int BuffID => buffID;
        public int MaxStack => maxStack;
        public BuffLifetimeType BuffLifetimeType => buffLifetimeType;
        public bool RemoveAllOnExpire => removeAllOnExpire;
        public bool ResetDurationOnStack => resetDurationOnStack;
        public float Duration => duration;
    }

}