using UnityEditor;

namespace AddressableEditor.Rule
{
    public class AddressablePatternProductNameRule : AddressablePatternRule
    {
        protected override void SetKey(out string key)
        {
            key = "productName";
        }

        public override string GetValue()
        {
            return PlayerSettings.productName;
        }
    }

}