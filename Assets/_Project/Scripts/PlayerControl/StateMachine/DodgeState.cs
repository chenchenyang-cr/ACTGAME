using UnityEngine;

public sealed class DodgeState : PlayerState
{
    private bool animationStarted;
    private bool continuationSelected;
    private bool movementUnlocked;

    public DodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        continuationSelected = false;
        movementUnlocked = false;
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
                           Machine.ActionAnimator.PlayDodge(Vector2.up, Machine.HasLatestMoveInput);
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

        // Input recovery is controlled by the authored movement window, not
        // by the end of the longer dodge/recovery animation.
        movementUnlocked |= Machine.Combat != null && Machine.Combat.CanInterruptWithMovement();
        Machine.Movement.SetRotationMode(movementUnlocked
            ? PlayerRotationMode.MovementDirection
            : PlayerRotationMode.Preserve);
        if (movementUnlocked) Machine.Movement.SetRootMotionTranslationScale(1f);
        Machine.Movement.Tick(movementUnlocked ? moveInput : Vector2.zero,
            movementUnlocked && hasMoveInput, allowTurn180: false);

        if (!continuationSelected && Machine.ActionAnimator != null &&
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
