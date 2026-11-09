using _3DModule.Sensing;
using UnityEngine;
using UnitySisters.Table;

namespace UnitySisters.Controller
{
    public sealed class SensorScene : MonoBehaviour, ISensorStimulusReceiver
    {
        [SerializeField] private CharacterAIData[] profiles = System.Array.Empty<CharacterAIData>();
        [SerializeField] private Character[] subjects = System.Array.Empty<Character>();
        [SerializeField, Min(1)] private int trailCapacity = 128;
        private readonly CharacterAITable table = new();
        private SensorEnvironment environment;
        private readonly System.Collections.Generic.List<Character> connected = new();
        public IReadOnlyCharacterAITable Table { get => table; }

        private void Awake()
        {
            environment = new SensorEnvironment(trailCapacity);
        }
        private void OnEnable()
        {
            table.Register(profiles);
            for (int i = 0; i < subjects.Length; i++) RegisterSubject(subjects[i]);
        }
        public void RegisterSubject(Character character)
        {
            if (character != null && environment != null)
            {
                environment.RegisterSubject(character.transform, character.SensorEye);
                character.SensorModel.TrackSubject(environment, character.transform);
            }
        }
        public bool Connect(Character character, object owner)
        {
            if (environment == null || character == null || !Table.TryGet(character.SensorKey, out CharacterAIData data)) return false;
            RegisterSubject(character);
            var context = new SensorContext(character.transform, character.SensorEye, character.SensorEar, environment);
            bool success = character.SensorModel.Connect(owner, context, data);
            if (success && !connected.Contains(character)) connected.Add(character);
            return success;
        }
        void ISensorStimulusReceiver.Receive(in SensorStimulus stimulus)
        {
            if (isActiveAndEnabled && environment != null) ((ISensorStimulusReceiver)environment).Receive(stimulus);
        }
        private void Update() { environment?.Prune(Time.time); }
        private void OnDisable()
        {
            environment?.Clear();
            for (int i = 0; i < connected.Count; i++) if (connected[i] != null) connected[i].SensorModel.Release();
            connected.Clear();
            table.Clear();
        }
    }
}
