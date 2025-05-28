using CoreSystem.Components;
using UnityEngine;

namespace CoreSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class Character : Pawn
    {
        protected virtual void Awake()
        {
            AddPureComponent<MovementComponent>();
        }
    }

}