using UnityEngine;

public sealed class AttackState : PlayerState
{
    private PlayerCombatAdapter combatAdapter;
    private bool consumeLastHandledCommand = true;

    public AttackState(PlayerStateMachine machine) : base(machine) { }

    private void BeginAttack()
    {
        combatAdapter = Machine.GetComponent<PlayerCombatAdapter>();
        if (combatAdapter != null && combatAdapter.FirstLightAttack != null)
        {
            combatAdapter.BeginAbility(combatAdapter.FirstLightAttack);
        }
    }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        BeginAttack();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        if (hasMoveInput &&combatAdapter != null &&combatAdapter.CanInterruptWithMovement())
        {
            Machine.CompleteAttack();
            Machine.Movement.Tick(moveInput, true);
            return;
        }

        Machine.Movement.Tick(Vector2.zero, false);
    }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (combatAdapter != null && combatAdapter.CurrentAbility != null)
        {
            if (combatAdapter.TryTransition(command, out bool consumeBufferedInput))
            {
                consumeLastHandledCommand = consumeBufferedInput;
                return true;
            }

            if (!combatAdapter.CanInterrupt(command)) return false;

            consumeLastHandledCommand = true;
            switch (command)
            {
                case PlayerActionCommand.Dodge:
                    Machine.ChangeState(Machine.DodgeState);
                    return true;
                case PlayerActionCommand.Jump when Machine.IsGrounded && Machine.Movement.TryJump():
                    Machine.ChangeState(Machine.AirborneState);
                    Machine.RaiseJumpRequested();
                    return true;
                default:
                    return false;
            }
        }

        return false;
    }

    public override bool ShouldConsumeHandledCommand(PlayerActionCommand command)
    {
        return consumeLastHandledCommand;
    }

}
