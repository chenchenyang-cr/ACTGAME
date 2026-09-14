using CombatEditor;
using UnityEngine;

public sealed class PlayerParryFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip parrySound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.55f;
    [SerializeField, Min(0.01f)] private float hitStopDuration = 0.08f;
    [SerializeField] private AnimationCurve hitStopCurve = AnimationCurve.Linear(0f, 0.05f, 1f, 1f);

    private void OnEnable() => CombatHitEventBus.HitConfirmed += OnHit;
    private void OnDisable() => CombatHitEventBus.HitConfirmed -= OnHit;

    private void OnHit(CombatHitConfirmedEvent hit)
    {
        if (hit.Target != gameObject || hit.ResultType != CombatHitResultType.Parried) return;
        if (parrySound != null) AudioSource.PlayClipAtPoint(parrySound, hit.HitPoint, soundVolume);
        CombatController defender = GetComponentInChildren<CombatController>();
        defender?._animSpeedExecutor?.PlayHitSpeedCurve(hitStopCurve, hitStopDuration, true);
        if (hit.Attacker != defender)
            hit.Attacker?._animSpeedExecutor?.PlayHitSpeedCurve(hitStopCurve, hitStopDuration, true);
    }
}
