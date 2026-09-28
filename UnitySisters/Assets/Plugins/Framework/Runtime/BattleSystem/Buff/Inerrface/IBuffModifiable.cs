using UnityEngine;
using static Codice.Client.BaseCommands.Import.Commit;

namespace UnityFramework.BattleSystem
{

    public interface IBuffModifiable
    {
        void AddBuffModifier(IBuffModifier modifier, IBuffInstance buffInstance);
        void RemoveBuffModifier(IBuffModifier modifier, IBuffInstance buffInstance);

        void ApplyModifiers();
    }

}