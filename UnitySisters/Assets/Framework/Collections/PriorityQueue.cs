using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityFramework.Algorithm;

namespace UnityFramework.Collections
{
    public class PriorityQueue<T> : IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>, ICollection
    {
        private IComparer<T> comparer;
        private T[] array;
        private int capacity = 4;
        private int lastIndex = -1;
        private readonly object syncRoot = new object();

        public int Capacity => capacity;

        public int Count => lastIndex + 1;

        public bool IsSynchronized => false;

        public object SyncRoot => syncRoot;

        public PriorityQueue()
        {
            array = new T[capacity];
            comparer = Comparer<T>.Default;
        }

        public PriorityQueue(int capacity)
        {
            this.capacity = capacity;
            array = new T[capacity];
            comparer = Comparer<T>.Default;
        }

        public PriorityQueue(IComparer<T> comparer = null, int capacity = 4)
        {
            this.capacity = capacity;
            this.comparer = comparer;
            array = new T[capacity];
        }

        public PriorityQueue(T[] array, IComparer<T> comparer = null)
        {
            if (comparer == null)
                this.comparer = Comparer<T>.Default;
            else 
                this.comparer = comparer;

            int count = array.Length;
            this.array = new T[count];
            capacity = count;
            Array.Copy(array,this.array, count);
            array.MakeHeap(comparer);        
        }


        public void Enqueue(T element)
        {
            if (++lastIndex >= capacity)
                ExpandArray();

            array[lastIndex] = element;
            array.HeapifyUp(lastIndex, comparer);
        }

        public T Dequeue()
        {
            if (lastIndex < 0)
                throw new InvalidOperationException("Queue is empty.");
            // 루트 뽑음
            T element = array[0];

            // 마지막 인덱스를 루트로 올린후 현재의 마지막 인덱스를 줄임(카운트)
            if (lastIndex > 0)
            {
                array.Overwrite(0, lastIndex);
                array.HeapifyDown(lastIndex, comparer);
            }
            lastIndex--;
            //요소 반환
            return element;

        }

        public bool Empty()
        {
            return lastIndex < 0;
        }


        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        Enumerator GetEnumerator()
        {
            return new Enumerator(array , lastIndex);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            Array.Copy(this.array, 0, array, index, this.array.Length);
        }

        private void ExpandArray()
        {
            int currentCapacity = capacity;
            capacity = capacity * 2;
            T[] newArray = new T[capacity];
            Array.Copy(array, newArray, currentCapacity);
            array = newArray;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void CheckIntegrity()
        {
            for (int i = 1; i < lastIndex; i++)
            {
                if (comparer.Compare(array[i], array[0]) > 0)
                {
                    Debug.LogError("Error!!");
                    break;
                }
            }
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly T[] array; 
            private int index;
            private int lastIndex;
            public T Current { get => array[index]; }

            object IEnumerator.Current => throw new NotImplementedException();

            public Enumerator(T[] array, int lastIndex)
            {
                this.array = array;
                this.lastIndex = lastIndex;
                index = -1;
            }

            public void Dispose()
            {
                index = -1; 
            }

            public bool MoveNext()
            {
                index++;
                return index <= lastIndex;
            }

            public void Reset()
            {
                index = -1;
            }
        }
    }
}
