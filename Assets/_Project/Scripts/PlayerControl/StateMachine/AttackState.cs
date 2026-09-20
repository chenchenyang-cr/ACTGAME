using UnityEngine;

public sealed class AttackState : PlayerState
{
    private bool consumeLastHandledCommand = true;
    private bool recovering;

    public AttackState(PlayerStateMachine machine) : base(machine) { }

    private void BeginAttack()
    {
        PlayerCombatAdapter combatAdapter = Machine.Combat;
        recovering = false;
        if (combatAdapter == null || !Machine.BeginAttackAbility(combatAdapter.FirstLightAttack)) Complete();
    }

    public override void Enter()
    {
        // Attacking always ends the fast locomotion granted after a dodge.
        // Otherwise returning directly to locomotion while movement input is held
        // keeps MoveSpeed on the fast-run gait.
        Machine.Movement.EndFastMovement();
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        BeginAttack();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        PlayerCombatAdapter combatAdapter = Machine.Combat;
        if (hasMoveInput &&combatAdapter != null &&combatAdapter.CanInterruptWithMovement())
        {
            Complete();
            Machine.Movement.Tick(moveInput, true);
            return;
        }

        Machine.Movement.Tick(Vector2.zero, false);
        var recovery = combatAdapter?.FindAttackRecovery(combatAdapter.CurrentAbility);
        bool atRecovery = !recovering && recovery != null &&
            Machine.ActionAnimator.TryGetTrackedActionTime(out float time) && time >= recovery.StartTime;
        if (atRecovery || (combatAdapter != null && combatAdapter.ConsumeExitRequest()) ||
            Machine.ActionAnimator.IsTrackedActionComplete())
            FinishPhase();
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
                recovering = false;
                consumeLastHandledCommand = consumeBufferedInput;
                return true;
            }

            if (!combatAdapter.CanInterrupt(command)) return false;

            consumeLastHandledCommand = true;
            switch (command)
            {
                case PlayerActionCommand.LightAttack when recovering:
                    if (!Machine.BeginAttackAbility(combatAdapter.FirstLightAttack)) return false;
                    recovering = false;
                    return true;
                case PlayerActionCommand.Parry:
                    return Machine.TryEnterParry();
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
        return FinishPhase();
    }

    public override void Exit()
    {
        Machine.Combat?.EndAbility();
        Machine.ActionAnimator?.StopTrackingAction();
    }

    private bool FinishPhase()
    {
        var recovery = Machine.Combat?.FindAttackRecovery(Machine.Combat.CurrentAbility);
        if (!recovering && recovery != null && recovery.End != null &&
            Machine.BeginAttackAbility(recovery.End, recovery.BlendDuration))
        {
            recovering = true;
            return true;
        }
        return Complete();
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
