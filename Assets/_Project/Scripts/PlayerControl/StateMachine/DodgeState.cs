using UnityEngine;

public sealed class DodgeState : PlayerState
{
    public DodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        Machine.RaiseDodgeRequested();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
    }
}
