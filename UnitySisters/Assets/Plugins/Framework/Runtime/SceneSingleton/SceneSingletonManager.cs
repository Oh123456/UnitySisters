using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityFramework.Singleton
{
    internal sealed class SceneSingletonManager : LazySingleton<SceneSingletonManager>
    {
        private readonly struct Entry
        {
            internal readonly MonoBehaviour instance;
            internal readonly int sceneHandle;
            internal Entry(MonoBehaviour instance)
            {
                this.instance = instance;
                sceneHandle = instance.gameObject.scene.handle;
            }
        }

        private readonly Dictionary<Type, Entry> entries = new();
        private readonly List<Type> removalKeys = new();

        public SceneSingletonManager() { Subscribe(); }

        internal bool TryRegister<T>(SceneSingleton<T> instance) where T : SceneSingleton<T>
        {
            if (instance == null) return false;
            if (instance is not T) throw new ArgumentException("SceneSingleton must use its own type as T.", nameof(instance));
            Type key = typeof(T);
            if (entries.TryGetValue(key, out Entry existing) && existing.instance != null)
                return ReferenceEquals(existing.instance, instance);
            entries[key] = new Entry(instance);
            return true;
        }

        internal T Get<T>() where T : SceneSingleton<T>
        {
            Type key = typeof(T);
            if (!entries.TryGetValue(key, out Entry entry)) return null;
            if (entry.instance != null) return entry.instance as T;
            entries.Remove(key);
            return null;
        }

        internal void Unregister<T>(SceneSingleton<T> instance) where T : SceneSingleton<T>
        {
            Type key = typeof(T);
            if (entries.TryGetValue(key, out Entry entry) && ReferenceEquals(entry.instance, instance))
                entries.Remove(key);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            foreach (var pair in entries)
                if (pair.Value.sceneHandle == scene.handle) removalKeys.Add(pair.Key);
            for (int i = 0; i < removalKeys.Count; i++) entries.Remove(removalKeys[i]);
            removalKeys.Clear();
        }

        private void Subscribe()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private void OnQuitting()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= OnQuitting;
            entries.Clear();
            removalKeys.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Instance.entries.Clear();
            Instance.removalKeys.Clear();
            Instance.Subscribe();
        }
    }
}
