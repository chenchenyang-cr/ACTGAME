using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    /// <summary>Applies a short visual-only pause without changing Time.timeScale.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class GlobalHitStopRuntime : MonoBehaviour
    {
        private struct Stop
        {
            public float StartTime;
            public float EndTime;
            public float Speed;
            public HitStopFadeSettings Fades;

            public float EvaluateSpeed(float now)
            {
                if (!Fades.FadeIn && !Fades.FadeOut) return Speed;

                float duration = EndTime - StartTime;
                float elapsed = Mathf.Clamp(now - StartTime, 0f, duration);
                float fadeIn = Fades.FadeIn
                    ? Mathf.Max(0, Fades.FadeInFrames) / CombatTimeline.FramesPerSecond : 0f;
                float fadeOut = Fades.FadeOut
                    ? Mathf.Max(0, Fades.FadeOutFrames) / CombatTimeline.FramesPerSecond : 0f;
                if (fadeIn + fadeOut > duration)
                {
                    float scale = duration / (fadeIn + fadeOut);
                    fadeIn *= scale;
                    fadeOut *= scale;
                }

                float strength = 1f;
                if (fadeIn > 0f && elapsed < fadeIn)
                {
                    float progress = elapsed / fadeIn;
                    strength = Fades.FadeInCurve != null && Fades.FadeInCurve.length > 0
                        ? Mathf.Clamp01(Fades.FadeInCurve.Evaluate(progress)) : progress;
                }
                if (fadeOut > 0f && elapsed >= duration - fadeOut)
                {
                    float progress = (elapsed - (duration - fadeOut)) / fadeOut;
                    float outgoing = Fades.FadeOutCurve != null && Fades.FadeOutCurve.length > 0
                        ? Mathf.Clamp01(Fades.FadeOutCurve.Evaluate(progress)) : 1f - progress;
                    strength = Mathf.Min(strength, outgoing);
                }
                return Mathf.Lerp(1f, Speed, strength);
            }
        }

        private struct SavedSpeed
        {
            public float Base;
            public float Applied;
        }

        private static GlobalHitStopRuntime instance;
        private static int startSequence;
        public static event System.Action Started;
        private readonly List<Stop> stops = new List<Stop>();
        private readonly Dictionary<Animator, SavedSpeed> animators = new Dictionary<Animator, SavedSpeed>();
        private readonly Dictionary<ParticleSystem, SavedSpeed> particles = new Dictionary<ParticleSystem, SavedSpeed>();

        public static float CurrentVisualSpeed
        {
            get
            {
                if (instance == null) return 1f;
                float speed = 1f;
                float now = Time.realtimeSinceStartup;
                foreach (Stop stop in instance.stops)
                    if (now < stop.EndTime) speed = Mathf.Min(speed, stop.EvaluateSpeed(now));
                return speed;
            }
        }

        public static int StartSequence => startSequence;
        public static bool IsActive => instance != null && instance.stops.Count > 0;

        public static float GetVisualSpeed(Component visual)
        {
            if (instance == null) return 1f;
            ParticleEffectLifetime lifetime = visual != null
                ? visual.GetComponentInParent<ParticleEffectLifetime>() : null;
            return lifetime != null && lifetime.IgnoreGlobalHitStop
                ? 1f : CurrentVisualSpeed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            startSequence = 0;
            Started = null;
        }

        public static void Play(int durationFrames, float speed, HitStopFadeSettings fades = default)
        {
            if (durationFrames <= 0) return;
            if (instance == null)
            {
                var host = new GameObject("Global Hit Stop Runtime");
                instance = host.AddComponent<GlobalHitStopRuntime>();
            }

            float startTime = Time.realtimeSinceStartup;
            instance.stops.Add(new Stop
            {
                StartTime = startTime,
                EndTime = startTime + durationFrames / CombatTimeline.FramesPerSecond,
                Speed = Mathf.Clamp01(speed),
                Fades = fades
            });
            startSequence++;
            Started?.Invoke();
            instance.ApplyVisualSpeed();
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = stops.Count - 1; i >= 0; i--)
            {
                if (now >= stops[i].EndTime) stops.RemoveAt(i);
            }

            if (stops.Count == 0)
            {
                Restore();
                instance = null;
                Destroy(gameObject);
                return;
            }

            ApplyVisualSpeed();
        }

        // Also catches visual objects spawned after Update without waiting for another frame.
        private void LateUpdate() => ApplyVisualSpeed();

        private void ApplyVisualSpeed()
        {
            if (stops.Count == 0) return;
            float speed = CurrentVisualSpeed;
            ApplyAnimators(speed);
            ApplyParticles(speed);
            foreach (ParticleEffectLifetime lifetime in FindObjectsOfType<ParticleEffectLifetime>(true))
                if (lifetime != null && lifetime.isActiveAndEnabled)
                    lifetime.ApplyHitStopSpeed();
        }

        private void ApplyAnimators(float speed)
        {
            foreach (Animator animator in FindObjectsOfType<Animator>(true))
            {
                if (!animator.isActiveAndEnabled) continue;
                CombatController combat = animator.GetComponentInParent<CombatController>();
                if (combat != null && combat._animator == animator &&
                    combat._animSpeedExecutor != null)
                {
                    animator.speed = combat._animSpeedExecutor.GetCurrentSpeedModifier() * speed;
                    continue;
                }
                float current = animator.speed;
                if (!animators.TryGetValue(animator, out SavedSpeed saved))
                    saved.Base = current;
                else if (!Mathf.Approximately(current, saved.Applied))
                    saved.Base = current;

                saved.Applied = saved.Base * speed;
                animators[animator] = saved;
                animator.speed = saved.Applied;
            }
        }

        private void ApplyParticles(float speed)
        {
            foreach (ParticleSystem particle in FindObjectsOfType<ParticleSystem>(true))
            {
                if (!particle.gameObject.activeInHierarchy) continue;
                // ParticleEffectLifetime owns these speeds and combines both modifiers itself.
                if (particle.GetComponentInParent<ParticleEffectLifetime>() != null) continue;
                var main = particle.main;
                float current = main.simulationSpeed;
                if (!particles.TryGetValue(particle, out SavedSpeed saved))
                    saved.Base = current;
                else if (!Mathf.Approximately(current, saved.Applied))
                    saved.Base = current;

                saved.Applied = saved.Base * speed;
                particles[particle] = saved;
                main.simulationSpeed = saved.Applied;
            }
        }

        private void Restore()
        {
            foreach (var entry in animators)
            {
                Animator animator = entry.Key;
                if (animator == null) continue;
                // Combat animation speed is owned by its executor and may have changed during the pause.
                CombatController combat = animator.GetComponentInParent<CombatController>();
                animator.speed = combat != null && combat._animator == animator &&
                    combat._animSpeedExecutor != null
                    ? combat._animSpeedExecutor.GetCurrentSpeedModifier()
                    : entry.Value.Base;
            }
            animators.Clear();

            foreach (CombatController combat in FindObjectsOfType<CombatController>(true))
                if (combat != null && combat._animator != null &&
                    combat._animSpeedExecutor != null)
                    combat._animator.speed = combat._animSpeedExecutor.GetCurrentSpeedModifier();

            foreach (var entry in particles)
            {
                ParticleSystem particle = entry.Key;
                if (particle == null) continue;
                var main = particle.main;
                main.simulationSpeed = entry.Value.Base;
            }
            particles.Clear();

            foreach (ParticleEffectLifetime lifetime in FindObjectsOfType<ParticleEffectLifetime>(true))
                if (lifetime != null) lifetime.ApplyHitStopSpeed();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Restore();
        }
    }
}
