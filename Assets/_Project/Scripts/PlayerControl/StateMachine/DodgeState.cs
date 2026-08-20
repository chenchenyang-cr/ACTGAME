using UnityEngine;

public sealed class DodgeState : PlayerState
{
    private bool animationStarted;

    public DodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        PlayerMovementDirectionSnapshot snapshot =
            Machine.Movement.CaptureDirectionSnapshot(Machine.HasLatestMoveInput ? Machine.LatestMoveInput : Vector2.zero);
        if (snapshot.HasDirection)
        {
            Machine.Movement.FaceWorldDirectionImmediately(snapshot.WorldDirection);
        }

        Machine.Movement.SetRotationMode(PlayerRotationMode.Preserve);
        Machine.Movement.SetRootMotionTranslationScale(Machine.DodgeRootMotionMultiplier);
        Machine.Combat?.BeginDodgeAbility();
        animationStarted = Machine.ActionAnimator != null &&
                           Machine.ActionAnimator.PlayDodge(Vector2.up);
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
        if (Machine.ActionAnimator != null &&
            Machine.ActionAnimator.TryGetDodgeNormalizedTime(out float normalizedTime))
        {
            Machine.Combat?.UpdateDodgeAbility(normalizedTime);
        }

        if (Machine.CurrentState != Machine.DodgeState)
        {
            return;
        }

        if (hasMoveInput &&
            Machine.Combat != null &&
            Machine.Combat.CanInterruptWithMovement())
        {
            Complete(moveInput, true);
            return;
        }

        if (!animationStarted ||
            Machine.ActionAnimator == null ||
            Machine.ActionAnimator.IsDodgeComplete())
        {
            Complete(moveInput, hasMoveInput);
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

        return CompleteToControllableState(moveInput, hasMoveInput);
    }
}
