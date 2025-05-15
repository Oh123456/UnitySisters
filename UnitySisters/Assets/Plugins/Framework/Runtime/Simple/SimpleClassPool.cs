using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityFramework.PoolObject;

namespace UnityFramework
{
    public class SimpleClassPool<T> where T : class, IPoolObject , new()
    {
        private Stack<T> classPool = new Stack<T>(4);

        /// <summary>
        /// 클래스 풀에서 가져오기
        /// </summary>
        /// <param name="isAutoActive">자동 활성화 </param>
        public T Get(bool isAutoActive = true)
        {
            T obj = classPool.Count == 0 ? new T() : classPool.Pop();
            if (isAutoActive)
                obj.Activate(); 
            return obj;
        }

        /// <summary>
        /// 클래스 풀에 봔환
        /// </summary>
        /// <param name="tClass">반환 클래스</param>
        /// <param name="isAutoDeactive"> 자동 비활성화</param>
        public void Set(IPoolObject tClass , bool isAutoDeactive = true)
        {
            tClass.Deactivate();
            classPool.Push((T)tClass);
        }

        /// <summary>
        /// 풀 제거
        /// </summary>
        public void Clear()
        {
            while (classPool.Count != 0)
                classPool.Pop();
        }
    }

}