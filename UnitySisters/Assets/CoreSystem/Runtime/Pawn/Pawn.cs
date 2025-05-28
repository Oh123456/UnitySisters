using CoreSystem.PureComponents;
using UnityEngine;

namespace CoreSystem
{
    using Controllers;

    public class Pawn : CustomMonoBehaviour
    {
        private BaseController baseController;

        internal void RemoveController()
        {
            baseController = null;
        }
    }

}