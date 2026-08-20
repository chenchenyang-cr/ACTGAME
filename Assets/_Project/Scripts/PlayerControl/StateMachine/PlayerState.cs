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
        return Machine.ReturnToControllableState(moveInput, hasMoveInput);
    }
}
