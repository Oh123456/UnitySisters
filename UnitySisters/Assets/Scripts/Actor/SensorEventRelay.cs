using _3DModule.Sensing;
using UnityEngine;

namespace UnitySisters.Controller
{
    public sealed class SensorEventRelay : MonoBehaviour, ISensorStimulusReceiver
    {
        [SerializeField] private Character character;
        [SerializeField] private SensorScene scene;

        private void Reset() { character = GetComponentInParent<Character>(); }
        void ISensorStimulusReceiver.Receive(in SensorStimulus stimulus)
        {
            if (!isActiveAndEnabled) return;
            if (stimulus.kind == StimulusKind.Sound || stimulus.kind == StimulusKind.Trail)
            {
                if (scene != null) ((ISensorStimulusReceiver)scene).Receive(stimulus);
            }
            else if (character != null) ((ISensorStimulusReceiver)character.SensorModel.Control).Receive(stimulus);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var other = hit.collider.GetComponentInParent<Character>();
            var signal = new SensorStimulus { kind = StimulusKind.Contact,
                source = other != null ? other.transform : hit.collider.transform,
                position = hit.point, direction = hit.moveDirection, strength = 1, time = Time.time, category = 1 };
            ((ISensorStimulusReceiver)this).Receive(signal);
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            var other = collision.collider.GetComponentInParent<Character>();
            var signal = new SensorStimulus { kind = StimulusKind.Contact,
                source = other != null ? other.transform : collision.transform,
                position = contact.point, direction = collision.relativeVelocity, strength = collision.relativeVelocity.magnitude,
                time = Time.time, category = 1 };
            ((ISensorStimulusReceiver)this).Receive(signal);
        }
    }
}
