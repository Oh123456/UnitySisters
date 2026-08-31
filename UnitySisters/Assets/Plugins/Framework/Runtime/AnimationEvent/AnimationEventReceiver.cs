using System;
using UnityEngine;

namespace UnityFramework.Animation
{

    public sealed class AnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private GameObject controlObject;

        public T GetInterface<T>() where T : class
        {
            if (!typeof(T).IsInterface)
                throw new InvalidOperationException();
            return controlObject.GetComponent<T>();
        }

    }

}