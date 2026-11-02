using FSM;

using UnityFramework.Animation;

[System.Serializable]
[AnimationEventCommandType(AnimationEventCommandType.Trigger)]
public class InputBufferAnimationEventCommand : AnimationEventCommand
{
    public override void Execute(AnimationEventReceiver animationEventReceiver)
    {
        ICharacterFSMCotnroller characterFSMCotnroller = animationEventReceiver.GetInterface<ICharacterFSMCotnroller>();
        if (characterFSMCotnroller == null)
            return;

        if (characterFSMCotnroller.IsInputBuffer)
            characterFSMCotnroller.DisableInputBuffer();
        else
            characterFSMCotnroller.EnableInputBuffer();
    }
}
