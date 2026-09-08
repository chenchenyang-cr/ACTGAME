using UnityEngine;

public sealed class HitState : PlayerState
{
    public HitState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
        Machine.Movement.EndFastMovement();
        Machine.ActionAnimator.PlayTrackedAction(Machine.AnimationProfile.HitStateName, 0.06f);
        Machine.RaiseHitStateEntered();
    }

    public override void Exit() => Machine.ActionAnimator.StopTrackingAction();

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
        if (Machine.ActionAnimator.IsTrackedActionComplete()) Machine.RecoverFromHit();
    }
}
