using CombatEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityLearning.EnemySystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine))]
public sealed class PlayerDamageReceiver : MonoBehaviour, ICombatDamageReceiver
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();
    private PlayerStateMachine machine;
    private EnemyController pendingStaggerTarget;
    private float pendingStaggerDuration;
    public CombatTeam Team => CombatTeam.Player;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        machine = GetComponent<PlayerStateMachine>();
        CurrentHealth = maximumHealth;
    }

    private void OnEnable() => CombatHitEventBus.HitConfirmed += OnHitConfirmed;
    private void OnDisable()
    {
        CombatHitEventBus.HitConfirmed -= OnHitConfirmed;
        pendingStaggerTarget = null;
    }

    public bool TryReceiveHit(in CombatHitRequest request, out CombatHitResolution resolution)
    {
        resolution = CombatHitResolution.Rejected;
        if (!isActiveAndEnabled || machine == null || !machine.enabled || CurrentHealth <= 0f) return false;
        if (machine.CurrentState == machine.ParryState &&
            machine.ParryState.TryParry(in request, out float duration))
        {
            // Apply stagger after HitBox has recorded acceptance and published the hit.
            // Interrupting the attacker here can destroy its active hit box mid-resolution.
            pendingStaggerTarget = request.Attacker != null
                ? request.Attacker.GetComponentInParent<EnemyController>() : null;
            pendingStaggerDuration = duration;
            resolution = new CombatHitResolution(true, CombatHitResultType.Parried);
            return true;
        }
        float damage = Mathf.Min(CurrentHealth, request.Damage);
        CurrentHealth -= damage;
        bool killed = CurrentHealth <= 0f;
        bool react = request.HitReaction == CombatHitReactionPolicy.EveryHit ||
                     (request.HitReaction == CombatHitReactionPolicy.FirstHitOnly && request.HitSequenceIndex == 1);
        if (killed || react) machine.EnterHitState();
        if (killed)
        {
            onDeath.Invoke();
            machine.enabled = false;
        }
        resolution = CombatHitResolution.Normal(damage, 0f, killed);
        return true;
    }

    private void OnHitConfirmed(CombatHitConfirmedEvent hit)
    {
        if (hit.Target != gameObject || hit.ResultType != CombatHitResultType.Parried) return;
        EnemyController attacker = pendingStaggerTarget;
        pendingStaggerTarget = null;
        hit.HitBox?.CancelHits();
        // Stop all boxes from this attack immediately, including repeated and multi-box strikes.
        // Their objects are cleaned up by the normal outgoing-animation effect lifecycle.
        if (hit.Attacker != null && hit.Attacker.ClipID_To_EventEffects != null)
        {
            foreach (var entries in hit.Attacker.ClipID_To_EventEffects.Values)
                foreach (var entry in entries)
                    if (entry.effect is AbilityEventEffect_CreateHitBox effect && effect.AnimObj == hit.Ability)
                        effect.CurrentHitBox?.CancelHits();
        }
        attacker?.NotifyStagger(pendingStaggerDuration);
    }
}
