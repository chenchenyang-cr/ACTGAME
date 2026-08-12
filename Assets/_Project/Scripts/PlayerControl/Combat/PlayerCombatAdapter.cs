using System;
using System.Collections.Generic;
using CombatEditor;
using UnityEngine;

/// <summary>
/// Project-specific interpretation of generic CombatEditor gameplay windows.
/// Keep this component outside CombatEditor so the editor package stays portable.
/// </summary>
public sealed class PlayerCombatAdapter : MonoBehaviour, ICombatGameplayWindowListener
{
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private AbilityScriptableObject firstLightAttack;

    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_RotationWindow> rotationWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_ComboWindow> comboWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_InterruptWindow> interruptWindows = new();
    private readonly HashSet<CombatWindowHandle> exitWindows = new();
    private IPlayerCombatTargetAssist targetAssist;

    public AbilityScriptableObject CurrentAbility { get; private set; }
    public AbilityScriptableObject FirstLightAttack => firstLightAttack;
    public bool CanExitAttack => exitWindows.Count > 0;

    public event Action<AbilityScriptableObject> AbilityRequested;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        targetAssist = GetComponent<IPlayerCombatTargetAssist>();
    }

    private void OnDisable()
    {
        ClearWindows();
    }

    public void BeginAbility(AbilityScriptableObject ability)
    {
        ClearWindows();
        CurrentAbility = ability;
        AbilityRequested?.Invoke(ability);
        stateMachine?.RaiseAbilityRequested(ability);
    }

    public bool TryTransition(PlayerActionCommand command, out bool consumeBufferedInput)
    {
        string commandId = command.ToString();
        AbilityEventObj_ComboWindow selected = null;

        foreach (AbilityEventObj_ComboWindow window in comboWindows.Values)
        {
            if (!string.Equals(window.CommandId, commandId, StringComparison.OrdinalIgnoreCase) ||
                window.NextAbility == null)
            {
                continue;
            }

            if (selected == null || window.Priority > selected.Priority)
            {
                selected = window;
            }
        }

        if (selected != null)
        {
            consumeBufferedInput = selected.ConsumeBufferedInput;
            BeginAbility(selected.NextAbility);
            return true;
        }

        consumeBufferedInput = true;
        return false;
    }

    public bool CanInterrupt(PlayerActionCommand command)
    {
        return IsInterruptAllowed(command.ToString());
    }

    public void OnCombatWindowEntered(in CombatGameplayWindowContext context)
    {
        switch (context.Window)
        {
            case AbilityEventObj_RotationWindow rotation:
                rotationWindows[context.Handle] = rotation;
                ApplyRotationPolicy();
                break;
            case AbilityEventObj_ComboWindow combo:
                comboWindows[context.Handle] = combo;
                break;
            case AbilityEventObj_InterruptWindow interrupt:
                interruptWindows[context.Handle] = interrupt;
                break;
            case AbilityEventObj_ExitWindow exit:
                if (exit.AllowControllerExit) exitWindows.Add(context.Handle);
                if (exit.ExitOnWindowEnter) stateMachine?.CompleteAttack();
                break;
        }
    }

    public void OnCombatWindowUpdated(in CombatGameplayWindowContext context)
    {
        if (context.Window is AbilityEventObj_TargetAssistWindow assist)
        {
            targetAssist?.ApplyTargetAssist(assist, context.NormalizedTime);
        }
    }

    public void OnCombatWindowExited(
        in CombatGameplayWindowContext context,
        CombatWindowExitReason reason)
    {
        rotationWindows.Remove(context.Handle);
        comboWindows.Remove(context.Handle);
        interruptWindows.Remove(context.Handle);
        exitWindows.Remove(context.Handle);
        ApplyRotationPolicy();
    }

    private bool IsInterruptAllowed(string commandId)
    {
        foreach (AbilityEventObj_InterruptWindow window in interruptWindows.Values)
        {
            if (window.Allows(commandId)) return true;
        }

        return false;
    }

    private void ApplyRotationPolicy()
    {
        if (movement == null) return;

        AbilityEventObj_RotationWindow selected = null;
        foreach (AbilityEventObj_RotationWindow window in rotationWindows.Values)
        {
            if (selected == null || window.Priority > selected.Priority) selected = window;
        }

        if (selected == null)
        {
            movement.SetRotationMode(PlayerRotationMode.Preserve);
            return;
        }

        switch (selected.Policy)
        {
            case CombatRotationPolicy.Animation:
                movement.SetRotationMode(PlayerRotationMode.Animation);
                break;
            case CombatRotationPolicy.InputDirection:
            case CombatRotationPolicy.Default:
                movement.SetRotationMode(PlayerRotationMode.MovementDirection);
                break;
            default:
                movement.SetRotationMode(PlayerRotationMode.Preserve);
                break;
        }
    }

    private void ClearWindows()
    {
        rotationWindows.Clear();
        comboWindows.Clear();
        interruptWindows.Clear();
        exitWindows.Clear();
    }
}

/// <summary>
/// Optional project-side hook for the lock-on/targeting implementation. A game may
/// move with CharacterController, Rigidbody or networking without changing CombatEditor.
/// </summary>
public interface IPlayerCombatTargetAssist
{
    void ApplyTargetAssist(AbilityEventObj_TargetAssistWindow config, float normalizedTime);
}
