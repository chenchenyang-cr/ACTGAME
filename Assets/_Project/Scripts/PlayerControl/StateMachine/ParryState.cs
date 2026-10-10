using CombatEditor;
using UnityEngine;
using UnityLearning.EnemySystem;

public sealed class ParryState : PlayerState
{
    public enum Phase { Start, End, Success }
    public Phase CurrentPhase { get; private set; }
    private bool animationStarted;
    private bool consumed;
    private AbilityScriptableObject successAbility;
    private AbilityScriptableObject contextAbility;
    private EnemyController contextParriedEnemy;
    private bool contextEnding;
    public EnemyController ContextParriedEnemy => contextAbility != null ? contextParriedEnemy : null;
    public bool IsLegacySuccess => contextAbility == null && CurrentPhase == Phase.Success;

    public ParryState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.EndFastMovement();
        if (contextAbility != null)
        {
            Machine.Movement.SetRotationMode(PlayerRotationMode.Animation);
            Machine.Movement.SetRootMotionTranslationScale(1f);
            Machine.NotifyCombatActivity();
            Machine.Combat.BeginAbility(contextAbility);
            animationStarted = Machine.ActionAnimator.PlayAbility(contextAbility);
            return;
        }
        Machine.Movement.SetRotationMode(PlayerRotationMode.Preserve);
        Machine.Movement.SetRootMotionTranslationScale(0f);
        BeginAttempt();
    }

    public void ConfigureContextAction(AbilityScriptableObject ability, EnemyController enemy)
    {
        contextAbility = ability;
        contextParriedEnemy = enemy;
        contextEnding = false;
    }

    private void BeginContextEnd()
    {
        contextEnding = true;
        contextAbility = Machine.Combat.ParryAttackEnd;
        Machine.Combat.BeginAbility(contextAbility);
        animationStarted = Machine.ActionAnimator.PlayAbility(contextAbility);
    }

    private void BeginAttempt()
    {
        consumed = false;
        Machine.NotifyCombatActivity();
        PlayPhase(Phase.Start);
    }

    private void PlayPhase(Phase phase, bool left = false)
    {
        CurrentPhase = phase;
        successAbility = phase == Phase.Success
            ? Machine.Combat.FindAbilityByAnimationName(left
                ? Machine.AnimationProfile.ParryLeftStateName
                : Machine.AnimationProfile.ParryRightStateName)
            : null;
        if (phase == Phase.Start) Machine.Combat.BeginAbility(Machine.Combat.ParryStartAbility);
        else if (phase == Phase.End) Machine.Combat.BeginAbility(Machine.Combat.ParryEndAbility);
        else Machine.Combat.EndAbility();
        animationStarted = Machine.ActionAnimator.PlayTrackedAction(
            phase == Phase.Start ? Machine.Combat.ParryStartAbility.Clip.name :
            phase == Phase.End ? Machine.AnimationProfile.ParryEndStateName :
            left ? Machine.AnimationProfile.ParryLeftStateName : Machine.AnimationProfile.ParryRightStateName,
            phase == Phase.End
                ? Machine.AnimationProfile.ParryEndBlendDuration
                : Machine.AnimationProfile.ParryBlendDuration);
    }

    public override void Tick(Vector2 moveInput, bool hasMoveInput)
    {
        if (contextAbility != null)
        {
            if (hasMoveInput && CanCancel(null, true))
            {
                Machine.ReturnToControllableState();
                return;
            }
            Machine.Movement.Tick(Vector2.zero, false);
            if (!animationStarted)
            {
                Machine.ReturnToControllableState();
                return;
            }
            if (!Machine.ActionAnimator.IsTrackedActionComplete()) return;
            if (contextEnding) Machine.ReturnToControllableState();
            else BeginContextEnd();
            return;
        }
        Machine.Movement.Tick(Vector2.zero, false);
        if (!animationStarted)
        {
            Machine.ReturnToControllableState();
            return;
        }
        if (hasMoveInput && CanCancel(null, true))
        {
            Machine.ReturnToControllableState();
            return;
        }
        bool startReadyToEnd = CurrentPhase == Phase.Start &&
            Machine.ActionAnimator.TryGetTrackedActionTime(out float startTime) &&
            startTime >= Machine.AnimationProfile.ParryStartEndTransitionTime;
        if (!startReadyToEnd && !Machine.ActionAnimator.IsTrackedActionComplete()) return;
        // Successful deflections already include recovery. Only a missed
        // attempt needs the separate block-end animation.
        if (CurrentPhase == Phase.End || CurrentPhase == Phase.Success)
            Machine.ReturnToControllableState();
        else
        {
            consumed = true;
            PlayPhase(Phase.End);
        }
    }

    public bool TryParry(in CombatHitRequest request, out float staggerDuration)
    {
        staggerDuration = 0f;
        if (contextAbility != null) return false;
        if (consumed || CurrentPhase != Phase.Start || !animationStarted ||
            request.SourceEvent == null || request.SourceEvent.HitProfile == null ||
            !request.SourceEvent.HitProfile.CanBeParried ||
            !Machine.ActionAnimator.TryGetTrackedActionTime(out float time) ||
            time >= Machine.AnimationProfile.ParryStartEndTransitionTime) return false;

        Vector3 direction = request.Attacker != null
            ? request.Attacker.transform.position - Machine.transform.position
            : -request.AttackDirection;
        AbilityEventObj_ParryWindow selected = null;
        foreach (AbilityEvent entry in Machine.Combat.ParryStartAbility.events)
        {
            if (entry?.Obj is AbilityEventObj_ParryWindow window &&
                window.Contains(time, entry.EventRange, Machine.transform.forward, direction) &&
                (selected == null || window.Priority > selected.Priority)) selected = window;
        }
        if (selected == null) return false;
        consumed = true;
        staggerDuration = selected.AttackerStaggerDuration;
        PlayPhase(Phase.Success, Vector3.Dot(Machine.transform.right, direction) < 0f);
        return true;
    }

    // Success can only be interrupted by commands permitted in its authored window.
    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (contextAbility != null)
        {
            if (!CanCancel(command, false)) return false;
            switch (command)
            {
                case PlayerActionCommand.Dodge:
                    Machine.ChangeState(Machine.DodgeState);
                    return true;
                case PlayerActionCommand.LightAttack:
                    Machine.ChangeState(Machine.AttackState);
                    return true;
                case PlayerActionCommand.Parry:
                    return Machine.TryRestartContextParry();
                default:
                    return false;
            }
        }
        if (command == PlayerActionCommand.Parry)
        {
            if (CurrentPhase == Phase.Success && !CanCancel(command, false))
                return false;
            BeginAttempt();
            return true;
        }
        if (!CanCancel(command, false)) return false;
        if (command == PlayerActionCommand.Dodge) Machine.ChangeState(Machine.DodgeState);
        else if (command == PlayerActionCommand.LightAttack) Machine.ChangeState(Machine.AttackState);
        else return false;
        return true;
    }

    private bool CanCancel(PlayerActionCommand? command, bool movement)
    {
        AbilityScriptableObject ability = contextAbility != null
            ? contextAbility
            : CurrentPhase == Phase.Success
                ? successAbility
                : CurrentPhase == Phase.End ? Machine.Combat.ParryEndAbility : null;
        if (ability == null ||
            !Machine.ActionAnimator.TryGetTrackedActionTime(out float time)) return false;
        foreach (AbilityEvent entry in ability.events)
        {
            if (entry?.Obj is AbilityEventObj_InterruptWindow window && window.IsActive &&
                time >= entry.EventRange.x && time < entry.EventRange.y &&
                (movement ? window.AllowMovement : command.HasValue && window.Allows(command.Value))) return true;
        }
        return false;
    }
    public override bool TryCompleteAction() => false;

    public override void Exit()
    {
        consumed = true;
        Machine.Combat.EndAbility();
        Machine.Movement.SetRootMotionTranslationScale(1f);
        Machine.ActionAnimator.StopTrackingAction();
        contextAbility = null;
        contextParriedEnemy = null;
        contextEnding = false;
    }
}
