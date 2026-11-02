namespace UnityFramework.BattleSystem
{
    public abstract class AttributeValueConverter<T>
        where T : System.IEquatable<T>
    {
        public abstract float ToFloat(T value);
        public abstract T FromFloat(float value);
    }
}
