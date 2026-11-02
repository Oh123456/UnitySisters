using UnityEngine;

namespace UnityFramework.CommandTree
{
    public abstract class CommandData : ScriptableObject
    {
        public abstract int Count { get; }
        public abstract void CompleteSetup();
    }

    public abstract class CommandData<TKey> : CommandData
    {
        [SerializeField] private CommandTree<TKey> commandTree = new CommandTree<TKey>();

        public CommandTree<TKey> CommandTree
        {
            get
            {
                if (commandTree == null)
                    commandTree = new CommandTree<TKey>();
                if (!commandTree.IsSetupComplete)
                    commandTree.CompleteSetup();

                return commandTree;
            }
        }

        public override int Count => CommandTree.Count;

        public override void CompleteSetup()
        {
            if (commandTree == null)
                commandTree = new CommandTree<TKey>();

            commandTree.CompleteSetup();
        }
    }
}
