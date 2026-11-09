using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using _3DModule.Sensing;

namespace _3DModule.Sensing.Tests
{
    internal sealed class SensorTests
    {
        private sealed class Receiver : ISensorResultReceiver
        {
            internal readonly List<SensorResult> results = new(128);
            void ISensorResultReceiver.Receive(in SensorResult result) { results.Add(result); }
        }
        private readonly List<GameObject> objects = new();
        private readonly List<ISensorControl> controls = new();
        private SensorEnvironment environment;
        private Transform owner;
        private Transform target;
        private Receiver receiver;
        private const int TestLayer = 30;

        [SetUp]
        public void SetUp()
        {
            environment = new SensorEnvironment(2);
            owner = Create("Sensor owner", new Vector3(1000, 1000, 1000));
            target = Create("Sensor target", owner.position + Vector3.forward * 3);
            target.gameObject.AddComponent<BoxCollider>();
            environment.RegisterSubject(owner); environment.RegisterSubject(target);
            receiver = new Receiver(); Physics.SyncTransforms();
        }
        private Transform Create(string name, Vector3 position)
        {
            var go = new GameObject(name); objects.Add(go); go.layer = TestLayer; go.transform.position = position; return go.transform;
        }
        [TearDown]
        public void TearDown()
        {
            foreach (var control in controls) control.Stop();
            environment.Clear();
            foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go);
            controls.Clear(); objects.Clear();
        }
        private SensorSettings Config(SensorKind kind)
        {
            return new SensorSettings { kind = kind, range = 10, viewAngle = 90, interval = 0.1f,
                capacity = 16, categoryMask = -1, targetLayers = 1 << TestLayer, obstacleLayers = 1 << TestLayer };
        }
        private ISensorControl Start(params SensorSettings[] settings)
        {
            ISensorControl control = new SensorController(); controls.Add(control);
            var context = new SensorContext(owner, owner, owner, environment);
            control.Start(context, settings, receiver); return control;
        }
        private SensorStimulus Signal(StimulusKind kind)
        {
            return new SensorStimulus { kind = kind, source = target, position = target.position,
                strength = 10, category = 1, time = 1, lifetime = 10 };
        }
        [Test]
        public void VisionFindsStaysAndLosesTarget()
        {
            var control = Start(Config(SensorKind.Vision)); control.Tick(0.1f, 1);
            Assert.That(receiver.results[0].eventType, Is.EqualTo(SensorEvent.Enter));
            receiver.results.Clear(); control.Tick(0.1f, 2);
            Assert.That(receiver.results.Count, Is.EqualTo(1));
            Assert.That(receiver.results[0].eventType, Is.EqualTo(SensorEvent.Stay));
            target.position = owner.position + Vector3.back * 3; Physics.SyncTransforms();
            receiver.results.Clear(); control.Tick(0.1f, 3);
            Assert.That(receiver.results[0].eventType, Is.EqualTo(SensorEvent.Exit));
        }
        [Test]
        public void VisionRejectsOcclusionAndRange()
        {
            var wall = Create("Wall", owner.position + Vector3.forward * 1.5f); wall.gameObject.AddComponent<BoxCollider>(); Physics.SyncTransforms();
            var control = Start(Config(SensorKind.Vision)); control.Tick(0.1f, 1);
            Assert.That(receiver.results, Is.Empty);
            wall.position += Vector3.right * 20; target.position += Vector3.forward * 20; Physics.SyncTransforms();
            control.Tick(0.1f, 2); Assert.That(receiver.results, Is.Empty);
        }
        [Test]
        public void ProximityIgnoresFacingAndDeduplicatesChildColliders()
        {
            target.position = owner.position + Vector3.back * 3;
            var child = Create("Child", target.position); child.parent = target; child.gameObject.AddComponent<BoxCollider>(); Physics.SyncTransforms();
            var control = Start(Config(SensorKind.Proximity)); control.Tick(0.1f, 1);
            Assert.That(receiver.results.Count, Is.EqualTo(1));
            target.position += Vector3.back * 20; Physics.SyncTransforms(); receiver.results.Clear(); control.Tick(0.1f, 2);
            Assert.That(receiver.results[0].eventType, Is.EqualTo(SensorEvent.Exit));
        }
        [TestCase(SensorKind.Hearing, StimulusKind.Sound, SensorEvent.Heard)]
        [TestCase(SensorKind.Impact, StimulusKind.Hit, SensorEvent.Hit)]
        [TestCase(SensorKind.Impact, StimulusKind.Contact, SensorEvent.Contact)]
        [TestCase(SensorKind.AllyInformation, StimulusKind.AllyInformation, SensorEvent.Alert)]
        public void EventSensorsReceiveOnlyMatchingEvents(SensorKind kind, StimulusKind input, SensorEvent expected)
        {
            var control = Start(Config(kind)); var signal = Signal(input); control.Receive(signal);
            Assert.That(receiver.results.Count, Is.EqualTo(1)); Assert.That(receiver.results[0].eventType, Is.EqualTo(expected));
            signal.position += Vector3.right * 100; control.Receive(signal); Assert.That(receiver.results.Count, Is.EqualTo(1));
        }
        [Test]
        public void HearingFiltersStrengthCategoryAndSelf()
        {
            var settings = Config(SensorKind.Hearing); settings.minimumStrength = 8; settings.categoryMask = 2;
            var control = Start(settings); var signal = Signal(StimulusKind.Sound); control.Receive(signal);
            signal.category = 2; control.Receive(signal); Assert.That(receiver.results, Is.Empty);
            signal.position = owner.position; signal.source = owner; control.Receive(signal); Assert.That(receiver.results, Is.Empty);
            signal.source = target; control.Receive(signal); Assert.That(receiver.results.Count, Is.EqualTo(1));
        }
        [Test]
        public void EnvironmentBroadcastsSoundButRejectsUnaddressedHits()
        {
            Start(Config(SensorKind.Hearing)); var signal = Signal(StimulusKind.Sound);
            ((ISensorStimulusReceiver)environment).Receive(signal); Assert.That(receiver.results.Count, Is.EqualTo(1));
            signal.kind = StimulusKind.Hit;
            Assert.Throws<ArgumentException>(() => ((ISensorStimulusReceiver)environment).Receive(signal));
        }
        [Test]
        public void TrailDecaysExpiresAndReportsCapacityOverflow()
        {
            var control = Start(Config(SensorKind.Trail)); var signal = Signal(StimulusKind.Trail);
            ((ISensorStimulusReceiver)environment).Receive(signal); control.Tick(0.1f, 6);
            Assert.That(receiver.results[0].strength, Is.EqualTo(5).Within(0.001));
            receiver.results.Clear(); control.Tick(0.1f, 11); Assert.That(receiver.results, Is.Empty);
            ((ISensorStimulusReceiver)environment).Receive(signal); ((ISensorStimulusReceiver)environment).Receive(signal);
            Assert.That(environment.TrailOverflowed, Is.True);
        }
        [Test]
        public void DuplicateSettingsAndInvalidNumbersAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Start(Config(SensorKind.Vision), Config(SensorKind.Vision)));
            var settings = Config(SensorKind.Hearing); settings.range = float.NaN;
            Assert.Throws<ArgumentException>(() => Start(settings));
        }
        [Test]
        public void StopIsIdempotentAndPooledReuseDoesNotRetainReceiver()
        {
            var a = Start(Config(SensorKind.Hearing)); a.Stop(); a.Stop();
            receiver.results.Clear(); var oldReceiver = receiver; receiver = new Receiver();
            var b = Start(Config(SensorKind.Hearing)); var signal = Signal(StimulusKind.Sound);
            ((ISensorStimulusReceiver)environment).Receive(signal);
            Assert.That(oldReceiver.results, Is.Empty); Assert.That(receiver.results.Count, Is.EqualTo(1));
        }
        [Test]
        public void QuerySaturationDoesNotInventExit()
        {
            var config = Config(SensorKind.Proximity); config.capacity = 1;
            var control = Start(config); control.Tick(0.1f, 1);
            Assert.That(control.QueryOverflowed, Is.True);
            receiver.results.Clear(); control.Tick(0.1f, 2);
            Assert.That(receiver.results.Exists(r => r.eventType == SensorEvent.Exit), Is.False);
        }
        [Test]
        public void SteadyHearingDoesNotBoxOrAllocate()
        {
            var control = Start(Config(SensorKind.Hearing)); var signal = Signal(StimulusKind.Sound);
            for (int i = 0; i < 10; i++) { control.Receive(signal); receiver.results.Clear(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { control.Receive(signal); receiver.results.Clear(); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [TestCase(SensorKind.Vision)]
        [TestCase(SensorKind.Proximity)]
        public void SteadyRangeScanDoesNotBoxOrAllocate(SensorKind kind)
        {
            var control = Start(Config(kind));
            for (int i = 0; i < 10; i++) { control.Tick(0.1f, i); receiver.results.Clear(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { control.Tick(0.1f, i); receiver.results.Clear(); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}
