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
            public float DirectionalCycles;
            public float SampleTime;
            public float NormalizedTime;
            public float IntensityScale;
            public Vector3 WorldForceDirection;
            public float DirectionalIntensityScale;
            public float DirectionalReturnProgress;
        }

        private sealed class TraumaLayer
        {
            public CameraShakeSettings Settings;
            public float Trauma;
            public float DecayPerSecond;
            public Vector2 WaveCycles;
            public float TimeSincePulse;
            public float PulseDuration;
            public bool UseUnscaledTime;
        }

        private sealed class DirectionalImpulse
        {
            public bool Returning;
            public float ReturnNormalizedTime = 1f;
            public CameraShakeSettings Settings;
            public float Cycles;
            public Vector3 WorldDirection;
            public float Duration;
            public float Elapsed;
            public float IntensityScale;
            public bool UseUnscaledTime;
        }

        private static readonly Dictionary<int, ContinuousSource> ContinuousSources =
            new Dictionary<int, ContinuousSource>();
        private static readonly Dictionary<CameraShakeChannel, TraumaLayer> TraumaLayers =
            new Dictionary<CameraShakeChannel, TraumaLayer>();
        private static readonly Dictionary<int, DirectionalImpulse> DirectionalImpulses =
            new Dictionary<int, DirectionalImpulse>();
        private static readonly Dictionary<CameraShakeChannel, CameraShakeSample> ChannelMix =
            new Dictionary<CameraShakeChannel, CameraShakeSample>();
        private static readonly List<CameraShakeChannel> ExpiredTraumaChannels =
            new List<CameraShakeChannel>();
        private static readonly List<int> ExpiredImpulseHandles = new List<int>();

        private static int nextHandle = 1;
        private static CameraShakeRunner runner;

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
            float directionalIntensityScale = 0f, bool resetPhase = false,
            float directionalReturnProgress = 0f)
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
                DirectionalReturnProgress = directionalReturnProgress,
                DirectionalCycles = resetPhase || sampleTime < previous.SampleTime
                    ? settings.EvaluateDirectionalCycles(sampleTime, normalizedTime)
                    : previous.DirectionalCycles + (sampleTime - previous.SampleTime) *
                      settings.EvaluateDirectionalFrequency((previous.NormalizedTime + normalizedTime) * 0.5f),
                SampleTime = Mathf.Max(0f, sampleTime),
                NormalizedTime = Mathf.Clamp01(normalizedTime),
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = worldForceDirection.sqrMagnitude > 0.000001f
                    ? worldForceDirection.normalized
                    : Vector3.zero,
                DirectionalIntensityScale = Mathf.Max(0f,
                    directionalIntensityScale)
            };
            RefreshEditorCamera();
        }

        public static void Remove(int handle, bool smoothReturn = false)
        {
            if (smoothReturn && Application.isPlaying && ContinuousSources.TryGetValue(handle, out var source)
                && source.Settings != null && source.Settings.EnableDirectionalImpulse)
            {
                EnsureRunner();
                DirectionalImpulses.Add(nextHandle++, new DirectionalImpulse
                {
                    Settings = source.Settings, Returning = true, Duration = 0f,
                    UseUnscaledTime = true,
                    Cycles = source.DirectionalCycles,
                    ReturnNormalizedTime = source.NormalizedTime,
                    WorldDirection = source.WorldForceDirection,
                    IntensityScale = source.DirectionalIntensityScale
                });
            }
            if (handle != 0 && ContinuousSources.Remove(handle))
                RefreshEditorCamera();
        }

        public static void ClearContinuousSources()
        {
            ContinuousSources.Clear();
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
            Vector3 worldForceDirection = default)
        {
            if (settings == null || intensityScale <= 0f)
                return;

            EnsureRunner();
            duration = Mathf.Max(0.01f, duration);
            AddTrauma(settings, duration, intensityScale, useUnscaledTime);

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
                UseUnscaledTime = useUnscaledTime
            });
        }

        public static CameraShakeSample EvaluateCurrent(Quaternion? cameraOrientation = null)
        {
            ChannelMix.Clear();

            foreach (ContinuousSource source in ContinuousSources.Values)
            {
                if (source.Settings == null)
                    continue;
                CameraShakeSample sample = source.Settings.EvaluateWithPhase(source.WaveCycles,
                    source.NormalizedTime, source.IntensityScale);
                CameraShakeSample directional = source.Settings.EvaluateDirectional(
                    source.DirectionalCycles, source.NormalizedTime, source.WorldForceDirection,
                    source.DirectionalIntensityScale * CameraShakeSettings.DirectionalReturnWeight(source.DirectionalReturnProgress),
                    cameraOrientation);
                AddToChannel(source.Settings.Channel, sample + directional);
            }

            foreach (KeyValuePair<CameraShakeChannel, TraumaLayer> pair in TraumaLayers)
            {
                TraumaLayer layer = pair.Value;
                if (layer.Settings == null || layer.Trauma <= 0f)
                    continue;

                float exponent = Mathf.Max(1f, layer.Settings.TraumaExponent);
                float intensity = Mathf.Pow(Mathf.Clamp01(layer.Trauma), exponent);
                float normalizedTime = Mathf.Clamp01(layer.TimeSincePulse /
                                                      Mathf.Max(0.01f,
                                                          layer.PulseDuration));
                AddToChannel(pair.Key, layer.Settings.EvaluateWithPhase(layer.WaveCycles,
                    normalizedTime, intensity));
            }

            CameraShakeSample result = default;
            foreach (CameraShakeSample channelSample in ChannelMix.Values)
                result += channelSample;

            foreach (DirectionalImpulse impulse in DirectionalImpulses.Values)
            {
                if (impulse.Settings == null)
                    continue;

                float normalizedTime = Mathf.Clamp01(impulse.Elapsed /
                                                      Mathf.Max(0.01f,
                                                          impulse.Duration));
                if (impulse.Returning)
                {
                    float weight = CameraShakeSettings.DirectionalReturnWeight(
                        (impulse.Elapsed - impulse.Duration) / Mathf.Max(0.01f, impulse.Settings.DirectionalReturnDuration));
                    result += impulse.Settings.EvaluateDirectional(impulse.Cycles,
                        impulse.ReturnNormalizedTime, impulse.WorldDirection, impulse.IntensityScale * weight,
                        cameraOrientation);
                }
                else
                    result += impulse.Settings.EvaluateDirectional(impulse.Cycles,
                        normalizedTime, impulse.WorldDirection, impulse.IntensityScale, cameraOrientation);
            }

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ContinuousSources.Clear();
            TraumaLayers.Clear();
            DirectionalImpulses.Clear();
            ChannelMix.Clear();
            ExpiredTraumaChannels.Clear();
            ExpiredImpulseHandles.Clear();
            nextHandle = 1;
            runner = null;
        }

        private static void AddTrauma(CameraShakeSettings settings, float duration,
            float intensityScale, bool useUnscaledTime)
        {
            float amount = Mathf.Clamp01(settings.TraumaPerPulse * intensityScale);
            if (amount <= 0f)
                return;

            float decayPerSecond = amount / duration;
            if (!TraumaLayers.TryGetValue(settings.Channel, out TraumaLayer layer))
            {
                TraumaLayers.Add(settings.Channel, new TraumaLayer
                {
                    Settings = settings,
                    Trauma = amount,
                    DecayPerSecond = decayPerSecond,
                    WaveCycles = Vector2.zero,
                    TimeSincePulse = 0f,
                    PulseDuration = duration,
                    UseUnscaledTime = useUnscaledTime
                });
                return;
            }

            layer.Settings = settings;
            layer.Trauma = Mathf.Clamp01(layer.Trauma + amount);
            layer.DecayPerSecond = layer.DecayPerSecond > 0f
                ? Mathf.Min(layer.DecayPerSecond, decayPerSecond)
                : decayPerSecond;
            layer.WaveCycles = Vector2.zero;
            layer.TimeSincePulse = 0f;
            layer.PulseDuration = Mathf.Max(layer.PulseDuration, duration);
            layer.UseUnscaledTime = useUnscaledTime;
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
            ExpiredTraumaChannels.Clear();
            foreach (KeyValuePair<CameraShakeChannel, TraumaLayer> pair in TraumaLayers)
            {
                TraumaLayer layer = pair.Value;
                float deltaTime = layer.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                float progress = Mathf.Clamp01(layer.TimeSincePulse / Mathf.Max(0.01f, layer.PulseDuration));
                layer.WaveCycles += deltaTime * layer.Settings.EvaluateFrequencies(progress);
                layer.TimeSincePulse += deltaTime;
                layer.Trauma = Mathf.Max(0f,
                    layer.Trauma - layer.DecayPerSecond * deltaTime);
                if (layer.Trauma <= 0f)
                    ExpiredTraumaChannels.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredTraumaChannels.Count; i++)
                TraumaLayers.Remove(ExpiredTraumaChannels[i]);

            ExpiredImpulseHandles.Clear();
            foreach (KeyValuePair<int, DirectionalImpulse> pair in DirectionalImpulses)
            {
                DirectionalImpulse impulse = pair.Value;
                float deltaTime = impulse.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                if (!impulse.Returning)
                {
                    float oscillationStep = Mathf.Min(deltaTime, Mathf.Max(0f, impulse.Duration - impulse.Elapsed));
                    float midpoint = (impulse.Elapsed + oscillationStep * 0.5f) / impulse.Duration;
                    impulse.Cycles += oscillationStep * impulse.Settings.EvaluateDirectionalFrequency(midpoint);
                    if (impulse.Elapsed + deltaTime >= impulse.Duration)
                    {
                        impulse.Returning = true;
                    }
                }
                impulse.Elapsed += deltaTime;
                if (impulse.Elapsed >= impulse.Duration + Mathf.Max(0.01f, impulse.Settings.DirectionalReturnDuration))
                    ExpiredImpulseHandles.Add(pair.Key);
            }
            for (int i = 0; i < ExpiredImpulseHandles.Count; i++)
                DirectionalImpulses.Remove(ExpiredImpulseHandles[i]);
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
