using UnityEngine;

namespace UnitySisters.Model
{
    public sealed class CharacterPerceptionModel
    {
        private Transform candidate;
        private bool visible;
        private bool nearby;
        private bool hasInvestigation;
        private Vector3 investigationPosition;
        private bool hasLastSeen;
        private bool searchPending;
        private Vector3 lastSeenPosition;
        public bool HasLastSeen { get => hasLastSeen; internal set => hasLastSeen = value; }
        public bool SearchPending { get => searchPending; internal set => searchPending = value; }
        public Vector3 LastSeenPosition { get => lastSeenPosition; internal set => lastSeenPosition = value; }
        public Transform Candidate { get => candidate; internal set => candidate = value; }
        public bool Visible { get => visible; internal set => visible = value; }
        public bool Nearby { get => nearby; internal set => nearby = value; }
        public bool HasInvestigation { get => hasInvestigation; internal set => hasInvestigation = value; }
        public Vector3 InvestigationPosition { get => investigationPosition; internal set => investigationPosition = value; }
        public Transform DetectedTarget { get => candidate != null && candidate.gameObject.activeInHierarchy && (visible || nearby) ? candidate : null; }
    }
}
