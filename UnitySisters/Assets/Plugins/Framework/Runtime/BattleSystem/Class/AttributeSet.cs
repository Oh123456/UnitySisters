using System;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public abstract class AttributeValueConverter<T> 
        where T : struct, IEquatable<T>
    {
        public abstract float ToFloat(T value);
        public abstract T FromFloat(float value);
    }

    [System.Serializable]
    public class AttributeSet<T> : ReflectionProperty<T>, IBuffModifiable 
        where T : struct, IEquatable<T> 
    {


        [SerializeField] private T finalValue;
        public System.Action<T> OnChangedFinalValue;
        private AttributeValueConverter<T> converter;
        protected IBuffModifierCollection<T> buffModifiers;

        public T FinalValue
        {
            get { return finalValue; }
            set
            {
                if (value.Equals(finalValue))
                    return;
                finalValue = value;
                OnChangedFinalValue?.Invoke(finalValue);
            }
        }

        public AttributeSet(AttributeValueConverter<T> converter =  null) : base()
        {
            buffModifiers = new BuffModifierList<T>();
            this.converter = converter;
        }

        ~AttributeSet()
        {
            buffModifiers.Clear();
            buffModifiers = null;
        }

        public override void ClearListeners()
        {
            base.ClearListeners();
            OnChangedFinalValue = null;
        }

        public override void ClearData(bool isReflection = false)
        {
            base.ClearData(isReflection);
            finalValue = value;
            if (isReflection)
                OnChangedFinalValue?.Invoke(finalValue);
        }

        void IBuffModifiable.AddBuffModifier(IBuffModifier modifier, IBuffInstance buffInstance)
        {
            if (modifier is IBuffModifier<T> buffModifier)
                buffModifiers.Add(new BuffModifierBinding<T>(buffModifier, buffInstance));
            else
                throw new ArgumentException($"Modifier type mismatch. Expected {typeof(IBuffModifier<T>).Name}, but received {modifier.GetType().Name}.", nameof(modifier));
        }

        void IBuffModifiable.RemoveBuffModifier(IBuffModifier modifier, IBuffInstance buffInstance)
        {
            if (modifier is IBuffModifier<T> buffModifier)
                buffModifiers.Remove(new BuffModifierBinding<T>(buffModifier, buffInstance));
            else
                throw new ArgumentException($"Modifier type mismatch. Expected {typeof(IBuffModifier<T>).Name}, but received {modifier.GetType().Name}.", nameof(modifier));
        }

        void IBuffModifiable.ApplyModifiers()
        {
            BuffModifierContext buffModifierContext = BuffModifierSystem.BuffModifierContainer.GetBuffModifierContext();

            try
            {
                T cache = value;
                foreach (BuffModifierBinding<T> modifier in buffModifiers.Enumerate())
                {
                    cache = modifier.buffModifier.Modify(value, cache, buffModifierContext, modifier.buffInstance);
                }

                if (converter == null)
                {
                    FinalValue = value;
                    return;
                }

                float calcuateValue = buffModifierContext.Calculate(converter.ToFloat(value));
                FinalValue = converter.FromFloat(calcuateValue);
            }
            finally
            {
                BuffModifierSystem.BuffModifierContainer.SetBuffModifierContext(buffModifierContext);
            }
        }


    }

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