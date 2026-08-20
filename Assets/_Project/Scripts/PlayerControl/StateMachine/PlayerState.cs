using UnityEngine;

public abstract class PlayerState
{
    protected PlayerStateMachine Machine { get; }

    protected PlayerState(PlayerStateMachine machine)
    {
        Machine = machine;
    }

    public virtual void Enter() { }

    public virtual void Exit() { }

    public virtual void Tick(Vector2 moveInput, bool hasMoveInput) { }

    public virtual bool TryHandleCommand(PlayerActionCommand command)
    {
        return false;
    }

    public virtual bool ShouldConsumeHandledCommand(PlayerActionCommand command)
    {
        return true;
    }

    public virtual bool TryCompleteAction()
    {
        return false;
    }

    protected bool CompleteToControllableState(Vector2 moveInput, bool hasMoveInput)
    {
        if (!Machine.IsGrounded)
        {
            Machine.ChangeState(Machine.AirborneState);
            return true;
        }

        Vector2 currentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        bool returnToLocomotion = hasMoveInput ||
                                  currentMoveInput.sqrMagnitude > 0.0001f;
        if (!returnToLocomotion)
        {
            Machine.ChangeState(Machine.IdleState);
            Machine.ActionAnimator?.PlayIdle();
            return true;
        }

        Machine.ChangeState(Machine.LocomotionState);
        Machine.Movement.PrepareLocomotionAnimation(currentMoveInput);
        Machine.ActionAnimator?.PlayLocomotion();
        return true;
    }
}
