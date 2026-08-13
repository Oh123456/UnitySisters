using UnityEngine;
namespace UnityFramework.FSM
{

    public sealed class EmptyState : State
    {
        public EmptyState(int id, string name = null) : base(id, name)
        {
        }
    }
}