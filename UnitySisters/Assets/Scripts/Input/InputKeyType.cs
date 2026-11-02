using UnityEngine;
namespace UnitySisters
{
    [System.Flags]
    public enum InputKeyType
    {
        None = 0,
        Up = 1,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Attack = 1 << 4,
    }

}