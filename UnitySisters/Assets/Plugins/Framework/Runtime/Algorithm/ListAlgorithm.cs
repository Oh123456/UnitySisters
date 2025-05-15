using System.Collections.Generic;


namespace UnityFramework.Algorithm
{
    public static class ListAlgorithm
    {
        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(this List<T> list)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MaekAndHeapSort(list, list.Count, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(List<T> list, int size)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MaekAndHeapSort(list, size, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(this List<T> list, IComparer<T> comparer)
        {
            MaekAndHeapSort(list, list.Count, comparer);
        }

        /// <summary>
        /// 배열을 힙정렬함 O(n + n * long(n)) 의 시간 복잡도가 소요됨
        /// </summary>
        public static void MaekAndHeapSort<T>(List<T> list, int size, IComparer<T> comparer)
        {
            MakeHeap(list, comparer);
            // 최대 힙 기준 오름 차순으로 정렬
            // O(n*log(n)) 시간 복잡도
            for (int i = size - 1; i > 0; i--)
            {
                list.Swap(0, i);
                Heapify(list, i, size, comparer);
            }
        }

        /// <summary>
        /// 힙 트리를 힙 정렬함
        /// </summary>
        public static List<T> HeapSort<T>(this List<T> list, IComparer<T> comparer)
        {
            return HeapSort(list, list.Count, comparer);
        }

        /// <summary>
        /// 힙 트리를 힙 정렬함
        /// </summary>
        public static List<T> HeapSort<T>(List<T> list, int size, IComparer<T> comparer)
        {
            List<T> temp = new List<T>(list);

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
        public static void MakeHeap<T>(this List<T> list)
        {
            IComparer<T> comparer = Comparer<T>.Default;
            MakeHeap(list, comparer);

        }

        /// <summary>
        /// 배열을 힙트리로 변환
        /// </summary>        
        /// <param name="comparer">커스텀 비교</param>
        public static void MakeHeap<T>(this List<T> list, IComparer<T> comparer)
        {
            int count = list.Count;
            // 배열을 힙트리로 변환
            // i 가  (length / 2) - 1 이유는  마지막 노드의 부모 노드부터 뒤에서 앞으로 순회 O(n) 시간 복잡도
            for (int i = (count - 1 / 2); i >= 0; i--)
            {
                Heapify(list, i, count, comparer);
            }

        }

        /// <summary>
        /// 배열 스왑
        /// </summary>
        public static void Swap<T>(this List<T> list, int index, int index2)
        {
            T temp = list[index];
            list[index] = list[index2];
            list[index2] = temp;
        }

        /// <summary>
        /// 배열 덮어쓰기
        /// </summary>
        public static void Overwrite<T>(this List<T> list, int destinationIndex, int sourceIndex)
        {
            list[destinationIndex] = list[sourceIndex];
        }

        /// <summary>
        /// 힙 정렬
        /// </summary>
        public static void Heapify<T>(List<T> list, int rootIndex, int heapSize, IComparer<T> comparer)
        {
            int largest = ComparerHeap(list, rootIndex, heapSize, comparer);

            if (largest != rootIndex)
            {
                list.Swap(rootIndex, largest);
                Heapify(list, largest, heapSize, comparer);
            }
        }

        private static int ComparerHeap<T>(List<T> list, int rootIndex, int heapSize, IComparer<T> comparer)
        {
            int largest = rootIndex;
            int left = 2 * rootIndex + 1;
            int right = left + 1;

            // 두개를 검사하여 양수값이 나오면 큰것
            if (left < heapSize && comparer.Compare(list[left], list[largest]) > 0)
                largest = left;

            // 두개를 검사하여 양수값이 나오면 큰것
            if (right < heapSize && comparer.Compare(list[right], list[largest]) > 0)
                largest = right;

            return largest;
        }

        /// <summary>
        /// 마지막 노드 혹은 특정 노드에서 부터 루트까지 올라가면서 힙정렬
        /// </summary>
        /// <param name="lastIndex"> 마지막 인덱스 혹은 특정 인덱스</param
        public static void HeapifyUp<T>(this List<T> list, int lastIndex, IComparer<T> comparer)
        {
            int index = lastIndex;
            int heapSize = lastIndex + 1;
            while (index > 0)
            {
                // (i - 1) -> 부모 찾기
                // 힙 노드는 자식이  i * 2 + 1 과 i * 2 + 2 이다
                int parent = (index - 1) / 2;

                int largest = ComparerHeap(list, parent, heapSize, comparer);

                if (largest != parent)
                    list.Swap(parent, largest);
                else
                    break;

                index = parent;
            }

        }

        /// <summary>
        /// 루트에서부터 아래로 힙정렬
        /// </summary>
        public static void HeapifyDown<T>(this List<T> list, int lastIndex, IComparer<T> comparer)
        {
            Heapify(list, 0, lastIndex, comparer);
        }


        /// <summary>
        /// 정렬된 배열에서 target보다 큰 첫 번째 원소의 인덱스를 반환합니다.
        /// 배열은 반드시 오름차순이어야 하며, 값이 없을 경우 length를 반환합니다.
        /// </summary>
        /// <param name="list"> 배열</param>
        /// <param name="length"> 배열의 길이  </param>
        /// <param name="target"> 타겟 값</param>
        /// <returns></returns>
        public static int UpperBound<T>(this List<T> list, int length, T target, IComparer<T> comparer = null)
        {

            if (comparer == null)
                comparer = Comparer<T>.Default;

            int low = 0;
            int high = length;
            int mid = 0;

            while (low < high)
            {
                mid = (low + high) / 2;

                if (comparer.Compare(list[mid], target) <= 0)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }


        /// <summary>
        /// 정렬된 배열에서 target보다 큰 첫 번째 원소의 인덱스를 반환합니다.
        /// 배열은 반드시 오름차순이어야 하며, 값이 없을 경우 length를 반환합니다.
        /// </summary>
        /// <param name="list"> 배열</param>
        /// <param name="length"> 배열의 길이  </param>
        /// <param name="target"> 타겟 값</param>
        /// <returns></returns>
        public static int UpperBound<T>(this List<T> list, T target, IComparer<T> comparer = null)
        {
            return UpperBound(list, list.Count, target, comparer);
        }


        /// <summary>
        /// 정렬된 배열에서 target의 원소의 인덱스를 반환합니다.
        /// 배열은 반드시 오름차순이어야함
        /// </summary>
        /// <param name="list"> 배열</param>
        /// <param name="length"> 배열의 길이  </param>
        /// <param name="target"> 타겟 값</param>
        /// <returns></returns>
        public static int LowerBound<T>(this List<T> list, int length, T target, IComparer<T> comparer = null)
        {

            if (comparer == null)
                comparer = Comparer<T>.Default;

            int low = 0;
            int high = length;
            int mid = 0;

            while (low < high)
            {
                mid = (low + high) / 2;

                if (comparer.Compare(list[mid], target) < 0)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        /// <summary>
        /// 정렬된 배열에서 target의 원소의 인덱스를 반환합니다.
        /// 배열은 반드시 오름차순이어야함
        /// </summary>
        /// <param name="list"> 배열</param>
        /// <param name="target"> 타겟 값</param>
        /// <returns></returns>

        public static int LowerBound<T>(this List<T> list, T target, IComparer<T> comparer = null)
        {
            return LowerBound(list, list.Count, target, comparer);
        }
    }

}
