using System;


namespace AddressableEditor.Rule
{
    [System.Serializable]
    public abstract class AddressablePatternRule : IEquatable<AddressablePatternRule>
    {

        private string key;

        public AddressablePatternRule()
        {
            SetKey(out this.key);
        }

        protected abstract void SetKey(out string key);
        public abstract string GetValue();

        public override int GetHashCode()
        {
            return key.GetHashCode();
        }

        public bool Equals(AddressablePatternRule other)
        {
            return other.key == this.key;
        }

        public bool EqualsKey(string otherKey)
        {
            return this.key == otherKey;
        }
    }

}