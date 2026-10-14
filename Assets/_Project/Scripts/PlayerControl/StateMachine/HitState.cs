using UnityEngine;

public sealed class HitState : PlayerState
{
    public HitState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        Machine.Movement.EndFastMovement();
        if (Machine.Combat != null && Machine.Combat.HitAbility != null)
            Machine.Combat.BeginAbility(Machine.Combat.HitAbility);
        Machine.ActionAnimator.PlayTrackedAction(Machine.AnimationProfile.HitStateName, 0.06f);
        Machine.RaiseHitStateEntered();
    }

    public override void Exit()
    {
        Machine.Combat?.EndAbility();
        Machine.ActionAnimator.StopTrackingAction();
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        if (hasMoveInput && Machine.Combat != null &&
            Machine.Combat.CanInterruptWithMovement())
        {
            CompleteToControllableState(moveInput, hasMoveInput);
            return;
        }
        Machine.Movement.Tick(Vector2.zero, false);
        if (Machine.ActionAnimator.IsTrackedActionComplete()) Machine.RecoverFromHit();
    }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (Machine.Combat == null || !Machine.Combat.CanInterrupt(command)) return false;
        switch (command)
        {
            case PlayerActionCommand.Dodge:
                Machine.ChangeState(Machine.DodgeState);
                return true;
            case PlayerActionCommand.LightAttack:
                Machine.ChangeState(Machine.AttackState);
                return true;
            case PlayerActionCommand.Parry:
                return Machine.TryEnterParry();
            default:
                return false;
        }
    }
}
