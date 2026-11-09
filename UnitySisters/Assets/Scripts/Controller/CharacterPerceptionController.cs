using _3DModule.Sensing;
using UnityEngine;
using UnitySisters.Controller.Interface;
using UnitySisters.Model;

namespace UnitySisters.Controller
{
    public sealed class CharacterPerceptionController : IModelBinder<CharacterPerceptionModel>
    {
        private CharacterPerceptionModel model;
        public void SetModel(CharacterPerceptionModel model) { this.model = model; }
        public void Reset(Transform candidate)
        {
            model.Candidate = candidate; model.Visible = false; model.Nearby = false;
            model.HasInvestigation = false; model.InvestigationPosition = default;
            model.HasLastSeen = false; model.SearchPending = false; model.LastSeenPosition = default;
        }
        public void Consume(IReadOnlySensorResults results)
        {
            bool wasDetected = model.DetectedTarget != null;
            for (int i = 0; i < results.Count; i++)
            {
                if (!results.TryGetResult(i, out var result)) continue;
                bool candidate = model.Candidate != null && result.source != null &&
                    (result.source == model.Candidate || result.source.IsChildOf(model.Candidate));
                if (result.sensor == SensorKind.Vision || result.sensor == SensorKind.Proximity)
                {
                    if (!candidate) continue;
                    bool detected = result.eventType != SensorEvent.Exit;
                    if (result.sensor == SensorKind.Vision)
                    {
                        model.Visible = detected;
                        if (detected) { model.LastSeenPosition = result.position; model.HasLastSeen = true; }
                    }
                    else model.Nearby = detected;
                    continue;
                }
                if (result.source != null && !candidate && result.sensor != SensorKind.AllyInformation) continue;
                if (result.eventType == SensorEvent.Heard || result.eventType == SensorEvent.Hit ||
                    result.eventType == SensorEvent.Contact || result.eventType == SensorEvent.Alert || result.eventType == SensorEvent.Trail)
                {
                    model.InvestigationPosition = result.position;
                    model.HasInvestigation = true;
                }
            }
            if (model.DetectedTarget != null) { model.HasInvestigation = false; model.SearchPending = false; }
            else if (wasDetected && model.HasLastSeen) model.SearchPending = true;
        }
        public void CompleteInvestigation() { model.HasInvestigation = false; }
        public void CompleteSearch() { model.SearchPending = false; }
    }
}
