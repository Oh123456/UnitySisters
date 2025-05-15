namespace UnityFramework.PoolObject
{
    public interface IPoolObject
    {
        /// <summary>
        /// 객체가 활성화 되있는지 혹은 존재하는지
        /// </summary>
        /// <returns></returns>
        public bool IsValid();
        /// <summary>
        /// 객체 활성화
        /// </summary>
        public void Activate();
        /// <summary>
        /// 객체 비활성화
        /// </summary>
        public void Deactivate();
    }

    public interface IMonoPoolObject : IPoolObject
    {
        public int KeyCode { get; set; }
    }
}