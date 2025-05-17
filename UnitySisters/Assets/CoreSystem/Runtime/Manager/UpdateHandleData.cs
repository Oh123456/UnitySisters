using CoreSystem.PureComponents;
using CoreSystem.PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CoreSystem
{
    internal class UpdateHandleData
    {
        private List<IUpdateHandle> updateHandles = new List<IUpdateHandle>();
        private List<ILateUpdateHandle> lateUpdateHandles = new List<ILateUpdateHandle>();
        private List<IFixedUpdateHandle> fixedUpdateHandles = new List<IFixedUpdateHandle>();
        public List<IUpdateHandle> UpdateHandles => updateHandles;
        public List<ILateUpdateHandle> LateUpdateHandles => lateUpdateHandles;
        public List<IFixedUpdateHandle> FixedUpdateHandles => fixedUpdateHandles;


        public void AddUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is IUpdateHandle updateHandle)
                updateHandles.Add(updateHandle);
        }

        public void AddFixedUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is IFixedUpdateHandle updateHandle)
                fixedUpdateHandles.Add(updateHandle);
        }

        public void AddLateUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is ILateUpdateHandle updateHandle)
                lateUpdateHandles.Add(updateHandle);
        }


        public void RemoveUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is IUpdateHandle updateHandle)
                updateHandles.Remove(updateHandle);
        }

        public void RemoveFixedUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is IFixedUpdateHandle updateHandle)
                fixedUpdateHandles.Remove(updateHandle);
        }

        public void RemoveLateUpdateHandle(PureComponent pureComponent)
        {
            if (pureComponent is ILateUpdateHandle updateHandle)
                lateUpdateHandles.Remove(updateHandle);
        }
    }

}