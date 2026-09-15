using UnityEngine;
using UnityFramework.PoolObject;

namespace UnityFramework.BattleSystem
{
    public interface IAttackAble
    {
        public void Attack<TAttackData>(in TAttackData attackData) where TAttackData : struct;
    }

}