using UnityEngine;

namespace UnityFramework.Animation
{
    public abstract partial class AnimationEventCommand
    {
#if UNITY_EDITOR
        [SerializeField] private string editorEventName;

        public string EditorEventName => string.IsNullOrWhiteSpace(editorEventName) ? GetType().Name : editorEventName;
#endif
    }
}
