using System;
using System.Collections.Generic;
using UnityEngine;

namespace _3DModule.Sensing
{
    public sealed class SensorEnvironment : ISensorStimulusReceiver
    {
        private struct Subject
        {
            internal Transform root;
            internal Transform aim;
        }

        private readonly Dictionary<Transform, Subject> subjects = new();
        private readonly List<SensorController> listeners = new();
        private readonly List<SensorStimulus> trails;
        private readonly int trailCapacity;
        private bool trailOverflowed;
        private bool publishing;
        public bool TrailOverflowed { get => trailOverflowed; }
        internal int TrailCount { get => trails.Count; }

        public SensorEnvironment(int trailCapacity = 128)
        {
            if (trailCapacity < 1) throw new ArgumentOutOfRangeException(nameof(trailCapacity));
            this.trailCapacity = trailCapacity;
            trails = new List<SensorStimulus>(trailCapacity);
        }

        public void RegisterSubject(Transform root, Transform aim = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            subjects[root] = new Subject { root = root, aim = aim != null ? aim : root };
        }

        public void UnregisterSubject(Transform root) { if (root != null) subjects.Remove(root); }

        internal bool TryResolve(Collider collider, out Transform root, out Vector3 position)
        {
            for (Transform current = collider.transform; current != null; current = current.parent)
                if (subjects.TryGetValue(current, out Subject subject) && subject.root != null && subject.aim != null)
                {
                    root = subject.root;
                    position = subject.aim.position;
                    return true;
                }
            root = null;
            position = default;
            return false;
        }

        internal void Register(SensorController controller) { if (!listeners.Contains(controller)) listeners.Add(controller); }
        internal void Unregister(SensorController controller)
        {
            if (publishing)
            {
                int index = listeners.IndexOf(controller);
                if (index >= 0) listeners[index] = null;
            }
            else listeners.Remove(controller);
        }

        void ISensorStimulusReceiver.Receive(in SensorStimulus stimulus)
        {
            if (publishing) throw new InvalidOperationException("Sensor signal dispatch cannot be reentered.");
            if (stimulus.kind != StimulusKind.Sound && stimulus.kind != StimulusKind.Trail)
                throw new ArgumentException("Hit, contact and ally information must be addressed to the intended receiver.");
            if (!Valid(stimulus)) throw new ArgumentException("Invalid sensor stimulus.");
            if (stimulus.kind == StimulusKind.Trail)
            {
                Prune(stimulus.time);
                if (trails.Count == trailCapacity) { trails.RemoveAt(0); trailOverflowed = true; }
                trails.Add(stimulus);
                return;
            }
            publishing = true;
            int count = listeners.Count;
            try
            {
                for (int i = 0; i < count; i++) listeners[i]?.Receive(stimulus);
            }
            finally
            {
                publishing = false;
                for (int i = listeners.Count - 1; i >= 0; i--) if (listeners[i] == null) listeners.RemoveAt(i);
            }
        }

        internal static bool Valid(in SensorStimulus value)
        {
            return value.kind >= StimulusKind.Sound && value.kind <= StimulusKind.Trail &&
                Finite(value.position.x) && Finite(value.position.y) && Finite(value.position.z) &&
                Finite(value.direction.x) && Finite(value.direction.y) && Finite(value.direction.z) &&
                Finite(value.strength) && value.strength >= 0 && Finite(value.time) &&
                Finite(value.lifetime) && value.lifetime >= 0;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal SensorStimulus GetTrail(int index) { return trails[index]; }

        public void Prune(float time)
        {
            for (int i = trails.Count - 1; i >= 0; i--)
                if (time - trails[i].time >= trails[i].lifetime) trails.RemoveAt(i);
        }

        public void Clear()
        {
            if (publishing) throw new InvalidOperationException("Cannot clear during signal dispatch.");
            while (listeners.Count > 0) listeners[listeners.Count - 1].Stop();
            trails.Clear(); subjects.Clear(); trailOverflowed = false;
        }
    }
}
