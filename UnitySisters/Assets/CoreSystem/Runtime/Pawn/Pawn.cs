using CoreSystem.PureComponents;
using UnityEngine;

namespace CoreSystem
{
    using Controllers;

    public class Pawn : CustomMonoBehaviour
    {
        public Pawn()
        {
            
        }

        public void SetController(BaseController controller)
        {
            controller.controlPawn = this;
        }
    }

}