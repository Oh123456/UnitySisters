using UnityEngine;

namespace UnitySisters.Controller.Interface
{
    public interface IAICommandControl
    {
        bool TryGetPosition(out Vector3 position);
        void RequestMovement(Vector3 direction);
        bool RequestAttack();
        void RequestRoam();
    }
}
