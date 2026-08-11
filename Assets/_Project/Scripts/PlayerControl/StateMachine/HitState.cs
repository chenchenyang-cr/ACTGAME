using UnityEngine;

public sealed class HitState : PlayerState
{
    public HitState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        Machine.RaiseHitStateEntered();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
    }
}
