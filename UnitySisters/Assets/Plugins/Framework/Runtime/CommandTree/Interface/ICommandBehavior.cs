namespace UnityFramework.CommandTree
{
    public interface ICommandBehavior
    {
        void Apply<T>(T value) where T : class;
    }
}
