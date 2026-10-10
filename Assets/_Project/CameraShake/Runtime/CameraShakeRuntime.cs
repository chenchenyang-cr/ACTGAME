using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    public static class CameraShakeRuntime
    {
        private struct ContinuousSource
        {
            public CameraShakeSettings Settings;
            public Vector2 WaveCycles;
            public float SampleTime;
            public float NormalizedTime;
            public float IntensityScale;
            public Vector3 WorldForceDirection;
            public float DirectionalIntensityScale;
        }

        private sealed class HitPulse
        {
            public CameraShakeSettings Settings;
            public float IntensityScale;
            public float DecayPerSecond;
            public Vector2 WaveCycles;
            public float TimeSincePulse;
            public float PulseDuration;
            public bool UseUnscaledTime;
            public System.Func<float> TimeScale;
        }

        private sealed class PendingHitPulse
        {
            public CameraShakeSettings Settings;
            public float Duration;
            public float IntensityScale;
            public bool UseUnscaledTime;
            public Vector3 WorldForceDirection;
            public System.Func<bool> IsHitStopActive;
            public System.Func<float> TimeScale;
        }

        private sealed class DirectionalImpulse
        {
            public CameraShakeSettings Settings;
            public Vector3 WorldDirection;
            public float Duration;
            public float Elapsed;
            public float IntensityScale;
            public bool UseUnscaledTime;
            public System.Func<float> TimeScale;
        }

        private sealed class DirectionalBlend
        {
            public bool FadingOut;
            public double FadeStartTime;

            public float WeightAt(double now)
            {
                return FadingOut
                    ? Mathf.Clamp01(1f - (float)((now - FadeStartTime) /
                                            DirectionalHandoffDuration))
                    : 1f;
            }

            public void BeginFade(double now)
            {
                if (FadingOut)
                    return;
                FadingOut = true;
                FadeStartTime = now;
            }
        }

        private sealed class TimedContinuousSource
        {
            public float Duration;
            public float Elapsed;
            public System.Func<float> TimeScale;
        }

        private static readonly Dictionary<int, ContinuousSource> ContinuousSources =
            new Dictionary<int, ContinuousSource>();
        private static readonly Dictionary<int, HitPulse> HitPulses =
            new Dictionary<int, HitPulse>();
        private static readonly Dictionary<int, PendingHitPulse> PendingHitPulses =
            new Dictionary<int, PendingHitPulse>();
        private static readonly Dictionary<int, DirectionalImpulse> DirectionalImpulses =
            new Dictionary<int, DirectionalImpulse>();
        private static readonly Dictionary<int, DirectionalBlend> DirectionalBlends =
            new Dictionary<int, DirectionalBlend>();
        private static readonly Dictionary<int, TimedContinuousSource> TimedContinuousSources =
            new Dictionary<int, TimedContinuousSource>();
        private static readonly Dictionary<CameraShakeChannel, CameraShakeSample> ChannelMix =
            new Dictionary<CameraShakeChannel, CameraShakeSample>();
        private static readonly List<int> ExpiredPulseHandles =
            new List<int>();
        private static readonly List<int> ReadyHitPulseTargets = new List<int>();
        private static readonly List<int> ExpiredImpulseHandles = new List<int>();
        private static readonly List<int> ExpiredContinuousHandles = new List<int>();

        private static int nextHandle = 1;
        private static CameraShakeRunner runner;
        private const float DirectionalHandoffDuration = 0.08f;

        public static int Add(CameraShakeSettings settings, float intensityScale = 1f)
        {
            if (settings == null)
                return 0;

            int handle = nextHandle++;
            ContinuousSources.Add(handle, new ContinuousSource
            {
                Settings = settings,
                SampleTime = 0f,
                NormalizedTime = 0f,
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = Vector3.zero,
                DirectionalIntensityScale = 0f
            });
            return handle;
        }

        public static void Update(int handle, CameraShakeSettings settings, float sampleTime,
            float normalizedTime, float intensityScale = 1f,
            Vector3 worldForceDirection = default,
            float directionalIntensityScale = 0f, bool resetPhase = false)
        {
            if (handle == 0 || settings == null ||
                !ContinuousSources.TryGetValue(handle, out ContinuousSource previous))
                return;

            sampleTime = Mathf.Max(0f, sampleTime);
            normalizedTime = Mathf.Clamp01(normalizedTime);
            Vector2 frequencies = settings.EvaluateFrequencies(normalizedTime);
            // Only elapsed clock time advances the wave. Timeline progress selects Hz.
            Vector2 cycles = resetPhase || sampleTime < previous.SampleTime
                ? sampleTime * frequencies
                : previous.WaveCycles + (sampleTime - previous.SampleTime) * frequencies;
            ContinuousSources[handle] = new ContinuousSource
            {
                Settings = settings,
                WaveCycles = cycles,
                SampleTime = Mathf.Max(0f, sampleTime),
                NormalizedTime = Mathf.Clamp01(normalizedTime),
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = worldForceDirection.sqrMagnitude > 0.000001f
                    ? worldForceDirection.normalized
                    : Vector3.zero,
                DirectionalIntensityScale = Mathf.Max(0f,
                    directionalIntensityScale)
            };
            if (settings.EnableDirectionalImpulse && directionalIntensityScale > 0f)
                ActivateDirectional(handle);
            RefreshEditorCamera();
        }

        public static void Remove(int handle)
        {
            TimedContinuousSources.Remove(handle);
            DirectionalBlends.Remove(handle);
            if (handle != 0 && ContinuousSources.Remove(handle))
                RefreshEditorCamera();
        }

        public static void ContinueTimed(int handle, float duration, float elapsed,
            System.Func<float> timeScale)
        {
            if (handle == 0 || !ContinuousSources.ContainsKey(handle))
                return;

            EnsureRunner();
            TimedContinuousSources[handle] = new TimedContinuousSource
            {
                Duration = Mathf.Max(0.01f, duration),
                Elapsed = Mathf.Max(0f, elapsed),
                TimeScale = timeScale
            };
        }

        public static void ClearContinuousSources()
        {
            foreach (int handle in ContinuousSources.Keys)
                DirectionalBlends.Remove(handle);
            ContinuousSources.Clear();
            TimedContinuousSources.Clear();
            RefreshEditorCamera();
        }

        private static void RefreshEditorCamera()
        {
#if UNITY_EDITOR
            // Repainting alone does not run Cinemachine's edit-mode LateUpdate.
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        public static void Pulse(CameraShakeSettings settings, float duration,
            float intensityScale = 1f, bool useUnscaledTime = true,
            Vector3 worldForceDirection = default, System.Func<float> timeScale = null)
        {
            if (settings == null || intensityScale <= 0f)
                return;

            EnsureRunner();
            duration = Mathf.Max(0.01f, duration);
            AddHitPulse(settings, duration, intensityScale, useUnscaledTime, timeScale);

            if (!settings.EnableDirectionalImpulse)
                return;

            int handle = nextHandle++;
            DirectionalImpulses.Add(handle, new DirectionalImpulse
            {
                Settings = settings,
                WorldDirection = worldForceDirection.normalized,
                Duration = duration,
                Elapsed = 0f,
                IntensityScale = Mathf.Max(0f, intensityScale),
                UseUnscaledTime = useUnscaledTime,
                TimeScale = timeScale
            });
            ActivateDirectional(handle);
        }

        public static void PulseAfterHitStop(int targetId, CameraShakeSettings settings,
            float duration, float intensityScale, bool useUnscaledTime,
            Vector3 worldForceDirection, System.Func<bool> isHitStopActive,
            System.Func<float> timeScale = null)
        {
            if (settings == null || intensityScale <= 0f)
                return;

            // A later hit replaces the pending shake for this target, including when
            // the later hit has no hit stop and can play immediately.
            PendingHitPulses.Remove(targetId);
            if (isHitStopActive == null || !isHitStopActive())
            {
                Pulse(settings, duration, intensityScale, useUnscaledTime,
                    worldForceDirection, timeScale);
                return;
            }

            PendingHitPulses[targetId] = new PendingHitPulse
            {
                Settings = settings,
                Duration = duration,
                IntensityScale = intensityScale,
                UseUnscaledTime = useUnscaledTime,
                WorldForceDirection = worldForceDirection,
                IsHitStopActive = isHitStopActive,
                TimeScale = timeScale
            };
            EnsureRunner();
        }

        public static CameraShakeSample EvaluateCurrent()
        {
            ChannelMix.Clear();

            foreach (KeyValuePair<int, ContinuousSource> pair in ContinuousSources)
            {
                ContinuousSource source = pair.Value;
                if (source.Settings == null)
                    continue;
                CameraShakeSample sample = source.Settings.EvaluateWithPhase(source.WaveCycles,
                    source.NormalizedTime, source.IntensityScale);
                float directionalWeight = DirectionalBlends.TryGetValue(pair.Key,
                    out DirectionalBlend blend)
                    ? blend.WeightAt(Time.realtimeSinceStartupAsDouble) : 1f;
                CameraShakeSample directional = source.Settings.EvaluateDirectional(
                    source.SampleTime, source.NormalizedTime, source.WorldForceDirection,
                    source.DirectionalIntensityScale * directionalWeight);
                AddToChannel(source.Settings.Channel, sample + directional);
            }

            foreach (HitPulse pulse in HitPulses.Values)
            {
                if (pulse.Settings == null)
                    continue;

                float progress = Mathf.Clamp01(pulse.TimeSincePulse / pulse.PulseDuration);
                float intensity = pulse.Settings.EvaluateHitIntensity(progress, pulse.IntensityScale);
                AddToChannel(pulse.Settings.Channel, pulse.Settings.EvaluateWithPhase(
                    pulse.WaveCycles, progress, intensity));
            }
            CameraShakeSample result = default;
            foreach (CameraShakeSample channelSample in ChannelMix.Values)
                result += channelSample;

            foreach (KeyValuePair<int, DirectionalImpulse> pair in DirectionalImpulses)
            {
                DirectionalImpulse impulse = pair.Value;
                if (impulse.Settings == null)
                    continue;
                float directionalWeight = DirectionalBlends.TryGetValue(pair.Key,
                    out DirectionalBlend blend)
                    ? blend.WeightAt(Time.realtimeSinceStartupAsDouble) : 1f;

                result += impulse.Settings.EvaluateDirectional(impulse.Elapsed,
                    impulse.Elapsed / impulse.Duration,
                    impulse.WorldDirection, impulse.IntensityScale * directionalWeight);
            }

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ContinuousSources.Clear();
            HitPulses.Clear();
            PendingHitPulses.Clear();
            DirectionalImpulses.Clear();
            DirectionalBlends.Clear();
            TimedContinuousSources.Clear();
            ChannelMix.Clear();
            ExpiredPulseHandles.Clear();
            ReadyHitPulseTargets.Clear();
            ExpiredImpulseHandles.Clear();
            ExpiredContinuousHandles.Clear();
            nextHandle = 1;
            runner = null;
        }

        private static void AddHitPulse(CameraShakeSettings settings, float duration,
            float intensityScale, bool useUnscaledTime, System.Func<float> timeScale)
        {
            if (settings.TraumaPerPulse <= 0f)
                return;

            // Each hit owns its clock and wave phase; later hits never restart it.
            HitPulses.Add(nextHandle++, new HitPulse
            {
                Settings = settings,
                IntensityScale = intensityScale,
                WaveCycles = Vector2.zero,
                TimeSincePulse = 0f,
                PulseDuration = duration,
                UseUnscaledTime = useUnscaledTime,
                TimeScale = timeScale
            });
        }

        private static void ActivateDirectional(int handle)
        {
            if (DirectionalBlends.ContainsKey(handle))
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            foreach (DirectionalBlend blend in DirectionalBlends.Values)
                blend.BeginFade(now);
            DirectionalBlends.Add(handle, new DirectionalBlend());
        }

        private static void AddToChannel(CameraShakeChannel channel,
            CameraShakeSample sample)
        {
            if (ChannelMix.TryGetValue(channel, out CameraShakeSample current))
                ChannelMix[channel] = current + sample;
            else
                ChannelMix.Add(channel, sample);
        }

        private static void Advance(float scaledDeltaTime, float unscaledDeltaTime)
        {
            ExpiredContinuousHandles.Clear();
            foreach (KeyValuePair<int, TimedContinuousSource> pair in TimedContinuousSources)
            {
                TimedContinuousSource timed = pair.Value;
                if (!ContinuousSources.TryGetValue(pair.Key, out ContinuousSource source) ||
                    source.Settings == null)
                {
                    ExpiredContinuousHandles.Add(pair.Key);
                    continue;
                }

                float timeScale = timed.TimeScale != null
                    ? Mathf.Max(0f, timed.TimeScale()) : 1f;
                timed.Elapsed += unscaledDeltaTime * timeScale;
                if (timed.Elapsed >= timed.Duration)
                {
                    ExpiredContinuousHandles.Add(pair.Key);
                    continue;
                }

                Update(pair.Key, source.Settings, timed.Elapsed,
                    timed.Elapsed / timed.Duration, source.IntensityScale,
                    source.WorldForceDirection, source.DirectionalIntensityScale);
            }
            for (int i = 0; i < ExpiredContinuousHandles.Count; i++)
                Remove(ExpiredContinuousHandles[i]);

            ExpiredPulseHandles.Clear();
            foreach (KeyValuePair<int, HitPulse> pair in HitPulses)
            {
                HitPulse pulse = pair.Value;
                if (pulse.Settings == null)
                {
                    ExpiredPulseHandles.Add(pair.Key);
                    continue;
                }
                float deltaTime = pulse.UseUnscaledTime ? unscaledDeltaTime : scaledDeltaTime;
                if (pulse.TimeScale != null)
                    deltaTime *= Mathf.Max(0f, pulse.TimeScale());
                float progress = Mathf.Clamp01(pulse.TimeSincePulse / pulse.PulseDuration);
                pulse.WaveCycles += deltaTime * pulse.Settings.EvaluateFrequencies(progress);
                pulse.TimeSincePulse += deltaTime;
                if (pulse.TimeSincePulse >= pulse.PulseDuration)
                    ExpiredPulseHandles.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredPulseHandles.Count; i++)
                HitPulses.Remove(ExpiredPulseHandles[i]);
            ExpiredImpulseHandles.Clear();
            foreach (KeyValuePair<int, DirectionalImpulse> pair in DirectionalImpulses)
            {
                DirectionalImpulse impulse = pair.Value;
                float deltaTime = impulse.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                if (impulse.TimeScale != null)
                    deltaTime *= Mathf.Max(0f, impulse.TimeScale());
                impulse.Elapsed += deltaTime;
                if (impulse.Settings == null || impulse.Elapsed >= impulse.Duration)
                    ExpiredImpulseHandles.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredImpulseHandles.Count; i++)
            {
                DirectionalBlends.Remove(ExpiredImpulseHandles[i]);
                DirectionalImpulses.Remove(ExpiredImpulseHandles[i]);
            }

            // Start ready shakes after advancing existing sources so their first
            // sample remains at time zero, even after a long frame.
            ReadyHitPulseTargets.Clear();
            foreach (KeyValuePair<int, PendingHitPulse> pair in PendingHitPulses)
            {
                if (pair.Value.IsHitStopActive == null || !pair.Value.IsHitStopActive())
                    ReadyHitPulseTargets.Add(pair.Key);
            }
            for (int i = 0; i < ReadyHitPulseTargets.Count; i++)
            {
                int targetId = ReadyHitPulseTargets[i];
                PendingHitPulse pending = PendingHitPulses[targetId];
                PendingHitPulses.Remove(targetId);
                Pulse(pending.Settings, pending.Duration, pending.IntensityScale,
                    pending.UseUnscaledTime, pending.WorldForceDirection, pending.TimeScale);
            }
        }

        private static CameraShakeRunner EnsureRunner()
        {
            if (runner != null)
                return runner;

            var gameObject = new GameObject("[Camera Shake Runtime]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(gameObject);
            runner = gameObject.AddComponent<CameraShakeRunner>();
            return runner;
        }

        // Cinemachine normally evaluates virtual cameras during LateUpdate.  Advance
        // pulse time after that evaluation so a short hit pulse is guaranteed to be
        // sampled at least once before it can expire (notably after a slow frame).
        [DefaultExecutionOrder(10000)]
        private sealed class CameraShakeRunner : MonoBehaviour
        {
            private void LateUpdate()
            {
                Advance(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }
    }
}
