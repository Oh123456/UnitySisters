using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffContainer
    {
        void AddBuff(BuffState buffState);
        bool RemoveBuff(int buffId);
    }

}