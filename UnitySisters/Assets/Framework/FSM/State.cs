using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.FSM
{
    public abstract class State
    {
        protected int id;
        protected string name;
        protected IStateMachine ownerMachine;
        protected HashSet<int> changeAble = new HashSet<int>();

        public int ID => id;

        public State()
        {
            SetID(out id);
            SetChangeAble(changeAble);
        }


        public void SetOwnerMachine(IStateMachine stateMachine)
        {
            ownerMachine = stateMachine;    
        }

		protected abstract void SetChangeAble(HashSet<int> changeAble);
        protected abstract void SetID(out int id);
		public bool ConditionChangeID(int id) => changeAble.Contains(id);
		
        public virtual void Enter() { }
		public virtual void Update() { }
		public virtual void Exit() { }
	}

}
