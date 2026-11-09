using System.Collections.Generic;
using _3DModule.Sensing;
using UnityEngine;

namespace UnitySisters.Table
{
    [CreateAssetMenu(menuName = "UnitySisters/Table/Character AI", fileName = "CharacterAIData")]
    public sealed class CharacterAIData : ScriptableObject
    {
        [SerializeField] private string key;
        [SerializeField, Min(1)] private int resultCapacity = 64;
        [SerializeField] private SensorSettings[] sensors = System.Array.Empty<SensorSettings>();
        [SerializeField] private AIBehaviorSettings behavior = AIBehaviorSettings.Default;
        public AIBehaviorSettings Behavior { get => behavior; }
        public string Key { get => key; }
        public int ResultCapacity { get => resultCapacity; }
        public IReadOnlyList<SensorSettings> Sensors { get => sensors; }

        public void Validate()
        {
            behavior.Validate();
            if (string.IsNullOrWhiteSpace(key) || resultCapacity < 1 || sensors == null)
                throw new System.ArgumentException("Character AI key, result capacity and sensors are required.");
            int kinds = 0;
            for (int i = 0; i < sensors.Length; i++)
            {
                sensors[i].Validate();
                int flag = 1 << (int)sensors[i].kind;
                if ((kinds & flag) != 0) throw new System.ArgumentException("Duplicate sensor kind in AI table row.");
                kinds |= flag;
            }
        }
    }
}
