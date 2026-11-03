using FSM;
using UnityEngine;
using UnityFramework.Animation;

[System.Serializable]
[AnimationEventCommandType(AnimationEventCommandType.Trigger)]
public class InputBufferAnimationEventCommand : AnimationEventCommand
{
    public enum BufferOperation
    {
        Begin,
        End,
        Cancel,
    }

    [SerializeField] private BufferOperation operation;

    public override void Execute(AnimationEventReceiver animationEventReceiver)
    {
        ICharacterFSMCotnroller characterFSMCotnroller = animationEventReceiver.GetInterface<ICharacterFSMCotnroller>();
        if (characterFSMCotnroller == null)
            return;

        switch (operation)
        {
            case BufferOperation.Begin:
                characterFSMCotnroller.EnableInputBuffer();
                break;
            case BufferOperation.End:
                characterFSMCotnroller.DisableInputBuffer();
                break;
            case BufferOperation.Cancel:
                characterFSMCotnroller.CancelInputBuffer();
                break;
        }
    }
}
