public abstract class GroundedState : PlayerState
{
    protected GroundedState(PlayerStateMachine machine) : base(machine) { }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        switch (command)
        {
            case PlayerActionCommand.Jump:
                if (!Machine.IsGrounded)
                {
                    return false;
                }

                Machine.ChangeState(Machine.AirborneState);
                Machine.RaiseJumpRequested();
                return true;

            case PlayerActionCommand.Dodge:
                Machine.ChangeState(Machine.DodgeState);
                return true;

            case PlayerActionCommand.LightAttack:
                Machine.AttackState.BeginAttack();
                Machine.ChangeState(Machine.AttackState);
                return true;

            default:
                return false;
        }
    }
}
