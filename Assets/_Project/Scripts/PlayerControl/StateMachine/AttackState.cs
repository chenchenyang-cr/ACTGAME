using UnityEngine;

public sealed class AttackState : PlayerState
{
    private bool comboWindowOpen;
    private int comboIndex;

    public AttackState(PlayerStateMachine machine) : base(machine) { }

    public void BeginAttack()
    {
        comboIndex = 1;
        comboWindowOpen = false;
    }

    public override void Enter()
    {
        Machine.RaiseLightAttackRequested(comboIndex);
    }

    public override void Exit()
    {
        comboWindowOpen = false;
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        Machine.Movement.Tick(Vector2.zero, false);
    }

    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (command != PlayerActionCommand.LightAttack || !comboWindowOpen)
        {
            return false;
        }

        comboWindowOpen = false;
        comboIndex++;
        Machine.RaiseLightAttackRequested(comboIndex);
        return true;
    }

    public void OpenComboWindow()
    {
        comboWindowOpen = true;
    }

    public void CloseComboWindow()
    {
        comboWindowOpen = false;
    }
}
