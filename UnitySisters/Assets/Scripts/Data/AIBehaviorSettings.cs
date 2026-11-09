using System;
using UnityEngine;

namespace UnitySisters.Table
{
    [Serializable]
    public struct AIBehaviorSettings
    {
        [Min(0.1f)] public float attackRange;
        [Min(0.1f)] public float attackRequestInterval;
        [Min(1)] public float roamRadius;
        [Min(0)] public float roamPause;
        [Min(0.1f)] public float stuckTimeout;
        public LayerMask groundLayers;
        public LayerMask obstacleLayers;
        public static AIBehaviorSettings Default
        {
            get => new AIBehaviorSettings { attackRange = 1.8f, attackRequestInterval = 1.3f,
                roamRadius = 6, roamPause = 1, stuckTimeout = 3, groundLayers = ~0, obstacleLayers = ~0 };
        }
        public void Validate()
        {
            if (!Finite(attackRange) || attackRange <= 0 || !Finite(attackRequestInterval) || attackRequestInterval <= 0 ||
                !Finite(roamRadius) || roamRadius < 1 || !Finite(roamPause) || roamPause < 0 || !Finite(stuckTimeout) || stuckTimeout <= 0)
                throw new ArgumentException("Invalid AI behavior settings.");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
