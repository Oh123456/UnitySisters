namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public class AttributeSetFloat : AttributeSet<float>
    {
        public class Converter : AttributeValueConverter<float>
        {
            public override float ToFloat(float value)
            {
                return value;
            }

            public override float FromFloat(float value)
            {
                return value;
            }
        }

        public static Converter CONVERTER = new Converter();

        public AttributeSetFloat() : base(converter: CONVERTER)
        {

        }
    }
}
