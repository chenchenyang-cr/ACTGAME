using UnityEngine;

public sealed class AttackState : PlayerState
{
    private bool comboWindowOpen;
    private int comboIndex;
    private PlayerCombatAdapter combatAdapter;
    private bool consumeLastHandledCommand = true;

    public AttackState(PlayerStateMachine machine) : base(machine) { }

    public void BeginAttack()
    {
        comboIndex = 1;
        comboWindowOpen = false;
        combatAdapter = Machine.GetComponent<PlayerCombatAdapter>();
        if (combatAdapter != null && combatAdapter.FirstLightAttack != null)
        {
            combatAdapter.BeginAbility(combatAdapter.FirstLightAttack);
        }
    }

    public override void Enter()
    {
        Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
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
        if (combatAdapter != null && combatAdapter.CurrentAbility != null)
        {
            if (combatAdapter.TryTransition(command, out bool consumeBufferedInput))
            {
                consumeLastHandledCommand = consumeBufferedInput;
                comboIndex++;
                Machine.RaiseLightAttackRequested(comboIndex);
                return true;
            }

            if (!combatAdapter.CanInterrupt(command)) return false;

            consumeLastHandledCommand = true;
            switch (command)
            {
                case PlayerActionCommand.Dodge:
                    Machine.ChangeState(Machine.DodgeState);
                    return true;
                case PlayerActionCommand.Jump when Machine.IsGrounded:
                    Machine.ChangeState(Machine.AirborneState);
                    Machine.RaiseJumpRequested();
                    return true;
                default:
                    return false;
            }
        }

        if (command != PlayerActionCommand.LightAttack || !comboWindowOpen)
        {
            return false;
        }

        comboWindowOpen = false;
        comboIndex++;
        Machine.RaiseLightAttackRequested(comboIndex);
        return true;
    }

    public override bool ShouldConsumeHandledCommand(PlayerActionCommand command)
    {
        return consumeLastHandledCommand;
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
