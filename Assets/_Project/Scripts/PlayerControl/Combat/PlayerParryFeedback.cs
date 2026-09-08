using CombatEditor;
using UnityEngine;

public sealed class PlayerParryFeedback : MonoBehaviour
{
    [SerializeField] private GameObject sparkPrefab;
    [SerializeField] private AudioClip parrySound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.55f;
    [SerializeField, Min(0f)] private float sparkScale = 0.5f;
    [SerializeField, Min(0.01f)] private float hitStopDuration = 0.08f;
    [SerializeField] private AnimationCurve hitStopCurve = AnimationCurve.Linear(0f, 0.05f, 1f, 1f);

    private void OnEnable() => CombatHitEventBus.HitConfirmed += OnHit;
    private void OnDisable() => CombatHitEventBus.HitConfirmed -= OnHit;

    private void OnHit(CombatHitConfirmedEvent hit)
    {
        if (hit.Target != gameObject || hit.ResultType != CombatHitResultType.Parried) return;
        if (sparkPrefab != null)
        {
            Vector3 direction = hit.AttackDirection.sqrMagnitude > 0.0001f
                ? -hit.AttackDirection : transform.forward;
            GameObject spark = Instantiate(sparkPrefab, hit.HitPoint, Quaternion.LookRotation(direction));
            spark.transform.localScale *= sparkScale;
            foreach (ParticleSystem particles in spark.GetComponentsInChildren<ParticleSystem>()) particles.Play(true);
            Destroy(spark, 3f);
        }
        if (parrySound != null) AudioSource.PlayClipAtPoint(parrySound, hit.HitPoint, soundVolume);
        CombatController defender = GetComponentInChildren<CombatController>();
        defender?._animSpeedExecutor?.PlayHitSpeedCurve(hitStopCurve, hitStopDuration, true);
        if (hit.Attacker != defender)
            hit.Attacker?._animSpeedExecutor?.PlayHitSpeedCurve(hitStopCurve, hitStopDuration, true);
    }
}
