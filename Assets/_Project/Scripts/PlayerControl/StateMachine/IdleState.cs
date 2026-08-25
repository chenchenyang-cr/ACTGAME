using UnityEngine;

public sealed class IdleState : GroundedState
{
    public IdleState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        base.Enter();
        Machine.Movement.EndFastMovement();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(moveInput, hasMoveInput);

        if (hasMoveInput)
        {
            Machine.ChangeState(Machine.LocomotionState);
        }
    }
}
