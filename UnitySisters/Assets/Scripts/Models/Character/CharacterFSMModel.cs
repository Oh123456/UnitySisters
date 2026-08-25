using UnityFramework.FSM;

namespace UnitySisters.Model
{
    public partial class CharacterFSMModel
    {
        [FSMParameter] public float moveValue;
        [UnityEngine.Serialization.FormerlySerializedAs("isGround")]
        [FSMParameter] public bool isFalling;
    }

}
