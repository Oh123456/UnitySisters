namespace UnityFramework.BattleSystem
{
    public interface IReadOnlyAttributeSet<T> : IReadOnlyReflectionProperty<T>
        where T : System.IEquatable<T>
    {
        T FinalValue { get; }
        event System.Action<T> OnChangedFinalValue;
    }
}
