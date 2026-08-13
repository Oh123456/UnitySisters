using UnityEngine;

namespace UnitySisters.Controller.Interface
{
    public interface IModelBinder<T1>
    {
        public void SetModel(T1 t);
    }

    public interface IModelBinder<T1, T2>
    {
        public void SetModel(T1 t, T2 t2);
    }

    public interface IModelBinder<T1, T2, T3>
    {
        public void SetModel(T1 t, T2 t2, T3 t3);
    }

    public interface IModelBinder<T1, T2, T3, T4>
    {
        public void SetModel(T1 t, T2 t2, T3 t3, T4 t4);
    }


}