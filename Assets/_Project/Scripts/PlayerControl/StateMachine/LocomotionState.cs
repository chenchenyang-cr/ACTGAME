using UnityEngine;

public sealed class LocomotionState : GroundedState
{
    public LocomotionState(PlayerStateMachine machine) : base(machine) { }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(moveInput, hasMoveInput);

        if (!hasMoveInput)
        {
            Machine.ChangeState(Machine.IdleState);
        }
    }
}
