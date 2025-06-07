using CoreSystem;
using CoreSystem.Components;
using UnityEngine;

public class PlayerCharacter : Character
{
    protected override void InitializePureComponent()
    {
        base.InitializePureComponent();
        AddPureComponent<CinemachineCameraComponent>();
    }
 
}
