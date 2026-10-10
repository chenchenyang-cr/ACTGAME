using UnityEngine;

public sealed class DodgeState : PlayerState
{
    private bool animationStarted;
    private bool continuationSelected;

    public DodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        continuationSelected = false;
        PlayerMovementDirectionSnapshot snapshot =
            Machine.Movement.CaptureDirectionSnapshot(Machine.HasLatestMoveInput ? Machine.LatestMoveInput : Vector2.zero);
        bool directionalDodge = !string.IsNullOrEmpty(Machine.AnimationProfile.DirectionalDodgeStateName);
        if (snapshot.HasDirection)
        {
            Machine.Movement.FaceWorldDirectionImmediately(snapshot.WorldDirection);
        }

        Machine.Movement.SetRotationMode(PlayerRotationMode.Preserve);
        Machine.Movement.SetRootMotionTranslationScale(Machine.DodgeRootMotionMultiplier);
        if (Machine.Combat != null && Machine.Combat.DodgeAbility != null)
            Machine.Combat.BeginDodgeAbility();
        animationStarted = Machine.ActionAnimator != null &&
                           Machine.ActionAnimator.PlayDodge(
                               directionalDodge
                                   ? (snapshot.HasDirection ? Vector2.up : Vector2.down)
                                   : Vector2.up,
                               Machine.HasLatestMoveInput);
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        if (Machine.ActionAnimator != null &&
            Machine.ActionAnimator.TryGetDodgeNormalizedTime(out float normalizedTime))
        {
            Machine.Combat?.UpdateDodgeAbility(normalizedTime);
        }

        if (Machine.CurrentState != Machine.DodgeState)
        {
            return;
        }

        if (hasMoveInput && Machine.Combat != null && Machine.Combat.CanInterruptWithMovement() &&
            Machine.EnterFastLocomotionLoop(moveInput))
        {
            Machine.Movement.Tick(moveInput, true);
            return;
        }

        Machine.Movement.Tick(Vector2.zero, false, allowTurn180: false);

        if (!continuationSelected &&
            string.IsNullOrEmpty(Machine.AnimationProfile.DirectionalDodgeStateName) &&
            Machine.ActionAnimator != null &&
            Machine.ActionAnimator.TryGetDodgeElapsedTime(out float elapsed) &&
            elapsed >= Machine.AnimationProfile.DodgeContinuationDecisionTime)
        {
            continuationSelected = true;
            Machine.ActionAnimator.SetDodgeContinuation(hasMoveInput);
        }

        if (!animationStarted ||
            Machine.ActionAnimator == null ||
            Machine.ActionAnimator.IsDodgeComplete())
        {
            Complete(moveInput, hasMoveInput);
        }
    }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (Machine.Combat == null || !Machine.Combat.CanInterrupt(command))
            return false;

        switch (command)
        {
            case PlayerActionCommand.Dodge:
                // ChangeState ignores transitions to the current state. Restart the
                // dodge here so the authored Dodge window can cancel its recovery.
                Exit();
                Enter();
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

    public override void Exit()
    {
        Machine.Movement.SetRootMotionTranslationScale(1f);
        Machine.Combat?.EndDodgeAbility();
        Machine.ActionAnimator?.StopTrackingDodge();
    }

    public override bool TryCompleteAction()
    {
        return Complete(Machine.LatestMoveInput, Machine.HasLatestMoveInput);
    }

    private bool Complete(Vector2 moveInput, bool hasMoveInput)
    {
        if (Machine.CurrentState != this)
        {
            return false;
        }

        if (hasMoveInput)
        {
            return Machine.EnterFastLocomotionLoop(moveInput);
        }

        return CompleteToControllableState(moveInput, hasMoveInput);
    }
}
