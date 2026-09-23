using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    internal class BuffModifierEntry : PoolObject.IPoolObject
    {
        private IBuffModifiable modifiabl;
        private List<IBuffModifier> buffModifiers = new();

        public void SetBuffModifiable(IBuffModifiable buffModifiable)
        {
            modifiabl = buffModifiable;
        }

        public void AddBuffModifiers(IBuffModifier buffModifier)
        {
            buffModifiers.Add(buffModifier);
        }

        public void ApplyModifiers()
        {
            modifiabl.ApplyModifiers();
        }

        public void RemoveAllModifiers()
        {
            int count = buffModifiers.Count;
            for(int i = 0; i < count; i++)
            {
                modifiabl.RemoveBuffModifier(buffModifiers[i]);
            }
        }

        public void Activate()
        {

        }

        public void Deactivate()
        {
            modifiabl = null;
            buffModifiers.Clear();
        }

        public bool IsValid()
        {
            return modifiabl != null;
        }
    }

}
