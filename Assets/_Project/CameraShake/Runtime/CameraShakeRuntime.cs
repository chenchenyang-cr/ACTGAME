using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    public static class CameraShakeRuntime
    {
        private struct ContinuousSource
        {
            public CameraShakeSettings Settings;
            public float SampleTime;
            public float NormalizedTime;
            public float IntensityScale;
            public Vector3 WorldForceDirection;
            public float DirectionalIntensityScale;
        }

        private sealed class TraumaLayer
        {
            public CameraShakeSettings Settings;
            public float Trauma;
            public float DecayPerSecond;
            public float SampleTime;
            public float TimeSincePulse;
            public float PulseDuration;
            public bool UseUnscaledTime;
        }

        private sealed class DirectionalImpulse
        {
            public CameraShakeSettings Settings;
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
            float directionalIntensityScale = 0f)
        {
            if (handle == 0 || settings == null ||
                !ContinuousSources.ContainsKey(handle))
                return;

            ContinuousSources[handle] = new ContinuousSource
            {
                Settings = settings,
                SampleTime = Mathf.Max(0f, sampleTime),
                NormalizedTime = Mathf.Clamp01(normalizedTime),
                IntensityScale = Mathf.Max(0f, intensityScale),
                WorldForceDirection = worldForceDirection.sqrMagnitude > 0.000001f
                    ? worldForceDirection.normalized
                    : Vector3.zero,
                DirectionalIntensityScale = Mathf.Max(0f,
                    directionalIntensityScale)
            };
        }

        public static void Remove(int handle)
        {
            if (handle != 0)
                ContinuousSources.Remove(handle);
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

            if (!settings.EnableDirectionalImpulse ||
                worldForceDirection.sqrMagnitude <= 0.000001f)
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

        public static CameraShakeSample EvaluateCurrent()
        {
            ChannelMix.Clear();

            foreach (ContinuousSource source in ContinuousSources.Values)
            {
                if (source.Settings == null)
                    continue;
                CameraShakeSample sample = source.Settings.Evaluate(source.SampleTime,
                    source.NormalizedTime, source.IntensityScale);
                Vector3 worldOffset = EvaluateDirectionalOffset(source.Settings,
                    source.WorldForceDirection, source.NormalizedTime,
                    source.DirectionalIntensityScale);
                AddToChannel(source.Settings.Channel, sample +
                    new CameraShakeSample(Vector3.zero, Vector3.zero, 0f,
                        worldOffset));
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
                AddToChannel(pair.Key, layer.Settings.Evaluate(layer.SampleTime,
                    normalizedTime, intensity));
            }

            CameraShakeSample result = default;
            foreach (CameraShakeSample channelSample in ChannelMix.Values)
                result += channelSample;

            Vector3 worldImpulse = Vector3.zero;
            foreach (DirectionalImpulse impulse in DirectionalImpulses.Values)
            {
                if (impulse.Settings == null)
                    continue;

                float normalizedTime = Mathf.Clamp01(impulse.Elapsed /
                                                      Mathf.Max(0.01f,
                                                          impulse.Duration));
                worldImpulse += EvaluateDirectionalOffset(impulse.Settings,
                    impulse.WorldDirection, normalizedTime,
                    impulse.IntensityScale);
            }

            return result + new CameraShakeSample(Vector3.zero, Vector3.zero, 0f,
                worldImpulse);
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
                    SampleTime = 0f,
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
            layer.SampleTime = 0f;
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

        private static Vector3 EvaluateDirectionalOffset(CameraShakeSettings settings,
            Vector3 worldDirection, float normalizedTime, float intensityScale)
        {
            if (settings == null || !settings.EnableDirectionalImpulse ||
                worldDirection.sqrMagnitude <= 0.000001f || intensityScale <= 0f)
                return Vector3.zero;

            float curve = settings.DirectionalImpulseCurve != null
                ? settings.DirectionalImpulseCurve.Evaluate(Mathf.Clamp01(normalizedTime))
                : 1f - Mathf.Clamp01(normalizedTime);
            return worldDirection.normalized * settings.DirectionalPositionAmplitude *
                   curve * intensityScale;
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
                layer.SampleTime += deltaTime;
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
                impulse.Elapsed += impulse.UseUnscaledTime
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                if (impulse.Elapsed >= impulse.Duration)
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
