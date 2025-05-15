using System;
using System.Buffers;
using System.Collections.Generic;

using Unity.Collections;

using UnityFramework.Algorithm;

namespace UnityFramework.Random
{
    public interface ISeed
    {
        uint Seed();
    }

    public static class WeightedRandom
    {
        private class DefaultSeed : ISeed
        {
            public uint Seed()
            {
                return (uint)System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        
        private static ISeed seedFunction = new DefaultSeed();

        public static void SetSeed(ISeed seed)
        {
            if (seed == null)
                return;
            seedFunction = seed;    
        }

        public static int Random(List<int> weightList)
        {
            int count = weightList.Count;
            int totalWeight = 0;
            for (int i = 0 ; i < count; i++)
            {
                totalWeight += weightList[i];
            }

            return Random(weightList, totalWeight);
        }

        public static int Random(List<int> weightList, int totalWeight)
        {
            int count = weightList.Count;
            int[] array = ArrayPool<int>.Shared.Rent(count);
            array[0] = weightList[0];
            for (int i = 1; i < count; i++)
            {
                array[i] = array[i - 1] + weightList[i];
            }

            uint seed = seedFunction.Seed();
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            int index = array.UpperBound(count, random.NextInt(0, totalWeight));
            ArrayPool<int>.Shared.Return(array, clearArray: true);
            return index;
        }

        public static int RandomJobSystem(NativeArray<int> weightList, NativeArray<int> sumArray, int totalWeight, uint seed)
        {
            int count = weightList.Length;
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            int index = UpperBoundJobSystem(sumArray, count, random.NextInt(0, totalWeight));
            return index;

        }

        private static int UpperBoundJobSystem(NativeArray<int> array, int count, int target)
        {
            int low = 0;
            int high = count;
            int mid = 0;

            while (low < high)
            {
                mid = (low + high) / 2;

                if (array[mid] <= target)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }
    } 
}
