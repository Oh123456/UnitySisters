using System;
using System.Collections.Generic;

namespace UnityFramework.Algorithm
{
    public static class ArrayAlgorithm
    {
        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(this T[] array)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MaekAndHeapSort(array, array.Length, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(T[] array, int size)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MaekAndHeapSort(array, size, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(this T[] array, IComparer<T> comparer)
        {
            MaekAndHeapSort(array, array.Length, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(T[] array, int size, IComparer<T> comparer)
        {
            MakeHeap(array, comparer);
            // 최대 힙 기준 오름 차순으로 정렬
            // O(n*log(n)) 시간 복잡도
            for (int i = size - 1; i > 0; i--)
            {
                array.Swap(0, i);
                Heapify(array, i, size, comparer);
            }
        }

        /// <summary>
        /// 힙 트리를 힙 정렬함
        /// </summary>
        public static T[] HeapSort<T>(this T[] array,IComparer<T> comparer)
        {
            return HeapSort(array, array.Length, comparer);
        }



        /// <summary>
        /// 힙 트리를 힙 정렬함
        /// </summary>
        public static T[] HeapSort<T>(T[] array, int size, IComparer<T> comparer)
        {
            T[] temp = new T[size];

            Array.Copy(array, temp, size);

            // 최대 힙 기준 오름 차순으로 정렬
            // O(n*log(n)) 시간 복잡도
            for (int i = size - 1; i > 0; i--)
            {
                temp.Swap(0, i);
                Heapify(temp, i, size, comparer);
            }

            return temp;
        }


        /// <summary>
        /// 배열을 힙트리로 변환
        /// </summary>
        public static void MakeHeap<T>(this T[] array)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MakeHeap(array,comparer);

        }

        /// <summary>
        /// 배열을 힙트리로 변환
        /// </summary>        
        /// <param name="comparer">커스텀 비교</param>
        public static void MakeHeap<T>(this T[] array, IComparer<T> comparer)
        {
            int length = array.Length;
            // 배열을 힙트리로 변환
            // i 가  (length / 2) - 1 이유는  마지막 노드의 부모 노드부터 뒤에서 앞으로 순회 O(n) 시간 복잡도
            for (int i = (length - 1 / 2) ; i >= 0; i--)
            {
                Heapify(array, i, length, comparer);
            }

        }

        /// <summary>
        /// 배열 스왑
        /// </summary>
        public static void Swap<T>(this T[] array, int index, int index2)
        {
            T temp = array[index];
            array[index] = array[index2];
            array[index2] = temp;
        }

        /// <summary>
        /// 배열 덮어쓰기
        /// </summary>
        public static void Overwrite<T>(this T[] array, int destinationIndex , int sourceIndex)
        {
            array[destinationIndex] = array[sourceIndex];
        }

        /// <summary>
        /// 힙 정렬
        /// </summary>
        public static void Heapify<T>(T[] array, int rootIndex, int heapSize ,IComparer<T> comparer)
        {
            int largest = ComparerHeap(array, rootIndex, heapSize, comparer);

            if (largest != rootIndex)
            {
                array.Swap(rootIndex, largest);
                Heapify(array, largest ,heapSize, comparer);
            }
        }

        private static int ComparerHeap<T>(T[] array, int rootIndex, int heapSize, IComparer<T> comparer)
        {
            int largest = rootIndex;
            int left = 2 * rootIndex + 1;
            int right = left + 1;

            // 두개를 검사하여 양수값이 나오면 큰것
            if (left < heapSize && comparer.Compare(array[left], array[largest]) > 0)
                largest = left;

            // 두개를 검사하여 양수값이 나오면 큰것
            if (right < heapSize && comparer.Compare(array[right], array[largest]) > 0)
                largest = right;

            return largest;
        }

        /// <summary>
        /// 마지막 노드 혹은 특정 노드에서 부터 루트까지 올라가면서 힙정렬
        /// </summary>
        /// <param name="lastIndex"> 마지막 인덱스 혹은 특정 인덱스</param
        public static void HeapifyUp<T>(this T[] array, int lastIndex ,IComparer<T> comparer)
        {
            int index = lastIndex;
            int heapSize = lastIndex + 1;
            while (index > 0)
            {
                // (i - 1) -> 부모 찾기
                // 힙 노드는 자식이  i * 2 + 1 과 i * 2 + 2 이다
                int parent = (index - 1) / 2;

                int largest = ComparerHeap(array, parent, heapSize, comparer);

                if (largest != parent)
                    array.Swap(parent, largest);
                else
                    break;

                index = parent;
            }

        }

        /// <summary>
        /// 루트에서부터 아래로 힙정렬
        /// </summary>
        public static void HeapifyDown<T>(this T[] array, int lastIndex,IComparer<T> comparer)
        {
            Heapify(array, 0, lastIndex, comparer);
        }
    }
}
