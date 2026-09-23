using UnityEngine;
using static Codice.Client.BaseCommands.Import.Commit;

namespace UnityFramework.BattleSystem
{

    public interface IBuffModifiable
    {
        void AddBuffModifier(IBuffModifier modifier);
        void RemoveBuffModifier(IBuffModifier modifier);

        void ApplyModifiers();
    }

}