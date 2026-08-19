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
    [SerializeField] private AbilityScriptableObject dodgeAbility;

    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_RotationWindow> rotationWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_ComboWindow> comboWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_InterruptWindow> interruptWindows = new();
    private readonly HashSet<CombatWindowHandle> exitWindows = new();
    private IPlayerCombatTargetAssist targetAssist;
    private CombatController combatController;
    private GameplayWindowAbilityRunner dodgeWindowRunner;

    public AbilityScriptableObject CurrentAbility { get; private set; }
    public AbilityScriptableObject FirstLightAttack => firstLightAttack;
    public AbilityScriptableObject DodgeAbility => dodgeAbility;
    public bool CanExitAttack => exitWindows.Count > 0;

    public event Action<AbilityScriptableObject> AbilityRequested;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        combatController = GetComponent<CombatController>();
        if (combatController == null) combatController = GetComponentInChildren<CombatController>();
        dodgeWindowRunner = new GameplayWindowAbilityRunner(combatController);
        targetAssist = GetComponent<IPlayerCombatTargetAssist>();
    }

    private void OnDisable()
    {
        EndDodgeAbility();
        ClearWindows();
    }

    public void BeginAbility(AbilityScriptableObject ability)
    {
        dodgeWindowRunner?.End();
        ClearWindows();
        CurrentAbility = ability;
        AbilityRequested?.Invoke(ability);
    }

    public bool BeginDodgeAbility()
    {
        dodgeWindowRunner?.End();
        ClearWindows();
        CurrentAbility = dodgeAbility;
        if (dodgeAbility == null)
        {
            Debug.LogError("Dodge Ability is not configured on PlayerCombatAdapter.", this);
            return false;
        }

        return dodgeWindowRunner != null && dodgeWindowRunner.Begin(dodgeAbility);
    }

    public void UpdateDodgeAbility(float normalizedTime)
    {
        dodgeWindowRunner?.Update(normalizedTime);
    }

    public void EndDodgeAbility()
    {
        dodgeWindowRunner?.End();
        if (CurrentAbility == dodgeAbility)
        {
            CurrentAbility = null;
        }
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

    public bool CanInterruptWithMovement()
    {
        foreach (AbilityEventObj_InterruptWindow window in interruptWindows.Values)
        {
            if (window.AllowMovement) return true;
        }

        return false;
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
                if (exit.ExitOnWindowEnter) stateMachine?.CompleteCurrentAction();
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
        bool rotationPolicyChanged = rotationWindows.Remove(context.Handle);
        comboWindows.Remove(context.Handle);
        interruptWindows.Remove(context.Handle);
        exitWindows.Remove(context.Handle);
        if (rotationPolicyChanged)
        {
            ApplyRotationPolicy();
        }
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
            if (stateMachine != null && stateMachine.CurrentState != stateMachine.AttackState)
            {
                return;
            }

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

    private sealed class GameplayWindowAbilityRunner
    {
        private readonly CombatController controller;
        private readonly List<WindowEntry> entries = new();
        private bool active;

        public GameplayWindowAbilityRunner(CombatController controller)
        {
            this.controller = controller;
        }

        public bool Begin(AbilityScriptableObject ability)
        {
            End();
            if (ability == null || controller == null)
            {
                return false;
            }

            for (int i = 0; i < ability.events.Count; i++)
            {
                AbilityEvent abilityEvent = ability.events[i];
                if (abilityEvent?.Obj is not AbilityEventObj_GameplayWindow window || !window.IsActive)
                {
                    continue;
                }

                AbilityEventEffect effect = window.Initialize();
                effect.eve = abilityEvent;
                effect.AnimObj = ability;
                effect._combatController = controller;
                entries.Add(new WindowEntry(abilityEvent, effect));
            }

            active = true;
            return true;
        }

        public void Update(float normalizedTime)
        {
            if (!active)
            {
                return;
            }

            float currentTime = Mathf.Clamp01(normalizedTime);
            for (int i = 0; i < entries.Count; i++)
            {
                WindowEntry entry = entries[i];
                float startTime = entry.AbilityEvent.GetEventStartTime();
                float endTime = entry.AbilityEvent.GetEventEndTime();
                bool isInsideWindow = currentTime >= startTime && currentTime < endTime;

                if (isInsideWindow && !entry.Effect.IsRunning)
                {
                    entry.Effect.StartEffect();
                    if (!active)
                    {
                        return;
                    }
                }

                if (isInsideWindow && entry.Effect.IsRunning)
                {
                    entry.Effect.EffectRunning(currentTime);
                }
                else if (!isInsideWindow && entry.Effect.IsRunning)
                {
                    entry.Effect.EndEffect();
                }
            }
        }

        public void End()
        {
            if (!active && entries.Count == 0)
            {
                return;
            }

            active = false;
            WindowEntry[] snapshot = entries.ToArray();
            entries.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Effect.IsRunning)
                {
                    snapshot[i].Effect.EndEffect();
                }
            }
        }

        private sealed class WindowEntry
        {
            public AbilityEvent AbilityEvent { get; }
            public AbilityEventEffect Effect { get; }

            public WindowEntry(AbilityEvent abilityEvent, AbilityEventEffect effect)
            {
                AbilityEvent = abilityEvent;
                Effect = effect;
            }
        }
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
