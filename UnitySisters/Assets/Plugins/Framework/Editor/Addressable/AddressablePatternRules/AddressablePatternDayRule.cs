using System;

namespace AddressableEditor.Rule
{
    public class AddressablePatternDayRule : AddressablePatternRule
    {
        protected override void SetKey(out string key)
        {
            key = "yyyy_MM_dd_HH_mm_ss";
        }

        public override string GetValue()
        {
            return DateTime.UtcNow.ToString("yyyy_MM_dd_HH_mm_ss");
        }
    }

}