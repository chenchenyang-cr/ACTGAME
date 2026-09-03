using System;
using System.Collections.Generic;
using CombatEditor;
using UnityEngine;

/// <summary>
/// Project-specific interpretation of generic CombatEditor gameplay windows.
/// Keep this component outside CombatEditor so the editor package stays portable.
/// </summary>
public sealed class PlayerCombatAdapter : MonoBehaviour, ICombatGameplayWindowListener,
    ICombatTeamProvider
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private AbilityScriptableObject firstLightAttack;
    [SerializeField] private AbilityScriptableObject dodgeAbility;

    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_RotationWindow> rotationWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_ComboWindow> comboWindows = new();
    private readonly Dictionary<CombatWindowHandle, AbilityEventObj_InterruptWindow> interruptWindows = new();
    private readonly Dictionary<CombatWindowHandle, Transform> targetAssistTargets = new();
    private readonly HashSet<CombatWindowHandle> exitWindows = new();
    private CombatController combatController;
    private GameplayWindowAbilityRunner dodgeWindowRunner;
    private bool exitRequested;

    public AbilityScriptableObject CurrentAbility { get; private set; }
    public AbilityScriptableObject FirstLightAttack => firstLightAttack;
    public AbilityScriptableObject DodgeAbility => dodgeAbility;
    public bool CanExitAttack => exitWindows.Count > 0;
    public CombatTeam Team => CombatTeam.Player;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        combatController = GetComponent<CombatController>();
        if (combatController == null) combatController = GetComponentInChildren<CombatController>();
        dodgeWindowRunner = new GameplayWindowAbilityRunner(combatController);
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
    }

    public void EndAbility()
    {
        ClearWindows();
        CurrentAbility = null;
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

    public bool TryGetTransition(
        PlayerActionCommand command,
        out AbilityScriptableObject nextAbility,
        out bool consumeBufferedInput)
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
            nextAbility = selected.NextAbility;
            consumeBufferedInput = selected.ConsumeBufferedInput;
            return true;
        }

        nextAbility = null;
        consumeBufferedInput = true;
        return false;
    }

    public bool ConsumeExitRequest()
    {
        if (!exitRequested)
        {
            return false;
        }

        exitRequested = false;
        return true;
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
            case AbilityEventObj_TargetAssistWindow assist:
                targetAssistTargets[context.Handle] = AcquireTarget(assist.AcquireRadius);
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
                if (exit.ExitOnWindowEnter) exitRequested = true;
                break;
        }
    }

    public void OnCombatWindowUpdated(in CombatGameplayWindowContext context)
    {
        if (context.Window is AbilityEventObj_TargetAssistWindow assist)
        {
            if (!targetAssistTargets.TryGetValue(context.Handle, out Transform target) ||
                target == null || !target.gameObject.activeInHierarchy)
            {
                target = AcquireTarget(assist.AcquireRadius);
                targetAssistTargets[context.Handle] = target;
            }

            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                movement?.FaceWorldDirectionImmediately(direction);
            }
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
        bool targetAssistChanged = targetAssistTargets.Remove(context.Handle);
        if (rotationPolicyChanged || targetAssistChanged)
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

        // Direction assist owns facing while active. Root-motion translation is
        // preserved, but authored root rotation must not pull away from the target.
        if (targetAssistTargets.Count > 0)
        {
            movement.SetRotationMode(PlayerRotationMode.Preserve);
            return;
        }

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
        targetAssistTargets.Clear();
        exitRequested = false;
    }

    private Transform AcquireTarget(float acquireRadius)
    {
        float nearestDistanceSqr = Mathf.Max(0f, acquireRadius);
        nearestDistanceSqr *= nearestDistanceSqr;
        Transform nearest = null;
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not ICombatDamageReceiver receiver ||
                receiver.Team != CombatTeam.Enemy)
            {
                continue;
            }

            Transform candidate = behaviours[i].transform;
            if (candidate.root == transform.root)
            {
                continue;
            }

            Vector3 offset = candidate.position - transform.position;
            offset.y = 0f;

            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr <= nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = candidate;
            }
        }

        return nearest;
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
