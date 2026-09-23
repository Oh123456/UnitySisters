using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBuffInstance
    {
        public int Stack { get; }
        public bool IsValid();
    }

}
