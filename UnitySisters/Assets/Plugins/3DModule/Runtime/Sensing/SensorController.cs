using System;
using System.Collections.Generic;
using UnityFramework.Pool;
using UnityFramework.PoolObject;

namespace _3DModule.Sensing
{
    public sealed class SensorController : ISensorControl
    {
        private readonly List<SensorModule> modules = new(6);
        private SensorEnvironment environment;
        private bool active;
        private UnityEngine.Transform owner;
        bool ISensorControl.IsActive { get => active; }
        bool ISensorControl.QueryOverflowed
        {
            get { for (int i = 0; i < modules.Count; i++) if (modules[i].overflowed) return true; return false; }
        }

        void ISensorControl.Start(in SensorContext context, IReadOnlyList<SensorSettings> settings, ISensorResultReceiver receiver)
        {
            if (context.owner == null || context.environment == null || settings == null || receiver == null)
                throw new ArgumentException("Sensor context, settings and receiver are required.");
            int kinds = 0;
            for (int i = 0; i < settings.Count; i++)
            {
                settings[i].Validate();
                int flag = 1 << (int)settings[i].kind;
                if ((kinds & flag) != 0) throw new ArgumentException("Duplicate sensor kind.");
                kinds |= flag;
            }
            Stop();
            environment = context.environment; owner = context.owner;
            try
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    SensorSettings config = settings[i];
                    SensorModule module = Rent(config.kind);
                    modules.Add(module);
                    module.Bind(context, config, receiver);
                    ((IPoolObject)module).Activate();
                }
                active = true; environment.Register(this);
            }
            catch { Stop(); throw; }
        }

        void ISensorControl.Tick(float deltaTime, float time)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0 || float.IsNaN(time) || float.IsInfinity(time))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!active) return;
            if (owner == null || !owner.gameObject.activeInHierarchy) { Stop(); return; }
            for (int i = 0; i < modules.Count; i++) modules[i].Tick(deltaTime, time);
        }

        void ISensorControl.Stop() { Stop(); }
        internal void Stop()
        {
            active = false; environment?.Unregister(this);
            for (int i = modules.Count - 1; i >= 0; i--) PoolManager.SetClassObject(modules[i]);
            modules.Clear(); environment = null; owner = null;
        }

        void ISensorStimulusReceiver.Receive(in SensorStimulus stimulus) { Receive(stimulus); }
        internal void Receive(in SensorStimulus stimulus)
        {
            if (!active) return;
            if (!SensorEnvironment.Valid(stimulus)) throw new ArgumentException("Invalid sensor stimulus.");
            for (int i = 0; i < modules.Count; i++) modules[i].Receive(stimulus);
        }

        private static SensorModule Rent(SensorKind kind)
        {
            switch (kind)
            {
                case SensorKind.Vision: return PoolManager.GetClassObject<VisionSensor>(false);
                case SensorKind.Hearing: return PoolManager.GetClassObject<HearingSensor>(false);
                case SensorKind.Impact: return PoolManager.GetClassObject<ImpactSensor>(false);
                case SensorKind.Proximity: return PoolManager.GetClassObject<ProximitySensor>(false);
                case SensorKind.AllyInformation: return PoolManager.GetClassObject<AllyInformationSensor>(false);
                case SensorKind.Trail: return PoolManager.GetClassObject<TrailSensor>(false);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
