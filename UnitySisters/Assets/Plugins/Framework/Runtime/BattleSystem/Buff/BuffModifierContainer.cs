using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public abstract class BuffModifierContainer
    {
        protected Dictionary<int, IBuffModifier> buffModifierDic = new Dictionary<int, IBuffModifier>();

        public abstract void Initialize();

        public bool TryGetBuffModifier(int id, out IBuffModifier buffModifier)
        {
            return buffModifierDic.TryGetValue(id, out buffModifier);
        }
    } 

}