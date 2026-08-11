using UnityEngine;

public sealed class IdleState : GroundedState
{
    public IdleState(PlayerStateMachine machine) : base(machine) { }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(moveInput, hasMoveInput);

        if (!Machine.IsGrounded)
        {
            Machine.ChangeState(Machine.AirborneState);
            return;
        }

        if (hasMoveInput)
        {
            Machine.ChangeState(Machine.LocomotionState);
        }
    }
}
