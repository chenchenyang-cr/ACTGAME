using UnityEngine;

public sealed class AttackState : PlayerState
{
    private bool consumeLastHandledCommand = true;

    public AttackState(PlayerStateMachine machine) : base(machine) { }

    private void BeginAttack()
    {
        PlayerCombatAdapter combatAdapter = Machine.Combat;
        if (combatAdapter != null && combatAdapter.FirstLightAttack != null)
        {
            Machine.BeginAttackAbility(combatAdapter.FirstLightAttack);
        }
    }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        BeginAttack();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        PlayerCombatAdapter combatAdapter = Machine.Combat;
        if (combatAdapter != null && combatAdapter.ConsumeExitRequest())
        {
            Complete();
            return;
        }

        if (hasMoveInput &&combatAdapter != null &&combatAdapter.CanInterruptWithMovement())
        {
            Complete();
            Machine.Movement.Tick(moveInput, true);
            return;
        }

        Machine.Movement.Tick(Vector2.zero, false);
    }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        PlayerCombatAdapter combatAdapter = Machine.Combat;
        if (combatAdapter != null && combatAdapter.CurrentAbility != null)
        {
            if (combatAdapter.TryGetTransition(
                    command,
                    out CombatEditor.AbilityScriptableObject nextAbility,
                    out bool consumeBufferedInput) &&
                Machine.BeginAttackAbility(nextAbility))
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

    public override bool TryCompleteAction()
    {
        return Complete();
    }

    public override void Exit()
    {
        Machine.Combat?.EndAbility();
    }

    private bool Complete()
    {
        if (Machine.CurrentState != this)
        {
            return false;
        }

        return CompleteToControllableState(
            Machine.LatestMoveInput,
            Machine.HasLatestMoveInput);
    }

}
