using UnityEngine;

namespace CombatEditor
{
    public enum CombatHitVfxDirectionMode
    {
        AttackDirection,
        SurfaceNormal,
        AttackerToTarget,
        CameraFacing
    }

    public static class CombatHitVfxManager
    {
        private const float DirectionEpsilon = 0.0001f;
        private const float DefaultLifetime = 2f;
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (subscribed)
                CombatHitEventBus.HitConfirmed -= OnHitConfirmed;
            subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (subscribed)
                return;

            CombatHitEventBus.HitConfirmed += OnHitConfirmed;
            subscribed = true;
        }

        private static void OnHitConfirmed(CombatHitConfirmedEvent hitEvent)
        {
            AbilityEventObj_CreateHitBox config = hitEvent.SourceHitBoxEvent;
            if (config == null || !config.EnableHitVfx || config.HitVfxPrefab == null ||
                (config.HitVfxResultMask & hitEvent.ResultMask) == 0)
                return;

            Vector3 direction = ResolveDirection(config.HitVfxDirection, hitEvent);
            Quaternion rotation = Quaternion.LookRotation(direction, ResolveUp(direction)) *
                                  Quaternion.Euler(config.HitVfxRotationOffset);
            Vector3 position = hitEvent.HitPoint +
                               rotation * config.HitVfxPositionOffset;

            GameObject instance = Object.Instantiate(config.HitVfxPrefab, position,
                rotation);
            instance.transform.localScale = config.HitVfxPrefab.transform.localScale *
                                            Mathf.Max(0f, config.HitVfxScale);

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);

            float lifetime = config.HitVfxLifetime > 0f
                ? config.HitVfxLifetime
                : EstimateLifetime(particles);
            Object.Destroy(instance, lifetime);
        }

        private static Vector3 ResolveDirection(CombatHitVfxDirectionMode mode,
            CombatHitConfirmedEvent hitEvent)
        {
            Vector3 direction;
            switch (mode)
            {
                case CombatHitVfxDirectionMode.SurfaceNormal:
                    direction = ResolveSurfaceNormal(hitEvent);
                    break;
                case CombatHitVfxDirectionMode.AttackerToTarget:
                    direction = hitEvent.Target != null && hitEvent.Attacker != null
                        ? hitEvent.Target.transform.position -
                          hitEvent.Attacker.transform.position
                        : hitEvent.AttackDirection;
                    break;
                case CombatHitVfxDirectionMode.CameraFacing:
                    Camera camera = Camera.main;
                    direction = camera != null
                        ? camera.transform.position - hitEvent.HitPoint
                        : -hitEvent.AttackDirection;
                    break;
                default:
                    direction = hitEvent.AttackDirection;
                    break;
            }

            if (direction.sqrMagnitude <= DirectionEpsilon &&
                hitEvent.Attacker != null)
                direction = hitEvent.Attacker.transform.forward;
            return direction.sqrMagnitude > DirectionEpsilon
                ? direction.normalized
                : Vector3.forward;
        }

        private static Vector3 ResolveSurfaceNormal(CombatHitConfirmedEvent hitEvent)
        {
            Vector3 center;
            if (hitEvent.TargetCollider is Collider collider)
            {
                center = collider.bounds.center;
            }
            else if (hitEvent.TargetCollider is Collider2D collider2D)
            {
                center = collider2D.bounds.center;
            }
            else
            {
                center = hitEvent.Target != null
                    ? hitEvent.Target.transform.position
                    : hitEvent.HitPoint;
            }

            if (center != hitEvent.HitPoint)
            {
                Vector3 fromCenter = hitEvent.HitPoint - center;
                if (fromCenter.sqrMagnitude > DirectionEpsilon)
                    return fromCenter.normalized;
            }

            return -hitEvent.AttackDirection;
        }

        private static Vector3 ResolveUp(Vector3 forward)
        {
            return Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
        }

        private static float EstimateLifetime(ParticleSystem[] particles)
        {
            float lifetime = 0f;
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule main = particles[i].main;
                float particleLifetime = main.startDelay.constantMax + main.duration +
                                         main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, particleLifetime);
            }

            return lifetime > 0f ? lifetime : DefaultLifetime;
        }
    }
}
