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
        public bool IsSolvingReturn { get; private set; }
        private bool hasOutput, exiting, exitUnscaled;
        private int exitHandle;
        private float exitDuration;
        private double exitStarted;
        private CameraShotPose lastOutput, exitFrom;
        private Vector3 exitFollowPosition;
        private bool inputLocked;
        private CinemachineFreeLook freeLook;
        private AxisState savedX, savedY;
        private AxisState.Recentering savedYawRecentering, savedPitchRecentering;
        private int timelineReturnHandle;
        private CameraShotPose timelineReturnFrom;
        private Vector3 timelineReturnFollow;
        private float timelineReturnStartBlend;
        private float lastReturnBlend;
        private readonly CameraShotOrbitSolver returnPreview = new();
        private readonly CameraShotOrbitSolver targetSolver = new();
        private CameraShotRuntime.TargetConstraint targetConstraint;

        private bool ConstrainTarget(CameraShotPose desired, out CameraShotPose constrained)
        {
            if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
            return targetSolver.Evaluate(freeLook, desired, out constrained, true);
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || vcam != VirtualCamera) return;
            // The shake extension invokes Apply first so composition always precedes shake,
            // independent of component order. Standalone shots work without that extension.
            var shake = GetComponent<CinemachineCameraShakeExtension>();
            if (shake == null || !shake.enabled) Apply(ref state);
        }

        public void Apply(ref CameraState state)
        {
            if (!isActiveAndEnabled || IsSolvingReturn) return;
            BasePose = new CameraShotPose { Position = state.CorrectedPosition,
                Rotation = state.CorrectedOrientation, FieldOfView = state.Lens.FieldOfView };
            HasBasePose = true;
            int previousReturnHandle = timelineReturnHandle;
            targetConstraint ??= ConstrainTarget;
            bool exitBeforeUpdate = CameraShotRuntime.TryGetExit(Channel, out int earlierHandle, out float earlierRemaining, out bool earlierUnscaled);
            bool active = CameraShotRuntime.Evaluate(Channel, state.CorrectedPosition,
                state.CorrectedOrientation, state.Lens.FieldOfView, out CameraShotPose pose, out bool lockInput, targetConstraint);
            if (CameraShotRuntime.TryGetTimelineReturn(Channel, BasePose,
                out int timelineHandle, out var timelineFrom, out float returnBlend, targetConstraint))
            {
                if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
                if (freeLook != null && freeLook.Follow != null)
                {
                    if (!Application.isPlaying)
                    {
                        if (returnPreview.Evaluate(freeLook, timelineFrom, out var destination))
                            pose = CameraShotRuntime.Blend(timelineFrom, destination, returnBlend, freeLook.Follow.position + Vector3.up * 1.2f, true);
                    }
                    else
                    {
                        if (timelineReturnHandle != timelineHandle)
                        {
                            timelineReturnFrom = hasOutput ? lastOutput : pose;
                            timelineReturnFollow = freeLook.Follow.position;
                            timelineReturnStartBlend = returnBlend;
                            state = FindReturnState(timelineReturnFrom);
                            timelineReturnHandle = timelineHandle;
                        }
                        Vector3 followDelta = freeLook.Follow.position - timelineReturnFollow;
                        returnBlend = timelineReturnStartBlend >= .9999f ? 1f : Mathf.InverseLerp(timelineReturnStartBlend, 1f, returnBlend);
                        var from = timelineReturnFrom; from.Position += followDelta;
                        pose = CameraShotRuntime.Blend(from, new CameraShotPose {
                            Position = state.CorrectedPosition, Rotation = state.CorrectedOrientation,
                            FieldOfView = state.Lens.FieldOfView }, returnBlend, freeLook.Follow.position + Vector3.up * 1.2f, true);
                        lastReturnBlend = returnBlend;
                        exiting = false;
                    }
                }
            }
            else timelineReturnHandle = 0;
            if (Application.isPlaying && hasOutput)
            {
                bool returning = CameraShotRuntime.TryGetExit(Channel, out int handle, out float remaining, out bool unscaled);
                if (!returning && !active && exitBeforeUpdate)
                { returning = true; handle = earlierHandle; remaining = earlierRemaining; unscaled = earlierUnscaled; }
                if (returning && !exiting && handle != exitHandle)
                {
                    if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
                    if (freeLook != null && freeLook.Follow != null)
                    {
                        exitFrom = lastOutput;
                        exitFollowPosition = freeLook.Follow.position;
                        // Establish the legal destination before interpolating, never
                        // restore the orbit that was saved at shot entry.
                        bool continuingReturn = previousReturnHandle == handle;
                        if (!continuingReturn) state = FindReturnState(exitFrom);
                        exitHandle = handle; exitDuration = continuingReturn ? remaining * (1f - lastReturnBlend) : remaining; exitUnscaled = unscaled;
                        exitStarted = ExitNow; exiting = true;
                    }
                }
                if (exiting && active && !returning) exiting = false;
                if (exiting)
                {
                    float t = exitDuration > 0 ? Mathf.Clamp01((float)(ExitNow - exitStarted) / exitDuration) : 1;
                    float weight = Mathf.SmoothStep(0, 1, t);
                    Vector3 followDelta = freeLook != null && freeLook.Follow != null ? freeLook.Follow.position - exitFollowPosition : Vector3.zero;
                    var from = exitFrom; from.Position += followDelta;
                    pose = CameraShotRuntime.Blend(from, new CameraShotPose {
                        Position = state.CorrectedPosition, Rotation = state.CorrectedOrientation,
                        FieldOfView = state.Lens.FieldOfView }, weight,
                        exitFollowPosition + followDelta + Vector3.up * 1.2f, true);
                    active = t < 1; lockInput = active;
                    if (!active) { exiting = false; CameraShotRuntime.FinishReleases(Channel); }
                }
            }
            // Editor scrubbing must not serialize temporary locked axis settings.
            SetInputLocked(Application.isPlaying && active && lockInput);
            hasOutput = active;
            if (!active) return;
            lastOutput = pose;
            state.PositionCorrection += pose.Position - state.CorrectedPosition;
            state.OrientationCorrection = Quaternion.Inverse(state.RawOrientation) * pose.Rotation;
            LensSettings lens = state.Lens;
            if (!lens.Orthographic) lens.FieldOfView = pose.FieldOfView;
            state.Lens = lens;
        }

        private double ExitNow => exitUnscaled ? Time.realtimeSinceStartupAsDouble : Time.timeAsDouble;

        public CameraState FindReturnState(CameraShotPose from, bool positionOnly = false)
        {
            if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
            if (freeLook == null || freeLook.Follow == null) return VirtualCamera.State;
            bool yawRecentering = freeLook.m_RecenterToTargetHeading.m_enabled;
            bool pitchRecentering = freeLook.m_YAxisRecentering.m_enabled;
            freeLook.m_RecenterToTargetHeading.m_enabled = false;
            freeLook.m_YAxisRecentering.m_enabled = false;
            IsSolvingReturn = true;
            try
            {
                float originalX = freeLook.m_XAxis.Value, originalY = freeLook.m_YAxis.Value;
                freeLook.ForceCameraPosition(from.Position, from.Rotation);
                float bestX = freeLook.m_XAxis.Value, bestY = Mathf.Clamp01(freeLook.m_YAxis.Value);
                float score = float.PositiveInfinity;
                Vector3 focus = freeLook.LookAt != null ? freeLook.LookAt.position : freeLook.Follow.position;
                void UpdateOrbit()
                {
                    // FreeLook pushes Y-axis settings to its rigs AFTER evaluating them.
                    // The first update synchronizes offsets; the second samples this orbit.
                    freeLook.InternalUpdateCameraState(Vector3.up, -1);
                    freeLook.InternalUpdateCameraState(Vector3.up, -1);
                }
                void Sample(float x, float y)
                {
                    freeLook.m_XAxis.Value = freeLook.m_XAxis.m_Wrap && freeLook.m_XAxis.m_MaxValue > freeLook.m_XAxis.m_MinValue ?
                        Mathf.Repeat(x - freeLook.m_XAxis.m_MinValue, freeLook.m_XAxis.m_MaxValue - freeLook.m_XAxis.m_MinValue) + freeLook.m_XAxis.m_MinValue :
                        Mathf.Clamp(x, freeLook.m_XAxis.m_MinValue, freeLook.m_XAxis.m_MaxValue);
                    freeLook.m_YAxis.Value = Mathf.Clamp(y, freeLook.m_YAxis.m_MinValue, freeLook.m_YAxis.m_MaxValue);
                    UpdateOrbit();
                    var candidate = freeLook.State;
                    float cost = positionOnly ? (from.Position - candidate.CorrectedPosition).sqrMagnitude : ReturnCost(from, new CameraShotPose { Position = candidate.CorrectedPosition,
                        Rotation = candidate.CorrectedOrientation, FieldOfView = candidate.Lens.FieldOfView }, focus);
                    if (cost < score) { score = cost; bestX = freeLook.m_XAxis.Value; bestY = freeLook.m_YAxis.Value; }
                }
                // Search the configured orbit surface, then refine around the best pair.
                Sample(bestX, bestY);
                Sample(originalX, originalY);
                float xSpan = freeLook.m_XAxis.m_MaxValue - freeLook.m_XAxis.m_MinValue;
                float ySpan = freeLook.m_YAxis.m_MaxValue - freeLook.m_YAxis.m_MinValue;
                for (int x = 0; x <= 4 && score > .000001f; x++)
                    for (int y = 0; y <= 2 && score > .000001f; y++) Sample(freeLook.m_XAxis.m_MinValue + xSpan * x / 4f,
                        freeLook.m_YAxis.m_MinValue + ySpan * y / 2f);
                float xStep = xSpan / 8f, yStep = ySpan / 4f;
                for (int pass = 0; pass < 4 && score > .000001f; pass++)
                {
                    float centerX = bestX, centerY = bestY;
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++) Sample(centerX + x * xStep, centerY + y * yStep);
                    xStep *= .5f; yStep *= .5f;
                }
                freeLook.m_XAxis.Value = bestX; freeLook.m_YAxis.Value = bestY;
                freeLook.m_XAxis.Reset(); freeLook.m_YAxis.Reset();
                UpdateOrbit();
                if (inputLocked) { savedX.Value = freeLook.m_XAxis.Value; savedY.Value = freeLook.m_YAxis.Value; }
                return freeLook.State;
            }
            finally
            {
                IsSolvingReturn = false;
                freeLook.m_RecenterToTargetHeading.m_enabled = yawRecentering;
                freeLook.m_YAxisRecentering.m_enabled = pitchRecentering;
                freeLook.m_RecenterToTargetHeading.CancelRecentering();
                freeLook.m_YAxisRecentering.CancelRecentering();
            }
        }

        // Used only on an isolated rig when refreshing a previously solved target.
        public CameraState EvaluateOrbit(float x, float y)
        {
            if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
            bool solving = IsSolvingReturn;
            bool yaw = freeLook.m_RecenterToTargetHeading.m_enabled, pitch = freeLook.m_YAxisRecentering.m_enabled;
            IsSolvingReturn = true;
            freeLook.m_RecenterToTargetHeading.m_enabled = freeLook.m_YAxisRecentering.m_enabled = false;
            try
            {
                freeLook.m_XAxis.Value = x; freeLook.m_YAxis.Value = y;
                freeLook.InternalUpdateCameraState(Vector3.up, -1);
                freeLook.InternalUpdateCameraState(Vector3.up, -1);
                return freeLook.State;
            }
            finally
            {
                IsSolvingReturn = solving;
                freeLook.m_RecenterToTargetHeading.m_enabled = yaw;
                freeLook.m_YAxisRecentering.m_enabled = pitch;
            }
        }
        public static float ReturnCost(CameraShotPose from, CameraShotPose to, Vector3 focus)
        {
            float distanceScale = Mathf.Max(1f, Vector3.Distance(from.Position, focus));
            float angle = Quaternion.Angle(from.Rotation, to.Rotation) / 90f;
            return (from.Position - to.Position).sqrMagnitude / (distanceScale * distanceScale) +
                angle * angle * .35f +
                (Framing(focus, from.Position, from.Rotation, from.FieldOfView) -
                 Framing(focus, to.Position, to.Rotation, to.FieldOfView)).sqrMagnitude * .1f;
        }

        private static Vector2 Framing(Vector3 focus, Vector3 position, Quaternion rotation, float fov)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (focus - position);
            float depth = Mathf.Max(.1f, local.z) * Mathf.Tan(Mathf.Clamp(fov, 1, 179) * Mathf.Deg2Rad * .5f);
            return Vector2.ClampMagnitude(new Vector2(local.x, local.y) / depth, 10);
        }

        private void SetInputLocked(bool locked)
        {
            if (!locked) { RestoreInput(); return; }
            if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
            if (freeLook == null) return;
            if (!inputLocked)
            {
                savedX = freeLook.m_XAxis; savedY = freeLook.m_YAxis;
                savedYawRecentering = freeLook.m_RecenterToTargetHeading;
                savedPitchRecentering = freeLook.m_YAxisRecentering;
                inputLocked = true;
            }
            freeLook.m_XAxis.Value = savedX.Value; freeLook.m_YAxis.Value = savedY.Value;
            freeLook.m_XAxis.m_MaxSpeed = 0f; freeLook.m_YAxis.m_MaxSpeed = 0f;
            // A zero gain alone still leaves damped velocity in InputValueGain mode.
            freeLook.m_XAxis.Reset(); freeLook.m_YAxis.Reset();
            freeLook.m_XAxis.m_Recentering.m_enabled = false;
            freeLook.m_YAxis.m_Recentering.m_enabled = false;
            freeLook.m_RecenterToTargetHeading.m_enabled = false;
            freeLook.m_YAxisRecentering.m_enabled = false;
        }

        private void RestoreInput()
        {
            if (!inputLocked) return;
            if (freeLook != null)
            {
                RestoreAxis(ref freeLook.m_XAxis, savedX);
                RestoreAxis(ref freeLook.m_YAxis, savedY);
                RestoreRecentering(ref freeLook.m_RecenterToTargetHeading, savedYawRecentering);
                RestoreRecentering(ref freeLook.m_YAxisRecentering, savedPitchRecentering);
            }
            inputLocked = false;
        }

        private static void RestoreAxis(ref AxisState axis, AxisState saved)
        {
            var recentering = axis.m_Recentering;
            axis = saved;
            // Keep the configuration/provider and held angle, but not the old
            // timestamp, input sample, or velocity. Next Update uses one frame's dt.
            axis.Reset();
            axis.m_Recentering = recentering;
            RestoreRecentering(ref axis.m_Recentering, saved.m_Recentering);
        }

        private static void RestoreRecentering(ref AxisState.Recentering current, AxisState.Recentering saved)
        {
            // Preserve the live update clock rather than restoring a pre-shot clock.
            current.m_enabled = saved.m_enabled;
            current.m_WaitTime = saved.m_WaitTime;
            current.m_RecenteringTime = saved.m_RecenteringTime;
            current.CancelRecentering();
        }

        private void LateUpdate()
        {
            if (inputLocked && (freeLook == null || !freeLook.isActiveAndEnabled)) RestoreInput();
        }

        private void OnDisable() { returnPreview.Dispose(); targetSolver.Dispose(); timelineReturnHandle = 0; exiting = hasOutput = false; RestoreInput(); }
        protected override void OnDestroy() { returnPreview.Dispose(); targetSolver.Dispose(); RestoreInput(); base.OnDestroy(); }
    }

    // Inactive clone projects targets and previews returns without changing live axes.
    public sealed class CameraShotOrbitSolver : System.IDisposable
    {
        private GameObject root;
        private CinemachineFreeLook source, copy;
        private CameraShotPose cachedFrom;
        private Vector3 follow, lookOffset;
        private Quaternion followRotation;
        private bool cachedPositionOnly;
        private bool cached;
        private int sourceVersion;
        public bool Evaluate(CinemachineFreeLook rig, CameraShotPose from, out CameraShotPose destination, bool positionOnly = false)
        {
            destination = default;
            if (rig == null || rig.Follow == null) return false;
            int version = rig.m_BindingMode.GetHashCode() ^ rig.m_SplineCurvature.GetHashCode() ^ rig.m_Lens.GetHashCode();
            unchecked
            {
                foreach (var orbit in rig.m_Orbits) version = version * 31 + orbit.GetHashCode();
                version = version * 31 + rig.m_XAxis.m_MinValue.GetHashCode();
                version = version * 31 + rig.m_XAxis.m_MaxValue.GetHashCode();
                version = version * 31 + rig.m_YAxis.m_MinValue.GetHashCode();
                version = version * 31 + rig.m_YAxis.m_MaxValue.GetHashCode();
                version = version * 31 + rig.m_XAxis.m_Wrap.GetHashCode();
                version = version * 31 + rig.m_Heading.GetHashCode();
            }
#if UNITY_EDITOR
            version ^= UnityEditor.EditorUtility.GetDirtyCount(rig);
            for (int i = 0; i < 3; i++)
            {
                var child = rig.GetRig(i);
                if (child == null) continue;
                foreach (var component in child.GetComponentsInChildren<Component>(true))
                    if (component != null) version ^= UnityEditor.EditorUtility.GetDirtyCount(component);
            }
#endif
            if (root == null || source != rig || sourceVersion != version || copy.Follow != rig.Follow || copy.LookAt != rig.LookAt)
            {
                Dispose(); source = rig; sourceVersion = version;
                root = new GameObject("CameraShot orbit solver") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                var instance = Object.Instantiate(rig.gameObject, root.transform);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                copy = instance.GetComponent<CinemachineFreeLook>();
                copy.Follow = rig.Follow; copy.LookAt = rig.LookAt;
                foreach (var camera in instance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                    foreach (var extension in camera.GetComponents<CinemachineExtension>()) camera.AddExtension(extension);
            }
            Vector3 delta = rig.Follow.position - follow;
            Vector3 currentLookOffset = rig.LookAt != null ? rig.LookAt.position - rig.Follow.position : Vector3.zero;
            var solver = copy.GetComponent<CinemachineCameraShotExtension>();
            if (solver == null) solver = copy.gameObject.AddComponent<CinemachineCameraShotExtension>();
            CameraState state;
            if (!cached || cachedPositionOnly != positionOnly ||
                Vector3.Distance(from.Position, cachedFrom.Position + delta) > .001f ||
                (!positionOnly && Quaternion.Angle(from.Rotation, cachedFrom.Rotation) > .01f) ||
                (!positionOnly && Mathf.Abs(from.FieldOfView - cachedFrom.FieldOfView) > .01f) ||
                Quaternion.Angle(followRotation, rig.Follow.rotation) > .01f ||
                (currentLookOffset - lookOffset).sqrMagnitude > .000001f)
            {
                state = solver.FindReturnState(from, positionOnly);
                cachedFrom = from; follow = rig.Follow.position; followRotation = rig.Follow.rotation;
                lookOffset = currentLookOffset; cachedPositionOnly = positionOnly; cached = true;
            }
            else state = solver.EvaluateOrbit(copy.m_XAxis.Value, copy.m_YAxis.Value);
            destination = new CameraShotPose { Position = state.CorrectedPosition,
                Rotation = state.CorrectedOrientation, FieldOfView = positionOnly ? from.FieldOfView : state.Lens.FieldOfView };
            return true;
        }
        public void Dispose()
        {
            if (root != null) Object.DestroyImmediate(root);
            root = null; copy = null; source = null; cached = false;
        }
    }
}
