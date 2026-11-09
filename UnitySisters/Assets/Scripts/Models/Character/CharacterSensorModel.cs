using _3DModule.Sensing;
using UnitySisters.Table;
using UnityEngine;

namespace UnitySisters.Model
{
    public sealed class CharacterSensorModel : ISensorResultReceiver, IReadOnlySensorResults
    {
        private readonly ISensorControl control = new SensorController();
        private SensorResult[] results = System.Array.Empty<SensorResult>();
        private int count;
        private int head;
        private bool overflowed;
        private object leaseOwner;
        private Transform subject;
        private SensorEnvironment environment;
        public ISensorControl Control { get => control; }
        public IReadOnlySensorResults Results { get => this; }
        public bool IsActive { get => control.IsActive; }
        internal void TrackSubject(SensorEnvironment value, Transform root)
        {
            if (environment != null && environment != value) { Release(); environment.UnregisterSubject(subject); }
            environment = value; subject = root;
        }
        int IReadOnlySensorResults.Count { get => count; }
        bool IReadOnlySensorResults.Overflowed { get => overflowed || control.QueryOverflowed; }

        public bool Connect(object owner, in SensorContext context, CharacterAIData data)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (leaseOwner != null && !ReferenceEquals(leaseOwner, owner)) return false;
            if (control.IsActive) return true;
            data.Validate();
            if (results.Length != data.ResultCapacity) results = new SensorResult[data.ResultCapacity];
            ClearResults();
            control.Start(context, data.Sensors, this);
            leaseOwner = owner; subject = context.owner; environment = context.environment;
            return true;
        }

        public void Disconnect(object owner)
        {
            if (!ReferenceEquals(leaseOwner, owner)) return;
            Release();
        }

        public void Release()
        {
            control.Stop(); ClearResults(); leaseOwner = null;
        }

        public void RemoveSubject()
        {
            Release(); environment?.UnregisterSubject(subject); environment = null; subject = null;
        }

        public void Tick(float deltaTime, float time) { control.Tick(deltaTime, time); }
        public void ClearResults()
        {
            System.Array.Clear(results, 0, results.Length); count = 0; head = 0; overflowed = false;
        }
        void ISensorResultReceiver.Receive(in SensorResult result)
        {
            if (results.Length == 0) return;
            int index = (head + count) % results.Length;
            if (count == results.Length) { index = head; head = (head + 1) % results.Length; overflowed = true; }
            else count++;
            results[index] = result;
        }
        bool IReadOnlySensorResults.TryGetResult(int index, out SensorResult result)
        {
            if (index < 0 || index >= count) { result = default; return false; }
            result = results[(head + index) % results.Length]; return true;
        }
    }
}
