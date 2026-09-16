using System;
using System.Collections.Generic;

using UnityEngine;

namespace UnityFramework.Utility
{
    public interface IDeepCopyGenerated
    {
        object DeepCopyGenerated(DeepCopyContext context);
    }

    public static class DeepCopyUtility
    {
        public static T DeepCopy<T>(this T source) where T : class
        {
            if (ReferenceEquals(source, null))
                return null;

            IDeepCopyGenerated generated = source as IDeepCopyGenerated;
            if (generated == null)
            {
                throw new InvalidOperationException(
                    $"Deep copy code has not been generated for '{source.GetType().FullName}'.");
            }

            return (T)generated.DeepCopyGenerated(new DeepCopyContext());
        }

        public static T CopyReference<T>(T source, DeepCopyContext context)
        {
            if (ReferenceEquals(source, null))
                return default;

            object instance = source;
            if (instance is string || instance is UnityEngine.Object || instance is ValueType)
                return source;

            if (context.TryGetCopy(instance, out object existingCopy))
                return (T)existingCopy;

            if (instance is IDeepCopyGenerated generated)
                return (T)generated.DeepCopyGenerated(context);

            if (instance is Delegate)
            {
                throw new InvalidOperationException(
                    $"Delegate '{instance.GetType().FullName}' cannot be deep copied.");
            }

            throw new InvalidOperationException(
                $"Reference type '{instance.GetType().FullName}' does not have generated deep copy code.");
        }

        public static T[] CopyReference<T>(T[] source, DeepCopyContext context)
        {
            if (source == null)
                return null;

            if (context.TryGetCopy(source, out object existingCopy))
                return (T[])existingCopy;

            T[] copy = new T[source.Length];
            context.RegisterCopy(source, copy);
            for (int i = 0; i < source.Length; i++)
                copy[i] = CopyReference(source[i], context);
            return copy;
        }

        public static List<T> CopyReference<T>(List<T> source, DeepCopyContext context)
        {
            if (source == null)
                return null;

            if (context.TryGetCopy(source, out object existingCopy))
                return (List<T>)existingCopy;

            List<T> copy = new List<T>(source.Count);
            context.RegisterCopy(source, copy);
            for (int i = 0; i < source.Count; i++)
                copy.Add(CopyReference(source[i], context));
            return copy;
        }

        public static Dictionary<TKey, TValue> CopyReference<TKey, TValue>(
            Dictionary<TKey, TValue> source,
            DeepCopyContext context)
        {
            if (source == null)
                return null;

            if (context.TryGetCopy(source, out object existingCopy))
                return (Dictionary<TKey, TValue>)existingCopy;

            Dictionary<TKey, TValue> copy =
                new Dictionary<TKey, TValue>(source.Count, source.Comparer);
            context.RegisterCopy(source, copy);
            foreach (KeyValuePair<TKey, TValue> pair in source)
            {
                copy.Add(
                    CopyReference(pair.Key, context),
                    CopyReference(pair.Value, context));
            }
            return copy;
        }

        public static HashSet<T> CopyReference<T>(HashSet<T> source, DeepCopyContext context)
        {
            if (source == null)
                return null;

            if (context.TryGetCopy(source, out object existingCopy))
                return (HashSet<T>)existingCopy;

            HashSet<T> copy = new HashSet<T>(source.Comparer);
            context.RegisterCopy(source, copy);
            foreach (T item in source)
                copy.Add(CopyReference(item, context));
            return copy;
        }
    }
}
