using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cinemachine;
using UnityEngine;

namespace CombatCamera
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/Combat Camera Shot Extension")]
    public sealed class CinemachineCameraShotExtension : CinemachineExtension
    {
        public int Channel;
        public CameraShotPose BasePose { get; private set; }
        public bool HasBasePose { get; private set; }
        public Vector2 CurrentAxes { get; private set; }
        public CameraShotPose CurrentPose { get; private set; }
        public int SelectedCandidate { get; private set; } = -1;
        private bool active, inputLocked;
        public bool IsApplyingHandoff { get; private set; }
        private CinemachineFreeLook rig;
        private AxisState savedX, savedY;
        private AxisState.Recentering savedYaw, savedPitch;
        private readonly CameraShotOrbitSolver probe = new CameraShotOrbitSolver();
        private readonly CameraShotOrbitSolver output = new CameraShotOrbitSolver();
        private static readonly List<CinemachineCameraShotExtension> live = new List<CinemachineCameraShotExtension>();
        public CinemachineFreeLook Rig => rig != null ? rig : rig = GetComponent<CinemachineFreeLook>();

        protected override void OnEnable() { base.OnEnable(); if (!live.Contains(this)) live.Add(this); }
        public static CinemachineCameraShotExtension FindActive(int channel)
        {
            CinemachineCameraShotExtension fallback = null;
            foreach (var extension in live) {
                if (extension == null || !extension.isActiveAndEnabled || extension.Channel != channel ||
                    extension.Rig == null || extension.Rig.Follow == null) continue;
                if (CinemachineCore.Instance.IsLive(extension.Rig)) return extension;
                if (fallback == null || extension.Rig.Priority > fallback.Rig.Priority) fallback = extension;
            }
            return fallback;
        }

        public bool Capture(CameraShotSession session, CameraShotProfile profile, Transform actor)
        {
            if (Rig == null || Rig.Follow == null) return false;
            var axes = active ? CurrentAxes : new Vector2(Rig.m_XAxis.Value, Rig.m_YAxis.Value);
            var brain = CinemachineCore.Instance.FindPotentialTargetBrain(Rig);
            var camera = brain != null ? brain.OutputCamera : null;
            var position = camera != null ? camera.transform.position : CameraShotPose.FromState(Rig.State).Position;
            return session.Capture(profile, actor, Rig, axes, position, probe);
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || vcam != VirtualCamera) return;
            var shake = GetComponent<CinemachineCameraShakeExtension>();
            if (shake == null || !shake.enabled) Apply(ref state);
        }

        public void Apply(ref CameraState state)
        {
            if (!isActiveAndEnabled || IsApplyingHandoff || Rig == null || Rig.Follow == null) return;
            if (Application.isPlaying && CameraShotRuntime.TryTakeHandoff(Channel, out var handoffAxes)) {
                var handoffPose = active ? CurrentPose : probe.Sample(Rig, handoffAxes);
                AdoptOrbit(handoffAxes, handoffPose);
                state = Rig.State;
                state.PositionCorrection += handoffPose.Position - state.CorrectedPosition;
                state.OrientationCorrection = Quaternion.Inverse(state.RawOrientation) * handoffPose.Rotation;
            }
            BasePose = CameraShotPose.FromState(state); HasBasePose = true;
            var gameAxes = new Vector2(Rig.m_XAxis.Value, Rig.m_YAxis.Value);
            bool wasActive = active;
            active = CameraShotRuntime.Evaluate(Channel, Rig, gameAxes,
                wasActive ? CurrentAxes : gameAxes, wasActive ? CurrentPose.Position : BasePose.Position,
                probe, out var axes, out bool lockInput, out int candidate, out var previewPose);
            SelectedCandidate = candidate;
            SetInputLocked(Application.isPlaying && active && lockInput);
            if (!active) { if (wasActive) output.Dispose(); return; }
            CurrentAxes = axes;
            float dt = Application.isPlaying ? Time.unscaledDeltaTime : -1;
            if (!wasActive) output.Seed(Rig, BasePose);
            CurrentPose = previewPose ?? output.Sample(Rig, axes, dt);
            state.PositionCorrection += CurrentPose.Position - state.CorrectedPosition;
            state.OrientationCorrection = Quaternion.Inverse(state.RawOrientation) * CurrentPose.Rotation;
        }

        private void SetInputLocked(bool locked)
        {
            if (!locked) { RestoreInput(); return; }
            if (!inputLocked) {
                savedX = Rig.m_XAxis; savedY = Rig.m_YAxis;
                savedYaw = Rig.m_RecenterToTargetHeading; savedPitch = Rig.m_YAxisRecentering;
                inputLocked = true;
            }
            Rig.m_XAxis.Value = savedX.Value; Rig.m_YAxis.Value = savedY.Value;
            Rig.m_XAxis.m_MaxSpeed = Rig.m_YAxis.m_MaxSpeed = 0;
            Rig.m_XAxis.Reset(); Rig.m_YAxis.Reset();
            Rig.m_XAxis.m_Recentering.m_enabled = Rig.m_YAxis.m_Recentering.m_enabled = false;
            Rig.m_RecenterToTargetHeading.m_enabled = Rig.m_YAxisRecentering.m_enabled = false;
        }
        // Seed the gameplay rig at the final output before restoring its input settings.
        // Only the orbit values change; existing speed, provider and recenter settings survive.
        public void AdoptOrbit(Vector2 axes, CameraShotPose pose)
        {
            if (Rig == null || Rig.Follow == null) return;
            axes = CameraShotOrbitSolver.ClampAxes(Rig, axes);
            IsApplyingHandoff = true;
            try {
                Rig.ForceCameraPosition(pose.Position, pose.Rotation);
                Rig.m_XAxis.Value = axes.x; Rig.m_YAxis.Value = axes.y;
                Rig.m_XAxis.Reset(); Rig.m_YAxis.Reset();
                float span = Rig.m_YAxis.m_MaxValue - Rig.m_YAxis.m_MinValue;
                var offset = Rig.GetLocalPositionForCameraFromInput(span > 0 ? axes.y / span : .5f);
                for (int i = 0; i < 3; i++) {
                    var body = Rig.GetRig(i).GetCinemachineComponent<CinemachineOrbitalTransposer>();
                    body.m_FollowOffset = offset; body.m_XAxis.Value = axes.x;
                }
                if (inputLocked) { savedX.Value = axes.x; savedY.Value = axes.y; }
                CurrentAxes = axes; CurrentPose = pose;
            }
            finally { IsApplyingHandoff = false; }
        }
        private void RestoreInput()
        {
            if (!inputLocked) return;
            if (Rig != null) {
                Rig.m_XAxis = savedX; Rig.m_YAxis = savedY;
                Rig.m_XAxis.Reset(); Rig.m_YAxis.Reset();
                Rig.m_XAxis.m_Recentering.CancelRecentering(); Rig.m_YAxis.m_Recentering.CancelRecentering();
                Rig.m_RecenterToTargetHeading = savedYaw; Rig.m_YAxisRecentering = savedPitch;
                Rig.m_RecenterToTargetHeading.CancelRecentering(); Rig.m_YAxisRecentering.CancelRecentering();
            }
            inputLocked = false;
        }
        private void LateUpdate() { if (inputLocked && (Rig == null || !Rig.isActiveAndEnabled)) RestoreInput(); }
        private void OnDisable() { live.Remove(this); RestoreInput(); probe.Dispose(); output.Dispose(); active = false; HasBasePose = false; }
        protected override void OnDestroy() { live.Remove(this); probe.Dispose(); output.Dispose(); RestoreInput(); base.OnDestroy(); }
    }

    // An inactive clone retains the original spline, body, aim, and collider extensions.
    // Its axes and update caches are private; sampling never changes the scene camera.
    public sealed class CameraShotOrbitSolver : IDisposable
    {
        private GameObject root;
        private CinemachineFreeLook source, copy;
        private int version;
        private static readonly FieldInfo updateStatus = typeof(CinemachineCore).GetField("mUpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo headingFrame = typeof(CinemachineFreeLook).GetField("m_LastHeadingUpdateFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        public CinemachineFreeLook Copy => copy;

        private bool Ensure(CinemachineFreeLook rig)
        {
            if (rig == null || rig.Follow == null) return false;
            int revision = 0;
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                revision = UnityEditor.EditorUtility.GetDirtyCount(rig);
                foreach (var component in rig.GetComponentsInChildren<Component>(true))
                    if (component != null) revision = revision * 31 + UnityEditor.EditorUtility.GetDirtyCount(component);
            }
#endif
            if (copy != null && source == rig && version == revision && copy.Follow == rig.Follow && copy.LookAt == rig.LookAt) return true;
            Dispose(); source = rig; version = revision;
            root = new GameObject("CameraShot orbit sample") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var instance = UnityEngine.Object.Instantiate(rig.gameObject, root.transform);
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true)) transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var component in instance.GetComponentsInChildren<Behaviour>(true))
                if (!(component is CinemachineVirtualCameraBase) && !(component is CinemachineComponentBase) && !(component is CinemachineExtension)) component.enabled = false;
            copy = instance.GetComponent<CinemachineFreeLook>();
            copy.Follow = rig.Follow; copy.LookAt = rig.LookAt;
            foreach (var camera in instance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                foreach (var extension in camera.GetComponents<CinemachineExtension>()) {
                    if (extension is CinemachineCameraShotExtension || extension is CinemachineCameraShakeExtension) extension.enabled = false;
                    camera.AddExtension(extension);
                }
            copy.m_XAxis.m_InputAxisName = copy.m_YAxis.m_InputAxisName = "";
            copy.m_XAxis.m_MaxSpeed = copy.m_YAxis.m_MaxSpeed = 0;
            copy.m_XAxis.SetInputAxisProvider(0, null); copy.m_YAxis.SetInputAxisProvider(1, null);
            copy.m_RecenterToTargetHeading.m_enabled = copy.m_YAxisRecentering.m_enabled = false;
            copy.m_XAxis.m_Recentering.m_enabled = copy.m_YAxis.m_Recentering.m_enabled = false;
            Sample(rig, new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value), -1);
            return true;
        }

        public void Seed(CinemachineFreeLook rig, CameraShotPose pose)
        {
            if (!Ensure(rig)) return;
            copy.ForceCameraPosition(pose.Position, pose.Rotation);
        }

        public CameraShotPose Sample(CinemachineFreeLook rig, Vector2 axes, float deltaTime = -1)
        {
            if (!Ensure(rig)) return default;
            axes = ClampAxes(rig, axes);
            copy.m_XAxis.Value = axes.x; copy.m_YAxis.Value = axes.y;
            copy.m_Lens = rig.m_Lens;
            float range = copy.m_YAxis.m_MaxValue - copy.m_YAxis.m_MinValue;
            Vector3 offset = copy.GetLocalPositionForCameraFromInput(range > 0 ? axes.y / range : .5f);
            // FreeLook normally pushes these values after its pipeline update. Set them
            // before the sample so the pose belongs to this frame's requested axes.
            for (int i = 0; i < 3; i++) {
                var child = copy.GetRig(i);
                var body = child.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                body.m_FollowOffset = offset; body.m_XAxis.Value = axes.x;
            }
            ClearSchedulingCache();
            copy.InternalUpdateCameraState(Vector3.up, deltaTime);
            return CameraShotPose.FromState(copy.State);
        }

        private void ClearSchedulingCache()
        {
            if (updateStatus == null || headingFrame == null)
                throw new InvalidOperationException("CameraShot 的手动轨道采样需要 Cinemachine 2.10 的更新缓存接口。");
            var updates = updateStatus.GetValue(CinemachineCore.Instance) as IDictionary;
            for (int i = 0; i < 3; i++) { var child = copy.GetRig(i); if (child != null) updates?.Remove(child); }
            headingFrame.SetValue(copy, -1f);
        }

        public bool TryCandidate(CinemachineFreeLook rig, Transform actor, CameraShotCandidate candidate,
            out Vector2 axes, out CameraShotPose pose)
        {
            axes = default; pose = default;
            if (candidate == null || actor == null || !Ensure(rig)) return false;
            float min = rig.m_YAxis.m_MinValue, max = rig.m_YAxis.m_MaxValue;
            float span = max - min;
            float height = actor.position.y + candidate.Height - rig.Follow.position.y;
            float bestY = min, best = float.PositiveInfinity;
            // Search the real spline, including custom/non-monotonic orbit heights.
            float Cost(float y) => Mathf.Abs(copy.GetLocalPositionForCameraFromInput(span > 0 ? y / span : .5f).y - height);
            for (int i = 0; i <= 64; i++) {
                float y = Mathf.Lerp(min, max, i / 64f), cost = Cost(y);
                if (cost < best) { best = cost; bestY = y; }
            }
            float low = Mathf.Max(min, bestY - span / 64), high = Mathf.Min(max, bestY + span / 64);
            for (int i = 0; i < 18; i++) {
                float a = Mathf.Lerp(low, high, 1f / 3), b = Mathf.Lerp(low, high, 2f / 3);
                if (Cost(a) <= Cost(b)) high = b; else low = a;
            }
            float refined = (low + high) * .5f;
            if (Cost(refined) < best) bestY = refined;
            var forward = Vector3.ProjectOnPlane(actor.forward, Vector3.up);
            var frame = Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : Vector3.forward, Vector3.up);
            var relative = candidate.RelativePosition.sqrMagnitude > .0001f ? candidate.RelativePosition : Vector2.down;
            Vector3 direction = frame * new Vector3(relative.x, 0, relative.y).normalized;
            Sample(rig, new Vector2(rig.m_XAxis.Value, bestY));
            var orbital = copy.GetRig(1).GetCinemachineComponent<CinemachineOrbitalTransposer>();
            float x = orbital.GetAxisClosestValue(rig.Follow.position + direction * 5, Vector3.up);
            axes = ClampAxes(rig, new Vector2(x, bestY));
            pose = Sample(rig, axes);
            return true;
        }

        public static Vector2 ClampAxes(CinemachineFreeLook rig, Vector2 axes)
        {
            float span = rig.m_XAxis.m_MaxValue - rig.m_XAxis.m_MinValue;
            axes.x = rig.m_XAxis.m_Wrap && span > 0 ? Mathf.Repeat(axes.x - rig.m_XAxis.m_MinValue, span) + rig.m_XAxis.m_MinValue
                : Mathf.Clamp(axes.x, rig.m_XAxis.m_MinValue, rig.m_XAxis.m_MaxValue);
            axes.y = Mathf.Clamp(axes.y, rig.m_YAxis.m_MinValue, rig.m_YAxis.m_MaxValue);
            return axes;
        }
        public static Vector2 BlendAxes(CinemachineFreeLook rig, Vector2 from, Vector2 to, float weight)
        {
            from = ClampAxes(rig, from); to = ClampAxes(rig, to);
            float delta = to.x - from.x;
            float span = rig.m_XAxis.m_MaxValue - rig.m_XAxis.m_MinValue;
            if (rig.m_XAxis.m_Wrap && span > 0) delta = Mathf.Repeat(delta + span * .5f, span) - span * .5f;
            return ClampAxes(rig, new Vector2(from.x + delta * Mathf.Clamp01(weight), Mathf.Lerp(from.y, to.y, Mathf.Clamp01(weight))));
        }

        public void Dispose()
        {
            if (copy != null && updateStatus != null) {
                var updates = updateStatus.GetValue(CinemachineCore.Instance) as IDictionary;
                updates?.Remove(copy);
                for (int i = 0; i < 3; i++) { var child = copy.GetRig(i); if (child != null) updates?.Remove(child); }
            }
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; source = copy = null;
        }
    }
}
