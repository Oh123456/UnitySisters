using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffContainer
    {
        void AddBuff(BuffState buffState, IBattleComponent buffSource);
        bool RemoveBuff(int buffId);
    }

}