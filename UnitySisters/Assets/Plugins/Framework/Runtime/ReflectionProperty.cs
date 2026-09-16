using System;
namespace UnityFramework
{

    [System.Serializable]
    public class ReflectionProperty<T> where T : IEquatable<T>
    {
        [UnityEngine.SerializeField]
        private T value;
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
        public void ClearListeners()
        {
            OnChanged = null;
        }

        /// <summary>
        /// 데이터 초기화 리플렉션 발생 x
        /// </summary>
        public void ClearData()
        {
            value = default(T);
        }        

        public static implicit operator T(ReflectionProperty<T> property)
        {
            return property.value;
        }

    }

}