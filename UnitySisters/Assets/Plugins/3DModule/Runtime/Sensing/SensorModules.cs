using System;
using System.Collections.Generic;
using UnityEngine;
using UnityFramework.PoolObject;

namespace _3DModule.Sensing
{
    internal abstract class SensorModule : IPoolObject
    {
        protected SensorContext context;
        protected SensorSettings settings;
        private ISensorResultReceiver receiver;
        protected bool active;
        private float elapsed;
        internal bool overflowed;

        internal void Bind(in SensorContext context, in SensorSettings settings, ISensorResultReceiver receiver)
        {
            this.context = context; this.settings = settings; this.receiver = receiver;
            elapsed = settings.interval; overflowed = false;
            Prepare();
        }

        protected virtual void Prepare() { }
        bool IPoolObject.IsValid() { return active; }
        void IPoolObject.Activate() { active = true; }
        void IPoolObject.Deactivate()
        {
            active = false; Reset(); context = default; settings = default; receiver = null;
            elapsed = 0; overflowed = false;
        }
        protected virtual void Reset() { }
        internal void Tick(float deltaTime, float time)
        {
            if (!active || context.owner == null) return;
            elapsed += deltaTime;
            if (elapsed < settings.interval) return;
            elapsed %= settings.interval;
            Scan(time);
        }
        protected virtual void Scan(float time) { }
        internal virtual void Receive(in SensorStimulus stimulus) { }
        protected bool Matches(in SensorStimulus stimulus, Transform origin)
        {
            return active && origin != null && stimulus.source != context.owner &&
                (settings.categoryMask & stimulus.category) != 0 &&
                stimulus.strength >= settings.minimumStrength &&
                (stimulus.position - origin.position).sqrMagnitude <= settings.range * settings.range;
        }
        protected void Emit(SensorEvent eventType, Transform source, Vector3 position, float time, float strength = 1, int category = 0, Vector3 direction = default)
        {
            if (!active) return;
            var result = new SensorResult { sensor = settings.kind, eventType = eventType, source = source,
                position = position, time = time, strength = strength, category = category, direction = direction };
            receiver.Receive(result);
        }
    }

