using System;
using System.Collections.Generic;
using UnityEngine;

namespace _3DModule.Sensing
{
    public enum SensorKind { Vision, Hearing, Impact, Proximity, AllyInformation, Trail }
    public enum SensorEvent { Enter, Stay, Exit, Heard, Hit, Contact, Alert, Trail }
    public enum StimulusKind { Sound, Hit, Contact, AllyInformation, Trail }

    [Serializable]
    public struct SensorSettings
    {
        public SensorKind kind;
        [Min(0)] public float range;
        [Range(0, 360)] public float viewAngle;
        [Min(0.01f)] public float interval;
        [Min(0)] public float minimumStrength;
        public LayerMask targetLayers;
        public LayerMask obstacleLayers;
        public int categoryMask;
        [Min(1)] public int capacity;

        public void Validate()
        {
            if (kind < SensorKind.Vision || kind > SensorKind.Trail || !Finite(range) || range < 0 ||
                !Finite(viewAngle) || viewAngle < 0 || viewAngle > 360 || !Finite(interval) || interval <= 0 ||
                !Finite(minimumStrength) || minimumStrength < 0 || capacity < 1)
                throw new ArgumentException("Invalid sensor settings.");
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public struct SensorStimulus
    {
        public StimulusKind kind;
        public Transform source;
        public Vector3 position;
        public Vector3 direction;
        public float strength;
        public float time;
        public float lifetime;
        public int category;
    }

    public struct SensorResult
    {
        public SensorKind sensor;
        public SensorEvent eventType;
        public Transform source;
        public Vector3 position;
        public Vector3 direction;
        public float strength;
        public float time;
        public int category;
    }

    public readonly struct SensorContext
    {
        public readonly Transform owner;
        public readonly Transform eye;
        public readonly Transform ear;
        public readonly SensorEnvironment environment;
        public SensorContext(Transform owner, Transform eye, Transform ear, SensorEnvironment environment)
        {
            this.owner = owner;
            this.eye = eye != null ? eye : owner;
            this.ear = ear != null ? ear : owner;
            this.environment = environment;
        }
    }

    public interface ISensorStimulusReceiver
    {
        void Receive(in SensorStimulus stimulus);
    }

    public interface ISensorControl : ISensorStimulusReceiver
    {
        bool IsActive { get; }
        bool QueryOverflowed { get; }
        void Start(in SensorContext context, IReadOnlyList<SensorSettings> settings, ISensorResultReceiver receiver);
        void Tick(float deltaTime, float time);
        void Stop();
    }

    public interface ISensorResultReceiver
    {
        void Receive(in SensorResult result);
    }

    public interface IReadOnlySensorResults
    {
        int Count { get; }
        bool Overflowed { get; }
        bool TryGetResult(int index, out SensorResult result);
    }
}
