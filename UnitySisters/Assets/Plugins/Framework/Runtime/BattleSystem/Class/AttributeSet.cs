using System;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    [System.Serializable]
    public class AttributeSet<T> : ReflectionProperty<T>, IBuffModifiable where T : IEquatable<T>
    {
        [SerializeField] private T finalValue;
        public System.Action<T> OnChangedFinalValue;

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

        public AttributeSet() : base()
        {
            buffModifiers = new BuffModifierList<T>();
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

        void IBuffModifiable.AddBuffModifier(IBuffModifier modifier)
        {
            if (modifier is IBuffModifier<T> buffModifier)
                buffModifiers.Add(buffModifier);
            else
                throw new ArgumentException($"Modifier type mismatch. Expected {typeof(IBuffModifier<T>).Name}, but received {modifier.GetType().Name}.", nameof(modifier));
        }

        void IBuffModifiable.RemoveBuffModifier(IBuffModifier modifier)
        {
            if (modifier is IBuffModifier<T> buffModifier)
                buffModifiers.Remove(buffModifier);
            else
                throw new ArgumentException($"Modifier type mismatch. Expected {typeof(IBuffModifier<T>).Name}, but received {modifier.GetType().Name}.", nameof(modifier));
        }

        void IBuffModifiable.ApplyModifiers()
        {
            T cache = value;
            foreach (IBuffModifier<T> modifier in buffModifiers.Enumerate())
            {
                cache = modifier.Modifiy(cache);
            }

            FinalValue = cache;
        }
    }

}