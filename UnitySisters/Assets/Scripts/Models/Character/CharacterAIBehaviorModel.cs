using UnityEngine;
using UnitySisters.Table;

namespace UnitySisters.Model
{
    public sealed class CharacterAIBehaviorModel
    {
        internal AIBehaviorSettings settings;
        internal Transform owner;
        internal Vector3 roamCenter;
        internal Vector3 destination;
        internal bool hasDestination;
        internal float waitUntil;
        internal Vector3 progressPosition;
        internal float progressAt;
        internal float attackReadyAt;
        internal bool canAttack;
        internal float attackDistance01;
        public Vector3 RoamCenter { get => roamCenter; }
        public Vector3 Destination { get => destination; }
        public bool HasDestination { get => hasDestination; }
        public bool CanAttack { get => canAttack; }
        public float AttackDistance01 { get => attackDistance01; }
        public float AttackReadyAt { get => attackReadyAt; }
    }
}
