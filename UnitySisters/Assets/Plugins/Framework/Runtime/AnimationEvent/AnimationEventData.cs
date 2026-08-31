using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.Animation
{
    [CreateAssetMenu(fileName = "AnimationEventData", menuName = "Scriptable Objects/AnimationEventData")]
    public class AnimationEventData : ScriptableObject
    {
        [SerializeReference, SerializeReferenceSelector] private List<AnimationEventCommand> events;

        public IReadOnlyList<AnimationEventCommand> Events => events;
    }

}
