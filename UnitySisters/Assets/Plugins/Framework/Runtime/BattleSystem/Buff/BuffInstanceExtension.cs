namespace UnityFramework.BattleSystem
{
    public static class BuffInstanceExtension
    {
        public static void SetBuffBuffModifier<TBattleAttributeSet>(this IBuffInstance buffInstance, System.Func<TBattleAttributeSet, IBuffModifiable> getAttributeSet, IBuffModifier buffModifier)
            where TBattleAttributeSet : BattleAttributeSet
        {
            if (buffInstance is BuffInstance instance)
                instance.SetBuffBuffModifier(getAttributeSet, buffModifier);
        }
    }
}
