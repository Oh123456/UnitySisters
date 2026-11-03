using UnityFramework.CommandTree;

namespace UnitySisters.Command
{
    [System.Serializable]
    public class AttackCommandBehavior : ICommandBehavior
    {
        public void Apply<T>(T value) where T : class
        {
            if (value is CharacterCommand command)
                command.isAttackButton = true;
        }
    }
}
