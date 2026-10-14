using System;
using Cinemachine;
using UnityEngine;

namespace CombatCamera.Editor
{
    // Fixed-step replay gives the same choice and damping when scrubbing backwards.
    public sealed class CameraShotPreviewRig : IDisposable
    {
        private readonly CameraShotOrbitSolver basis = new CameraShotOrbitSolver();
        private readonly CameraShotOrbitSolver probe = new CameraShotOrbitSolver();
        private readonly CameraShotOrbitSolver output = new CameraShotOrbitSolver();
        private CameraShotSession session;
        private CinemachineFreeLook source;
        private Vector2 gameAxes;
        private int frame = -1;
        private float lastSampledTime = float.NaN;
        private bool entered, handedOff;
        public const float Step = 1f / 60f;
        public CameraShotPose Output { get; private set; }
        public Vector2 Axes { get; private set; }
        public int SelectedCandidate => session != null ? session.SelectedCandidate : -1;

        public void Reset(CinemachineFreeLook rig)
        {
            basis.Dispose(); probe.Dispose(); output.Dispose();
            source = rig; frame = -1; lastSampledTime = float.NaN; entered = handedOff = false;
            session = new CameraShotSession();
            gameAxes = rig != null ? new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value) : Vector2.zero;
        }

        public CameraShotPose Evaluate(CameraShotProfile profile, CameraShotTiming timing,
            CinemachineFreeLook rig, Transform actor, float time, float start, float duration,
            Action<float> sampleActor = null, bool returnToOriginalCamera = true)
        {
            int targetFrame = Mathf.Max(0, Mathf.FloorToInt((time + .00001f) / Step));
            if (source != rig || frame > targetFrame || session == null) Reset(rig);
            if (rig == null || rig.Follow == null || actor == null) return default;
            bool advanced = frame < targetFrame;
            float oldTime = CinemachineCore.CurrentTimeOverride, oldDelta = CinemachineCore.UniformDeltaTimeOverride;
            try {
                while (frame < targetFrame) {
                    frame++;
                    float seconds = frame * Step;
                    sampleActor?.Invoke(seconds);
                    CinemachineCore.CurrentTimeOverride = seconds;
                    CinemachineCore.UniformDeltaTimeOverride = Step;
                    if (!returnToOriginalCamera && entered && !handedOff && seconds >= start + duration) {
                        gameAxes = Axes; handedOff = true;
                    }
                    var basePose = basis.Sample(rig, gameAxes, frame == 0 ? -1 : Step);
                    Axes = gameAxes; Output = basePose;
                    if (handedOff) { Output = output.Sample(rig, gameAxes, Step); continue; }
                    if (seconds < start || seconds >= start + duration || profile == null) continue;
                    if (!entered) {
                        entered = session.Capture(profile, actor, rig, gameAxes, basePose.Position, probe);
                        output.Seed(rig, basePose);
                    }
                    if (!entered) continue;
                    Axes = session.Evaluate(rig, probe, gameAxes, seconds - start, duration, timing, returnToOriginalCamera);
                    Output = output.Sample(rig, Axes, Step);
                }
                return Output;
            }
            finally {
                CinemachineCore.CurrentTimeOverride = oldTime;
                CinemachineCore.UniformDeltaTimeOverride = oldDelta;
                if (advanced || !Mathf.Approximately(time, lastSampledTime))
                    sampleActor?.Invoke(time);
                lastSampledTime = time;
            }
        }

        public void Dispose() { basis.Dispose(); probe.Dispose(); output.Dispose(); source = null; session = null; frame = -1; lastSampledTime = float.NaN; }
    }
}
