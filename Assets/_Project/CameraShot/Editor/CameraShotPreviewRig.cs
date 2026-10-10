using System;
using System.Collections;
using System.Reflection;
using Cinemachine;
using UnityEngine;

namespace CombatCamera.Editor
{
    // A manually clocked, inactive camera rig. It never joins the live Brain's queue.
    public sealed class CameraShotPreviewRig : IDisposable
    {
        private GameObject root;
        private CinemachineFreeLook rig;
        private CinemachineCameraShotExtension solver;
        private Transform follow, look;
        private CinemachineFreeLook original;
        private Vector3 followOffset, lookOffset;
        private readonly CameraShotRuntime.PreviewSession motion = new();
        private int frame = -1;
        private bool entered, returning, exiting;
        private CameraShotPose entry, returnFrom, exitFrom;
        private Vector3 entryActor, returnActor, exitActor;
        private float returnOrigin;
        public CameraShotPose Basis { get; private set; }
        public CameraShotPose Target { get; private set; }
        public CameraShotPose Output { get; private set; }
        public CinemachineFreeLook Rig => rig;
        public const float Step = 1f / 60f;
        // CM 2.10 caches child updates by Unity frame, even during manual evaluation.
        // A seek evaluates many timeline frames within ONE editor frame. Invalidate only
        // this inactive copy's scheduling entries, retaining its damping state.
        private static readonly FieldInfo updateStatus = typeof(CinemachineCore).GetField("mUpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo headingFrame = typeof(CinemachineFreeLook).GetField("m_LastHeadingUpdateFrame", BindingFlags.Instance | BindingFlags.NonPublic);

        public void Create(CinemachineFreeLook source, Transform actor)
        {
            Dispose(); original = source;
            root = new GameObject("CameraShot preview simulation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            if (source == null) return;
            var instance = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
            foreach (var component in instance.GetComponentsInChildren<Behaviour>(true))
                if (!(component is CinemachineVirtualCameraBase) && !(component is CinemachineComponentBase) && !(component is CinemachineExtension))
                    component.enabled = false;
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true)) transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rig = instance.GetComponent<CinemachineFreeLook>();
            foreach (var camera in instance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                foreach (var extension in camera.GetComponents<CinemachineExtension>())
                {
                    if (extension is CinemachineCameraShotExtension || extension is CinemachineCameraShakeExtension) extension.enabled = false;
                    camera.AddExtension(extension);
                }
            solver = rig.GetComponent<CinemachineCameraShotExtension>();
            if (solver == null) solver = rig.gameObject.AddComponent<CinemachineCameraShotExtension>();
            solver.enabled = false;
            follow = new GameObject("Follow").transform; follow.SetParent(root.transform);
            look = new GameObject("LookAt").transform; look.SetParent(root.transform);
            follow.gameObject.hideFlags = look.gameObject.hideFlags = HideFlags.HideAndDontSave;
            followOffset = source.Follow != null ? actor.InverseTransformPoint(source.Follow.position) : Vector3.zero;
            lookOffset = source.LookAt != null ? actor.InverseTransformPoint(source.LookAt.position) : Vector3.up * 1.2f;
            rig.Follow = follow; rig.LookAt = look;
            rig.m_XAxis.m_InputAxisName = rig.m_YAxis.m_InputAxisName = "";
            rig.m_XAxis.m_MaxSpeed = rig.m_YAxis.m_MaxSpeed = 0;
            rig.m_XAxis.SetInputAxisProvider(0, null); rig.m_YAxis.SetInputAxisProvider(1, null);
            rig.m_XAxis.Reset(); rig.m_YAxis.Reset();
        }

        public void Reset()
        {
            frame = -1; entered = returning = exiting = false; motion.Reset();
            if (rig == null || original == null) return;
            rig.m_XAxis.Value = original.m_XAxis.Value; rig.m_YAxis.Value = original.m_YAxis.Value;
            rig.PreviousStateIsValid = false;
        }

        private CameraShotPose UpdateBasis(Transform actor, CameraShotPose fallback, float dt)
        {
            if (rig == null) return fallback;
            follow.SetPositionAndRotation(actor.TransformPoint(followOffset), actor.rotation);
            look.SetPositionAndRotation(actor.TransformPoint(lookOffset), actor.rotation);
            if (updateStatus == null || headingFrame == null)
                throw new InvalidOperationException("当前 Cinemachine 版本不支持预览时钟，请检查 CameraShot 的版本适配。");
            var updates = updateStatus.GetValue(CinemachineCore.Instance) as IDictionary;
            for (int i = 0; i < 3; i++) { var child = rig.GetRig(i); if (child != null) updates?.Remove(child); }
            headingFrame.SetValue(rig, -1f);
            // Updating once with a negative delta synchronizes FreeLook's orbit settings.
            if (dt < 0) rig.InternalUpdateCameraState(Vector3.up, -1);
            rig.InternalUpdateCameraState(Vector3.up, dt);
            var state = rig.State;
            return new CameraShotPose { Position = state.CorrectedPosition, Rotation = state.CorrectedOrientation,
                FieldOfView = state.Lens.FieldOfView };
        }

        public CameraShotPose Evaluate(CameraShotProfile profile, CameraShotTiming timing, Transform actor,
            float time, float start, float duration, CameraShotPose fallback, Action<float> sampleActor = null)
        {
            int targetFrame = Mathf.Max(0, Mathf.FloorToInt((time + .00001f) / Step));
            if (targetFrame < frame) Reset();
            duration = Mathf.Max(.001f, duration);
            float oldTime = CinemachineCore.CurrentTimeOverride, oldDelta = CinemachineCore.UniformDeltaTimeOverride;
            try
            {
                while (frame < targetFrame)
                {
                    frame++;
                    float seconds = frame * Step;
                    CinemachineCore.CurrentTimeOverride = seconds;
                    CinemachineCore.UniformDeltaTimeOverride = Step;
                    sampleActor?.Invoke(seconds);
                    if (rig != null)
                    {
                        bool locked = profile.LockCameraInput && seconds >= start && seconds < start + duration + profile.BlendOut;
                        rig.m_RecenterToTargetHeading.m_enabled = !locked && original.m_RecenterToTargetHeading.m_enabled;
                        rig.m_YAxisRecentering.m_enabled = !locked && original.m_YAxisRecentering.m_enabled;
                    }
                    Basis = UpdateBasis(actor, fallback, frame == 0 ? -1 : Step);
                    if (seconds < start) { Output = Target = lastOutput = Basis; continue; }
                    if (!entered)
                    {
                        motion.CaptureStart(profile, actor, actor.position, actor.forward);
                        entry = Basis; entryActor = actor.position; entered = true;
                    }
                    float progress = Mathf.Clamp01((seconds - start) / duration);
                    bool Constraint(CameraShotPose desired, out CameraShotPose result)
                    {
                        result = desired;
                        if (solver == null) return false;
                        // The projection clone must not move the gameplay preview's axes.
                        return targetProjection.Evaluate(rig, desired, out result, true);
                    }
                    Target = motion.Evaluate(profile, actor, Basis.Rotation, Basis.FieldOfView, progress,
                        Basis.Position, Constraint, Step);
                    var movingEntry = entry; movingEntry.Position += actor.position - entryActor;
                    if (timing.Enabled)
                    {
                        Output = CameraShotRuntime.ComposeClip(profile, timing, movingEntry, Basis, Target, progress, actor.position);
                        Output = CameraShotRuntime.ConstrainFinalPose(profile, actor.position, Output);
                    }
                    else
                    {
                        Output = CameraShotRuntime.Blend(Basis, Target, profile.EvaluateWeight(progress),
                            actor.position + Vector3.up * 1.2f, profile.OrbitBlend && profile.Composition != CameraShotComposition.GameCameraRelative);
                        if (profile.TryGetReturnBlend(progress, out _, out float blend) && solver != null)
                        {
                            if (!returning)
                            {
                                returnFrom = frame > 0 ? lastOutput : Output; returnActor = actor.position;
                                returnOrigin = blend; solver.FindReturnState(returnFrom); returning = true;
                                Basis = UpdateBasis(actor, fallback, 0);
                            }
                            var from = returnFrom; from.Position += actor.position - returnActor;
                            Output = CameraShotRuntime.Blend(from, Basis, Mathf.InverseLerp(returnOrigin, 1, blend),
                                actor.position + Vector3.up * 1.2f, true);
                        }
                    }
                    if (seconds >= start + duration)
                    {
                        if (timing.Enabled && timing.Exit > 0) Output = Basis;
                        else
                        {
                            if (!exiting)
                            {
                                exitFrom = lastOutput; exitActor = actor.position; exiting = true;
                                if (!timing.Enabled && !returning && solver != null) solver.FindReturnState(exitFrom);
                                Basis = UpdateBasis(actor, fallback, 0);
                            }
                            var from = exitFrom; from.Position += actor.position - exitActor;
                            float blend = profile.BlendOut <= 0 ? 1 : Mathf.SmoothStep(0, 1, (seconds - start - duration) / profile.BlendOut);
                            Output = CameraShotRuntime.Blend(from, Basis, blend, actor.position + Vector3.up * 1.2f, profile.OrbitBlend);
                        }
                    }
                    lastOutput = Output;
                }
                return Output;
            }
            finally
            {
                CinemachineCore.CurrentTimeOverride = oldTime; CinemachineCore.UniformDeltaTimeOverride = oldDelta;
                sampleActor?.Invoke(time);
            }
        }
        private CameraShotPose lastOutput;
        private readonly CameraShotOrbitSolver targetProjection = new();

        public void Dispose()
        {
            targetProjection.Dispose();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; rig = null; original = null; frame = -1;
            entered = returning = exiting = false; motion.Reset();
        }
    }
}
