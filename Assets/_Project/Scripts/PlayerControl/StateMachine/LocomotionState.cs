using UnityEngine;

public sealed class LocomotionState : GroundedState
{
    public LocomotionState(PlayerStateMachine machine) : base(machine) { }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(moveInput, hasMoveInput);

        if (!Machine.IsGrounded)
        {
            Machine.ChangeState(Machine.AirborneState);
            return;
        }

        if (!hasMoveInput)
        {
            Machine.ChangeState(Machine.IdleState);
        }
    }
}
