using System;
namespace UnityFramework
{

    [System.Serializable]
    public class ReflectionProperty<T> : IReadOnlyReflectionProperty<T>
        where T : IEquatable<T> 
    {
        [UnityEngine.SerializeField]
        protected T value;
        public event Action<T> OnChanged;

        public T Value
        {
            get { return value; }
            set
            {
                if (this.value.Equals(value))
                    return;
                this.value = value;
                OnChanged?.Invoke(value);
            }
        }


        /// <summary>
        /// 모든 이벤트 구독 해지
        /// </summary>
        public virtual void ClearListeners()
        {
            OnChanged = null;
        }

        /// <summary>
        /// 데이터 초기화
        /// </summary>
        /// <param name="isReflection">리플렉션 여부</param>
        public virtual void ClearData(bool isReflection = false)
        {
            value = default(T);
            if (isReflection)
                OnChanged?.Invoke(value);
        }        

        public static implicit operator T(ReflectionProperty<T> property)
        {
            return property.value;
        }

    }

}