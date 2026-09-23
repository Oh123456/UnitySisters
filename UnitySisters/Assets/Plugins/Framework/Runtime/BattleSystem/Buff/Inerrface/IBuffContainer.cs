using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffContainer
    {
        void AddBuff(BuffState buffState);
        void RemoveBuff(int buffId);
    }

}