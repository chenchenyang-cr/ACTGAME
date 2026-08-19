using UnityEngine;

public sealed class DodgeState : PlayerState
{
    private bool animationStarted;

    public DodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        PlayerMovementDirectionSnapshot snapshot =
            Machine.Movement.CaptureDirectionSnapshot(
                Machine.HasLatestMoveInput ? Machine.LatestMoveInput : Vector2.zero);
        if (snapshot.HasDirection)
        {
            Machine.Movement.FaceWorldDirectionImmediately(snapshot.WorldDirection);
        }

        Machine.Movement.SetRotationMode(PlayerRotationMode.Preserve);
        Machine.BeginDodgeAbility();
        animationStarted = Machine.PlayDodgeAnimation(Vector2.up);
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
        Machine.UpdateDodgeAbilityWindows();
        if (Machine.CurrentState != Machine.DodgeState)
        {
            return;
        }

        if (hasMoveInput && Machine.CanDodgeInterruptWithMovement())
        {
            Machine.CompleteDodge(moveInput, true);
            return;
        }

        if (!animationStarted || Machine.IsDodgeAnimationComplete())
        {
            Machine.CompleteDodge(moveInput, hasMoveInput);
        }
    }

    public override void Exit()
    {
        Machine.EndDodgeAbility();
        Machine.StopTrackingDodgeAnimation();
    }
}
