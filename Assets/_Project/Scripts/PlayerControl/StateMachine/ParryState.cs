using CombatEditor;
using UnityEngine;

public sealed class ParryState : PlayerState
{
    public enum Phase { Start, End, Success }
    public Phase CurrentPhase { get; private set; }
    private bool animationStarted;
    private bool consumed;
    private AbilityScriptableObject successAbility;

    public ParryState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.EndFastMovement();
        Machine.Movement.SetRotationMode(PlayerRotationMode.Preserve);
        Machine.Movement.SetRootMotionTranslationScale(0f);
        BeginAttempt();
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
        if (consumed || CurrentPhase != Phase.Start || !animationStarted ||
            request.SourceEvent == null || !request.SourceEvent.CanBeParried ||
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
        CombatCamera.CameraShotTarget.Set(Machine.gameObject,
            request.Attacker != null ? request.Attacker.transform : null);
        PlayPhase(Phase.Success, Vector3.Dot(Machine.transform.right, direction) < 0f);
        return true;
    }

    // Success can only be interrupted by commands permitted in its authored window.
    public override bool TryHandleCommand(PlayerActionCommand command)
    {
        if (command == PlayerActionCommand.Parry)
        {
            if (CurrentPhase == Phase.Success && !CanCancel(command.ToString(), false))
                return false;
            BeginAttempt();
            return true;
        }
        if (!CanCancel(command.ToString(), false)) return false;
        if (command == PlayerActionCommand.Dodge) Machine.ChangeState(Machine.DodgeState);
        else if (command == PlayerActionCommand.LightAttack) Machine.ChangeState(Machine.AttackState);
        else return false;
        return true;
    }

    private bool CanCancel(string command, bool movement)
    {
        AbilityScriptableObject ability = CurrentPhase == Phase.Success
            ? successAbility
            : CurrentPhase == Phase.End ? Machine.Combat.ParryEndAbility : null;
        if (ability == null ||
            !Machine.ActionAnimator.TryGetTrackedActionTime(out float time)) return false;
        foreach (AbilityEvent entry in ability.events)
        {
            if (entry?.Obj is AbilityEventObj_InterruptWindow window && window.IsActive &&
                time >= entry.EventRange.x && time < entry.EventRange.y &&
                (movement ? window.AllowMovement : window.Allows(command))) return true;
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
    }
}
