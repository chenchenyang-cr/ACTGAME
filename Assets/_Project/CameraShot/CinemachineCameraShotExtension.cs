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
        private readonly CameraShotReturnPreview returnPreview = new();

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
            bool exitBeforeUpdate = CameraShotRuntime.TryGetExit(Channel, out int earlierHandle, out float earlierRemaining, out bool earlierUnscaled);
            bool active = CameraShotRuntime.Evaluate(Channel, state.CorrectedPosition,
                state.CorrectedOrientation, state.Lens.FieldOfView, out CameraShotPose pose, out bool lockInput);
            if (CameraShotRuntime.TryGetTimelineReturn(Channel, BasePose,
                out int timelineHandle, out var timelineFrom, out float returnBlend))
            {
                if (freeLook == null) freeLook = GetComponent<CinemachineFreeLook>();
                if (freeLook != null && freeLook.Follow != null)
                {
                    if (!Application.isPlaying)
                    {
                        if (returnPreview.Evaluate(freeLook, timelineFrom, out var destination))
                            pose = CameraShotReturnPreview.Blend(timelineFrom, destination, returnBlend);
                    }
                    else
                    {
                        if (timelineReturnHandle != timelineHandle)
                        {
                            timelineReturnFrom = timelineFrom;
                            timelineReturnFollow = freeLook.Follow.position;
                            state = FindReturnState(timelineFrom);
                            timelineReturnHandle = timelineHandle;
                        }
                        Vector3 followDelta = freeLook.Follow.position - timelineReturnFollow;
                        pose = new CameraShotPose {
                            Position = Vector3.Lerp(timelineReturnFrom.Position + followDelta, state.CorrectedPosition, returnBlend),
                            Rotation = Quaternion.Slerp(timelineReturnFrom.Rotation, state.CorrectedOrientation, returnBlend),
                            FieldOfView = Mathf.Lerp(timelineReturnFrom.FieldOfView, state.Lens.FieldOfView, returnBlend)
                        };
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
                        state = FindReturnState(exitFrom);
                        exitHandle = handle; exitDuration = remaining; exitUnscaled = unscaled;
                        exitStarted = ExitNow; exiting = true;
                    }
                }
                if (exiting && active && !returning) exiting = false;
                if (exiting)
                {
                    float t = exitDuration > 0 ? Mathf.Clamp01((float)(ExitNow - exitStarted) / exitDuration) : 1;
                    float weight = Mathf.SmoothStep(0, 1, t);
                    Vector3 followDelta = freeLook != null && freeLook.Follow != null ? freeLook.Follow.position - exitFollowPosition : Vector3.zero;
                    pose = new CameraShotPose {
                        Position = Vector3.Lerp(exitFrom.Position + followDelta, state.CorrectedPosition, weight),
                        Rotation = Quaternion.Slerp(exitFrom.Rotation, state.CorrectedOrientation, weight),
                        FieldOfView = Mathf.Lerp(exitFrom.FieldOfView, state.Lens.FieldOfView, weight)
                    };
                    active = t < 1; lockInput = active;
                    if (!active) exiting = false;
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

        public CameraState FindReturnState(CameraShotPose from)
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
                void Sample(float x, float y)
                {
                    freeLook.m_XAxis.Value = freeLook.m_XAxis.m_Wrap && freeLook.m_XAxis.m_MaxValue > freeLook.m_XAxis.m_MinValue ?
                        Mathf.Repeat(x - freeLook.m_XAxis.m_MinValue, freeLook.m_XAxis.m_MaxValue - freeLook.m_XAxis.m_MinValue) + freeLook.m_XAxis.m_MinValue :
                        Mathf.Clamp(x, freeLook.m_XAxis.m_MinValue, freeLook.m_XAxis.m_MaxValue);
                    freeLook.m_YAxis.Value = Mathf.Clamp(y, freeLook.m_YAxis.m_MinValue, freeLook.m_YAxis.m_MaxValue);
                    freeLook.InternalUpdateCameraState(Vector3.up, -1);
                    var candidate = freeLook.State;
                    float cost = ReturnCost(from, new CameraShotPose { Position = candidate.CorrectedPosition,
                        Rotation = candidate.CorrectedOrientation, FieldOfView = candidate.Lens.FieldOfView }, focus);
                    if (cost < score) { score = cost; bestX = freeLook.m_XAxis.Value; bestY = freeLook.m_YAxis.Value; }
                }
                // Search the configured orbit surface, then refine around the best pair.
                Sample(bestX, bestY);
                Sample(originalX, originalY);
                float xSpan = freeLook.m_XAxis.m_MaxValue - freeLook.m_XAxis.m_MinValue;
                float ySpan = freeLook.m_YAxis.m_MaxValue - freeLook.m_YAxis.m_MinValue;
                for (int x = 0; x <= 8; x++)
                    for (int y = 0; y <= 8; y++) Sample(freeLook.m_XAxis.m_MinValue + xSpan * x / 8f,
                        freeLook.m_YAxis.m_MinValue + ySpan * y / 8f);
                float xStep = xSpan / 32f, yStep = ySpan / 32f;
                for (int pass = 0; pass < 3; pass++)
                {
                    float centerX = bestX, centerY = bestY;
                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++) Sample(centerX + x * xStep, centerY + y * yStep);
                    xStep *= .25f; yStep *= .25f;
                }
                freeLook.m_XAxis.Value = bestX; freeLook.m_YAxis.Value = bestY;
                freeLook.m_XAxis.Reset(); freeLook.m_YAxis.Reset();
                freeLook.InternalUpdateCameraState(Vector3.up, -1);
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

        private void OnDisable() { returnPreview.Dispose(); timelineReturnHandle = 0; exiting = hasOutput = false; RestoreInput(); }
        protected override void OnDestroy() { returnPreview.Dispose(); RestoreInput(); base.OnDestroy(); }
    }

    // Inactive clone keeps editor scrubbing from changing the real FreeLook axes.
    public sealed class CameraShotReturnPreview : System.IDisposable
    {
        private GameObject root;
        private CinemachineFreeLook source, copy;
        private CameraShotPose cachedFrom, cachedDestination;
        private Vector3 follow;
        private bool cached;
        private int sourceVersion;
        public bool Evaluate(CinemachineFreeLook rig, CameraShotPose from, out CameraShotPose destination)
        {
            destination = default;
            if (rig == null || rig.Follow == null) return false;
            int version = 0;
#if UNITY_EDITOR
            version = UnityEditor.EditorUtility.GetDirtyCount(rig);
#endif
            if (root == null || source != rig || sourceVersion != version || copy.Follow != rig.Follow || copy.LookAt != rig.LookAt)
            {
                Dispose(); source = rig; sourceVersion = version;
                root = new GameObject("CameraShot return solver") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                var instance = Object.Instantiate(rig.gameObject, root.transform);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                copy = instance.GetComponent<CinemachineFreeLook>();
                copy.Follow = rig.Follow; copy.LookAt = rig.LookAt;
                foreach (var camera in instance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                    foreach (var extension in camera.GetComponents<CinemachineExtension>()) camera.AddExtension(extension);
            }
            Vector3 delta = rig.Follow.position - follow;
            if (!cached || Vector3.Distance(from.Position, cachedFrom.Position + delta) > .001f ||
                Quaternion.Angle(from.Rotation, cachedFrom.Rotation) > .01f || Mathf.Abs(from.FieldOfView - cachedFrom.FieldOfView) > .01f)
            {
                var solver = copy.GetComponent<CinemachineCameraShotExtension>();
                if (solver == null) solver = copy.gameObject.AddComponent<CinemachineCameraShotExtension>();
                var state = solver.FindReturnState(from);
                cachedFrom = from; follow = rig.Follow.position; cached = true;
                cachedDestination = new CameraShotPose { Position = state.CorrectedPosition,
                    Rotation = state.CorrectedOrientation, FieldOfView = state.Lens.FieldOfView };
                delta = Vector3.zero;
            }
            destination = cachedDestination; destination.Position += delta;
            return true;
        }
        public static CameraShotPose Blend(CameraShotPose from, CameraShotPose to, float t) => new CameraShotPose {
            Position = Vector3.Lerp(from.Position, to.Position, t),
            Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
            FieldOfView = Mathf.Lerp(from.FieldOfView, to.FieldOfView, t)
        };
        public void Dispose()
        {
            if (root != null) Object.DestroyImmediate(root);
            root = null; copy = null; source = null; cached = false;
        }
    }
}
