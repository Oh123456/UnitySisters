using CoreSystem;
using CoreSystem.Components;
using UnityEngine;

public class PlayerCharacter : Character
{
    protected override void Awake()
    {
        base.Awake();
        AddPureComponent<CinemachineCameraComponent>();
    } 
 
}
