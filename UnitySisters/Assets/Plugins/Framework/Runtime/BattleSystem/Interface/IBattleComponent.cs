using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public interface IBattleComponent : IAttackAble, IHitAble , IReadOnlyBattleComponent
    {
        T GetBattleAttributeSet<T>() where T : BattleAttributeSet;
        GameObject gameObject { get; }
    }

    public interface IReadOnlyBattleComponent
    {
        /// <summary>
        /// BattleAttributeSet 에서 값을 가져옴
        /// </summary>
        /// <param name="selector">가져올 AttributSet</param>
        public IReadOnlyAttributeSet<T> GetAttributeValue<T>(System.Func<BattleAttributeSet, IReadOnlyAttributeSet<T>> selector) where T : System.IEquatable<T>;
    }

}