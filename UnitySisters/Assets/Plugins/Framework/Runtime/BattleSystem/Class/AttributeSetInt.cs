using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public class AttributeSetInt : AttributeSet<int>
    {
        public class Converter : AttributeValueConverter<int>
        {
            public override float ToFloat(int value)
            {
                return value;
            }

            public override int FromFloat(float value)
            {
                return Mathf.RoundToInt(value);
            }
        }

        public static Converter CONVERTER = new Converter();

        public AttributeSetInt() : base(converter: CONVERTER)
        {
            
        }
    }
}