    internal abstract class RangeSensor : SensorModule
    {
        private Collider[] colliders;
        private RaycastHit[] hits;
        private readonly HashSet<Transform> previous = new();
        private readonly HashSet<Transform> current = new();
        protected virtual bool UsesVision { get => false; }
        protected override void Prepare()
        {
            if (colliders == null || colliders.Length != settings.capacity) colliders = new Collider[settings.capacity];
            if (UsesVision && (hits == null || hits.Length != settings.capacity)) hits = new RaycastHit[settings.capacity];
            previous.EnsureCapacity(settings.capacity); current.EnsureCapacity(settings.capacity);
        }
        protected override void Reset()
        {
            previous.Clear(); current.Clear();
            if (colliders != null) Array.Clear(colliders, 0, colliders.Length);
            if (hits != null) Array.Clear(hits, 0, hits.Length);
        }
        protected override void Scan(float time)
        {
            Transform origin = UsesVision ? context.eye : context.owner;
            if (origin == null) return;
            overflowed = false; current.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin.position, settings.range, colliders, settings.targetLayers, QueryTriggerInteraction.Ignore);
            bool incomplete = count == colliders.Length;
            overflowed = incomplete;
            for (int i = 0; i < count && active; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !context.environment.TryResolve(collider, out Transform root, out Vector3 position) ||
                    root == context.owner || current.Contains(root)) continue;
                Vector3 delta = position - origin.position;
                if (delta.sqrMagnitude > settings.range * settings.range) continue;
                if (UsesVision && delta.sqrMagnitude > 0.000001f)
                {
                    float threshold = Mathf.Cos(settings.viewAngle * 0.5f * Mathf.Deg2Rad);
                    if (Vector3.Dot(origin.forward, delta.normalized) < threshold) continue;
                    int hitCount = Physics.RaycastNonAlloc(origin.position, delta.normalized, hits, delta.magnitude, settings.obstacleLayers, QueryTriggerInteraction.Ignore);
                    bool blocked = hitCount == hits.Length;
                    if (blocked) { incomplete = true; overflowed = true; }
                    for (int h = 0; h < hitCount && !blocked; h++)
                    {
                        Transform hit = hits[h].collider.transform;
                        if (!hit.IsChildOf(context.owner) && !hit.IsChildOf(root)) blocked = true;
                    }
                    Array.Clear(hits, 0, hitCount);
                    if (blocked) continue;
                }
                current.Add(root);
            }
            Array.Clear(colliders, 0, count);
            // A saturated query cannot prove that an existing target has left.
            if (!incomplete)
                foreach (Transform target in previous)
                {
                    if (current.Contains(target)) continue;
                    Emit(SensorEvent.Exit, target, target != null ? target.position : Vector3.zero, time);
                    if (!active) return;
                }
            foreach (Transform target in current)
            {
                bool existed = previous.Contains(target);
                Emit(existed ? SensorEvent.Stay : SensorEvent.Enter, target, target.position, time);
                if (!active) return;
            }
            if (!incomplete) previous.Clear();
            foreach (Transform target in current)
            {
                if (previous.Count < settings.capacity || previous.Contains(target)) previous.Add(target);
                else overflowed = true;
            }
        }
    }

    internal sealed class VisionSensor : RangeSensor
    {
        public VisionSensor() { }
        protected override bool UsesVision { get => true; }
    }

    internal sealed class ProximitySensor : RangeSensor { public ProximitySensor() { } }

    internal sealed class HearingSensor : SensorModule
    {
        public HearingSensor() { }
        internal override void Receive(in SensorStimulus stimulus)
        {
            if (stimulus.kind != StimulusKind.Sound || !Matches(stimulus, context.ear)) return;
            float distance = Vector3.Distance(context.ear.position, stimulus.position);
            float perceived = stimulus.strength * Mathf.Clamp01(1 - distance / Mathf.Max(settings.range, 0.0001f));
            if (perceived >= settings.minimumStrength)
                Emit(SensorEvent.Heard, stimulus.source, stimulus.position, stimulus.time, perceived, stimulus.category, stimulus.direction);
        }
    }

    internal sealed class ImpactSensor : SensorModule
    {
        public ImpactSensor() { }
        internal override void Receive(in SensorStimulus stimulus)
        {
            if ((stimulus.kind == StimulusKind.Hit || stimulus.kind == StimulusKind.Contact) && Matches(stimulus, context.owner))
                Emit(stimulus.kind == StimulusKind.Hit ? SensorEvent.Hit : SensorEvent.Contact, stimulus.source, stimulus.position,
                    stimulus.time, stimulus.strength, stimulus.category, stimulus.direction);
        }
    }

    internal sealed class AllyInformationSensor : SensorModule
    {
        public AllyInformationSensor() { }
        internal override void Receive(in SensorStimulus stimulus)
        {
            if (stimulus.kind == StimulusKind.AllyInformation && Matches(stimulus, context.owner))
                Emit(SensorEvent.Alert, stimulus.source, stimulus.position, stimulus.time, stimulus.strength, stimulus.category, stimulus.direction);
        }
    }

    internal sealed class TrailSensor : SensorModule
    {
        public TrailSensor() { }
        protected override void Scan(float time)
        {
            overflowed = context.environment.TrailOverflowed;
            for (int i = 0; i < context.environment.TrailCount && active; i++)
            {
                SensorStimulus trail = context.environment.GetTrail(i);
                float age = time - trail.time;
                if (age < 0 || trail.lifetime <= 0 || age >= trail.lifetime) continue;
                trail.strength *= 1 - age / trail.lifetime;
                if (Matches(trail, context.owner))
                    Emit(SensorEvent.Trail, trail.source, trail.position, time, trail.strength, trail.category, trail.direction);
            }
        }
    }
}
