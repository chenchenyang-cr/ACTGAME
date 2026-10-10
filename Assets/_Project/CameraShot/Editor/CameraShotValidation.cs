using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace CombatCamera.Editor
{
    [InitializeOnLoad]
    public static class CameraShotValidation
    {
        static CameraShotValidation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CameraShotRuntime.Reset;
            EditorApplication.playModeStateChanged += _ => CameraShotRuntime.Reset();
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Temp/CameraShotValidation.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete("Temp/CameraShotValidation.request");
                Validate();
            };
        }

        [MenuItem("Tools/Camera/Validate Camera Shot")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var actor = new GameObject("Shot validation actor") { hideFlags = HideFlags.HideAndDontSave };
            var camera = new GameObject("Shot validation camera") { hideFlags = HideFlags.HideAndDontSave };
            var low = ScriptableObject.CreateInstance<CameraShotProfile>();
            var high = ScriptableObject.CreateInstance<CameraShotProfile>();
            var handles = new List<int>();
            const int channel = 991107;
            int checks = 0;
            void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
            int Begin(CameraShotProfile p, float duration = -1f)
            {
                int h = CameraShotRuntime.Begin(p, actor.transform, duration, channel);
                handles.Add(h); return h;
            }
            CameraShotPose Pose()
            {
                CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60f, out var p, out _);
                return p;
            }
            try
            {
                low.AvoidObstacles = high.AvoidObstacles = false;
                low.BlendIn = low.BlendOut = high.BlendIn = high.BlendOut = 0f;
                low.TargetPosition = new Vector3(0, 1, -4); high.TargetPosition = new Vector3(2, 2, -2);
                low.Priority = 1; high.Priority = 2;
                high.Composition = CameraShotComposition.GameCameraRelative;
                high.OverrideFieldOfView = false;
                Vector3 basePosition = new Vector3(7, 3, -9);
                Quaternion baseRotation = Quaternion.Euler(23, 125, 8);
                int relativeHandle = Begin(high);
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 67, out var relativePose, out _);
                Check(Vector3.Distance(relativePose.Position, basePosition) < .001f, "Zero relative position matches game camera");
                Check(Quaternion.Angle(relativePose.Rotation, baseRotation) < .01f, "Zero relative angle preserves pitch yaw and roll");
                Check(Mathf.Approximately(relativePose.FieldOfView, 67), "Relative camera inherits FOV");
                high.CameraPositionOffset = new Vector3(.3f, .2f, 1);
                high.CameraRotationOffset = new Vector3(-5, 15, 2);
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 67, out relativePose, out _);
                Check(Vector3.Distance(relativePose.Position, basePosition + baseRotation * high.CameraPositionOffset) < .001f, "Displacement uses full camera-local axes");
                var relativePreview = new CameraShotRuntime.PreviewSession().Evaluate(high, actor.transform, baseRotation, 67, 0, basePosition);
                Check(Vector3.Distance(relativePreview.Position, relativePose.Position) < .001f &&
                    Quaternion.Angle(relativePreview.Rotation, relativePose.Rotation) < .01f, "Relative preview and runtime agree");
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 67, out var repeatedPose, out _);
                Check(Vector3.Distance(repeatedPose.Position, relativePose.Position) < .001f, "Offsets do not accumulate across evaluations");
                CameraShotRuntime.SetPreview(relativeHandle, 0, .5f);
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 67, out var blendedPose, out _);
                Check(Vector3.Distance(blendedPose.Position, Vector3.Lerp(basePosition, relativePose.Position, .5f)) < .001f &&
                    Quaternion.Angle(blendedPose.Rotation, Quaternion.Slerp(baseRotation, relativePose.Rotation, .5f)) < .01f, "Blend interpolates target position and angle");
                CameraShotRuntime.Release(relativeHandle);
                high.Composition = CameraShotComposition.ActorStartPose;
                high.TargetPosition = new Vector3(2, 1.5f, -3);
                high.TargetRotation = new Vector3(10, -20, 0);
                high.FieldOfView = 48;
                int fixedHandle = Begin(high);
                // Rotate after Begin, before the first camera update: direction
                // must still be the direction at trigger time.
                actor.transform.rotation = Quaternion.Euler(0, 90, 0);
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 30, out var fixedA, out _);
                CameraShotRuntime.Evaluate(channel, new Vector3(-20, 10, 5), Quaternion.Euler(80, -40, 30), 90, out var fixedB, out _);
                Check(Vector3.Distance(fixedA.Position, fixedB.Position) < .001f && Quaternion.Angle(fixedA.Rotation, fixedB.Rotation) < .01f && Mathf.Approximately(fixedA.FieldOfView, fixedB.FieldOfView), "Different input views converge to identical target pose and FOV");
                Check(Vector3.Distance(fixedA.Position, high.TargetPosition) < .001f && Quaternion.Angle(fixedA.Rotation, Quaternion.Euler(high.TargetRotation)) < .01f, "Fixed target captures actor direction at Begin");
                actor.transform.position = new Vector3(5, 0, 2);
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 30, out fixedB, out _);
                Check(Vector3.Distance(fixedB.Position - fixedA.Position, actor.transform.position) < .001f && Quaternion.Angle(fixedB.Rotation, fixedA.Rotation) < .01f, "Fixed target follows translation without following actor turn");
                var fixedPreview = new CameraShotRuntime.PreviewSession();
                fixedPreview.CaptureStart(high, actor.transform, Vector3.zero, Vector3.forward);
                var fixedPreviewPose = fixedPreview.Evaluate(high, actor.transform, Quaternion.Euler(55, 120, 0), 75, 0);
                Check(Vector3.Distance(fixedPreviewPose.Position, fixedB.Position) < .001f && Quaternion.Angle(fixedPreviewPose.Rotation, fixedB.Rotation) < .01f, "Fixed target timeline preview matches runtime");
                high.LookAtPlayer = true;
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 30, out var aimed, out _);
                Check(Vector3.Angle(aimed.Rotation * Vector3.forward, actor.transform.position + Vector3.up * 1.2f - aimed.Position) < .01f,
                    "Look-at aims at the player's current upper body");
                Check(aimed.Position == fixedB.Position && Mathf.Approximately(aimed.FieldOfView, fixedB.FieldOfView),
                    "Look-at changes only the target angle");
                var aimedPreview = fixedPreview.Evaluate(high, actor.transform, baseRotation, 30, 0);
                Check(Quaternion.Angle(aimedPreview.Rotation, aimed.Rotation) < .01f, "Look-at preview matches runtime");
                Vector3 savedPosition = high.TargetPosition;
                high.TargetPosition = Vector3.up * 1.2f;
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 30, out var coincident, out _);
                Check(!float.IsNaN(coincident.Rotation.x) && Quaternion.Angle(coincident.Rotation, Quaternion.Euler(high.TargetRotation)) < .01f,
                    "Coincident camera and aim point retain a valid rotation");
                high.TargetPosition = savedPosition;
                high.LookAtPlayer = false;
                CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 30, out var restoredAngle, out _);
                Check(Quaternion.Angle(restoredAngle.Rotation, fixedB.Rotation) < .01f, "Disabling look-at restores authored angle");
                CameraShotRuntime.Release(fixedHandle);
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                high.Composition = CameraShotComposition.ActorStartPose;
                high.OverrideFieldOfView = true;
                actor.transform.SetPositionAndRotation(new Vector3(10, 0, 7), Quaternion.Euler(0, 90, 0));
                var preview = new CameraShotRuntime.PreviewSession();
                var previewPose = preview.Evaluate(low, actor.transform, Quaternion.identity, 60, 0);
                int comparison = Begin(low);
                var runtimePose = Pose();
                Check(Vector3.Distance(runtimePose.Position, previewPose.Position) < .001f, "Preview and gameplay positions agree");
                Check(Quaternion.Angle(runtimePose.Rotation, previewPose.Rotation) < .001f, "Preview and gameplay rotations agree");
                Check(Vector3.Distance(preview.ToPositionOffset(previewPose.Position), low.TargetPosition) < .001f, "Capture converts world position to relative offset");
                CameraShotRuntime.Release(comparison);
                actor.transform.SetPositionAndRotation(new Vector3(-5, 0, 4), Quaternion.Euler(0, 180, 0));
                preview.Reset();
                previewPose = preview.Evaluate(low, actor.transform, Quaternion.identity, 60, 0);
                Check(Vector3.Distance(previewPose.Position, actor.transform.position + actor.transform.rotation * low.TargetPosition) < .001f, "Same profile preserves composition after relocation and rotation");
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                int a = Begin(low);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition) < .001f, "Base composition");
                int b = Begin(high);
                Check(Vector3.Distance(Pose().Position, high.TargetPosition) < .001f, "Higher priority wins");
                CameraShotRuntime.SetPreview(b, 0, .5f);
                Check(Vector3.Distance(Pose().Position, (low.TargetPosition + high.TargetPosition) * .5f) < .001f, "Weighted blend");
                CameraShotRuntime.Release(b);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition) < .001f, "Release reveals lower shot");
                Check(!CameraShotRuntime.Evaluate(channel + 1, Vector3.zero, Quaternion.identity, 60, out _, out _), "Channel isolation");
                low.PositionTravel = Vector3.right * 2;
                CameraShotRuntime.SetProgress(a, .5f);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition + Vector3.right) < .001f, "Motion curve progress");
                low.PositionTravel = Vector3.zero;
                CameraShotRuntime.Release(a, true);
                a = Begin(low); // Exercise live timeline weights, not a lingering preview handle.
                // Timeline weight must be deterministic in either scrub direction,
                // independent of the legacy seconds-based blend settings.
                low.BlendIn = low.BlendOut = 10f;
                low.WeightCurve = AnimationCurve.Linear(0, 0, 1, 1);
                CameraShotRuntime.SetProgress(a, .25f);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition * .25f) < .001f, "Timeline quarter weight");
                CameraShotRuntime.SetProgress(a, .75f);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition * .75f) < .001f, "Timeline three-quarter weight");
                CameraShotRuntime.SetProgress(a, .25f);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition * .25f) < .001f, "Reverse scrubbing is deterministic");
                CameraShotRuntime.Release(a);
                Check(CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Interrupted timeline retains a release tail");
                CameraShotRuntime.FinishReleases(channel);
                Check(!CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Completed return removes the release tail");
                low.WeightCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.22f, 1), new Keyframe(.74f, .9981f), new Keyframe(1, 0));
                Check(!low.TryGetReturnBlend(.5f, out _, out _), "Nearly flat hold does not trigger an early return");
                Check(low.TryGetReturnBlend(.75f, out float explicitStart, out float initialBlend) &&
                    Mathf.Approximately(explicitStart, .75f) && initialBlend == 0f, "Return starts at the authored boundary");
                Check(low.TryGetReturnBlend(1f, out _, out float finalBlend) && finalBlend == 1f, "Return reaches the game camera");
                var arcFrom = new CameraShotPose { Position = new Vector3(-4, 0, 0), Rotation = Quaternion.LookRotation(Vector3.right), FieldOfView = 50 };
                var arcTo = new CameraShotPose { Position = new Vector3(0, 0, -4), Rotation = Quaternion.identity, FieldOfView = 60 };
                var arcMiddle = CameraShotRuntime.Blend(arcFrom, arcTo, .5f, Vector3.zero, true);
                Check(Mathf.Abs(arcMiddle.Position.magnitude - 4f) < .001f, "Orbit transition preserves subject distance");
                Check(Vector3.Angle(arcMiddle.Rotation * Vector3.forward, -arcMiddle.Position) < .01f, "Orbit transition preserves subject framing");
                Check(CameraShotRuntime.Blend(arcFrom, arcTo, 0, Vector3.zero, true).Position == arcFrom.Position &&
                    CameraShotRuntime.Blend(arcFrom, arcTo, 1, Vector3.zero, true).Position == arcTo.Position, "Orbit transition preserves endpoints");
                low.WeightCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, 1), new Keyframe(.75f, 1), new Keyframe(1, 0));
                a = Begin(low);
                CameraShotRuntime.SetProgress(a, 0);
                Check(Pose().Position.sqrMagnitude < .001f, "Timeline starts at game camera");
                CameraShotRuntime.SetProgress(a, .5f);
                Check(Vector3.Distance(Pose().Position, low.TargetPosition) < .001f, "Timeline holds target composition");
                var returnBasis = new CameraShotPose { Rotation = Quaternion.identity, FieldOfView = 60f };
                Check(!CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out _, out _, out _), "Hold does not start return search");
                CameraShotRuntime.SetProgress(a, .9f);
                Check(CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out int returnHandle, out var returnFrom, out float returnBlend) &&
                    returnHandle == a && Vector3.Distance(returnFrom.Position, low.TargetPosition) < .001f && returnBlend > 0f && returnBlend < 1f,
                    "Curve tail requests nearest return with curve blend");
                b = Begin(high);
                Check(!CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out _, out _, out _), "Layered shots keep their underlying owner");
                CameraShotRuntime.Release(b, true);
                CameraShotRuntime.SetProgress(a, 1);
                Check(Pose().Position.sqrMagnitude < .001f, "Timeline returns inside its range");
                low.BlendIn = low.BlendOut = 0f;
                CameraShotRuntime.Release(a);
                a = Begin(low);
                var vcam = camera.AddComponent<CinemachineFreeLook>();
                var extension = camera.AddComponent<CinemachineCameraShotExtension>();
                extension.Channel = channel;
                float originalSpeed = vcam.m_XAxis.m_MaxSpeed;
                var state = CameraState.Default;
                extension.Apply(ref state);
                Check(Mathf.Approximately(vcam.m_XAxis.m_MaxSpeed, originalSpeed), "Editor preview preserves serialized axes");
                var lockMethod = typeof(CinemachineCameraShotExtension).GetMethod("SetInputLocked", BindingFlags.Instance | BindingFlags.NonPublic);
                lockMethod.Invoke(extension, new object[] { true });
                Check(vcam.m_XAxis.m_MaxSpeed == 0f, "FreeLook input locked");
                Check(Vector3.Distance(state.CorrectedPosition, Pose().Position) < .001f, "Extension applies pose");
                CameraShotRuntime.Release(a);
                state = CameraState.Default;
                extension.Apply(ref state);
                Check(Mathf.Approximately(vcam.m_XAxis.m_MaxSpeed, originalSpeed), "FreeLook input restored");
                // Reproduce an axis snapshot from several seconds ago while input
                // is held. Unlock must behave like a fresh normal one-frame update.
                AxisState WithPrivate(AxisState axis, string field, object value)
                {
                    object boxed = axis;
                    typeof(AxisState).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, value);
                    return (AxisState)boxed;
                }
                foreach (AxisState.SpeedMode mode in new[] { AxisState.SpeedMode.MaxSpeed, AxisState.SpeedMode.InputValueGain })
                {
                    var axis = new AxisState(-180, 180, false, false, mode == AxisState.SpeedMode.MaxSpeed ? 90 : 2,
                        .2f, .2f, "", false) { Value = 10, m_SpeedMode = mode };
                    axis = WithPrivate(axis, "m_LastUpdateTime", Time.realtimeSinceStartup - 3f);
                    axis = WithPrivate(axis, "m_CurrentSpeed", 30f);
                    axis.m_InputAxisValue = 1;
                    vcam.m_XAxis = axis; vcam.m_YAxis = axis;
                    lockMethod.Invoke(extension, new object[] { true });
                    Check(vcam.m_XAxis.m_InputAxisValue == 0 && vcam.m_YAxis.m_InputAxisValue == 0, mode + " discards locked input");
                    lockMethod.Invoke(extension, new object[] { false });
                    Check(Mathf.Approximately(vcam.m_XAxis.Value, 10) && Mathf.Approximately(vcam.m_YAxis.Value, 10), mode + " unlock preserves held angles");
                    var expected = axis; expected.Reset();
                    expected = WithPrivate(expected, "m_LastUpdateFrame", -1);
                    expected.m_InputAxisValue = 1;
                    expected.Update(1f / 60);
                    var resumed = WithPrivate(vcam.m_XAxis, "m_LastUpdateFrame", -1);
                    resumed.m_InputAxisValue = 1;
                    resumed.Update(1f / 60);
                    Check(Mathf.Abs(resumed.Value - expected.Value) < .001f, mode + " continuous input resumes with one frame of acceleration");
                    var released = WithPrivate(vcam.m_YAxis, "m_LastUpdateFrame", -1);
                    released.Update(1f / 60);
                    Check(Mathf.Abs(released.Value - 10) < .001f, mode + " no stale movement when input stops");
                }
                Begin(low, duration: 0f);
                Check(!CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Timed shot expires");
                // A reachable orbit should be recovered from a very different old
                // orbit, and unlock must keep the recovered axes.
                vcam.Follow = actor.transform; vcam.LookAt = actor.transform;
                vcam.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                vcam.m_XAxis = new AxisState(-180, 180, true, false, 90, .2f, .2f, "", false);
                vcam.m_YAxis = new AxisState(0, 1, false, false, 1, .2f, .2f, "", false);
                // First update creates/synchronizes the three rigs after assigning Follow.
                // Capture the destination only after their configured orbits are active.
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                vcam.m_XAxis.Value = 120; vcam.m_YAxis.Value = .75f;
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                var desired = new CameraShotPose { Position = vcam.State.CorrectedPosition,
                    Rotation = vcam.State.CorrectedOrientation, FieldOfView = vcam.State.Lens.FieldOfView };
                vcam.m_XAxis.Value = -30; vcam.m_YAxis.Value = .1f;
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                var oldReturnPose = new CameraShotPose { Position = vcam.State.CorrectedPosition,
                    Rotation = vcam.State.CorrectedOrientation, FieldOfView = vcam.State.Lens.FieldOfView };
                using (var previewReturn = new CameraShotOrbitSolver())
                {
                    float beforeX = vcam.m_XAxis.Value, beforeY = vcam.m_YAxis.Value;
                    Check(previewReturn.Evaluate(vcam, desired, out var previewDestination), "Return preview solves on an inactive copy");
                    Check(Mathf.Approximately(vcam.m_XAxis.Value, beforeX) && Mathf.Approximately(vcam.m_YAxis.Value, beforeY), "Return preview preserves scene axes");
                    Check(CinemachineCameraShotExtension.ReturnCost(desired, previewDestination, actor.transform.position) <=
                        CinemachineCameraShotExtension.ReturnCost(desired, oldReturnPose, actor.transform.position) + .001f, "Preview return changes no more than old orbit");
                }
                lockMethod.Invoke(extension, new object[] { true });
                var recovered = extension.FindReturnState(desired);
                var recoveredPose = new CameraShotPose { Position = recovered.CorrectedPosition,
                    Rotation = recovered.CorrectedOrientation, FieldOfView = recovered.Lens.FieldOfView };
                Check(CinemachineCameraShotExtension.ReturnCost(desired, recoveredPose, actor.transform.position) <=
                    CinemachineCameraShotExtension.ReturnCost(desired, oldReturnPose, actor.transform.position) + .001f, "Runtime return changes no more than old orbit");
                float recoveredX = vcam.m_XAxis.Value, recoveredY = vcam.m_YAxis.Value;
                Check(recoveredY >= 0 && recoveredY <= 1 && recoveredX >= -180 && recoveredX <= 180, "Return stays inside configured axis ranges");
                Check(Vector3.Angle(desired.Rotation * Vector3.forward, recovered.CorrectedOrientation * Vector3.forward) < 10, "Return preserves a reachable viewing direction");
                lockMethod.Invoke(extension, new object[] { false });
                Check(Mathf.Abs(Mathf.DeltaAngle(recoveredX, vcam.m_XAxis.Value)) < .001f && Mathf.Abs(recoveredY - vcam.m_YAxis.Value) < .001f, "Unlock preserves recovered orbit instead of old orbit");
                Check((int)CameraShotComposition.GameCameraRelative == 1 && (int)CameraShotComposition.ActorStartPose == 2 &&
                    Enum.GetValues(typeof(CameraShotComposition)).Length == 2, "Remaining modes preserve serialized values");
                low.LimitToGameCameraOrbit = true;
                low.TargetPosition = new Vector3(-40, 20, -40);
                low.TargetRotation = new Vector3(80, 170, 35);
                low.FieldOfView = 42;
                vcam.m_XAxis.m_MinValue = -30; vcam.m_XAxis.m_MaxValue = 30; vcam.m_XAxis.m_Wrap = false;
                vcam.m_XAxis.Value = 5; vcam.m_YAxis.Value = .4f;
                using (var targetSolver = new CameraShotOrbitSolver())
                {
                    bool Project(CameraShotPose input, out CameraShotPose output) => targetSolver.Evaluate(vcam, input, out output, true);
                    int constrainedHandle = Begin(low);
                    Check(!CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var missingRig, out _) &&
                        missingRig.Position == basePosition, "Limited target without a solver keeps the game camera");
                    CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var constrained, out _, Project);
                    Check(Mathf.Abs(vcam.m_XAxis.Value - 5) < .001f && Mathf.Abs(vcam.m_YAxis.Value - .4f) < .001f,
                        "Target projection does not change live FreeLook axes");
                    float yaw = Mathf.Atan2(-constrained.Position.x, -constrained.Position.z) * Mathf.Rad2Deg;
                    Check(yaw >= -30.01f && yaw <= 30.01f, "Limited target respects horizontal axis bounds");
                    float nearest = float.PositiveInfinity;
                    var radial = new Vector2(new Vector2(constrained.Position.x, constrained.Position.z).magnitude, constrained.Position.y);
                    for (int i = 0; i <= 1000; i++)
                    {
                        var point = vcam.GetLocalPositionForCameraFromInput(i / 1000f);
                        nearest = Mathf.Min(nearest, Vector2.Distance(radial, new Vector2(-point.z, point.y)));
                    }
                    Check(nearest < .01f, "Limited target lies on the normal orbit surface");
                    Check(Mathf.Approximately(constrained.FieldOfView, 42), "Target constraint preserves authored FOV");
                    Check(Quaternion.Angle(constrained.Rotation, Quaternion.Euler(low.TargetRotation)) > 1,
                        "Target constraint adopts the normal camera angle");
                    var constrainedPreview = new CameraShotRuntime.PreviewSession().Evaluate(low, actor.transform,
                        baseRotation, 60, 0, basePosition, Project);
                    Check(Vector3.Distance(constrainedPreview.Position, constrained.Position) < .001f &&
                        Quaternion.Angle(constrainedPreview.Rotation, constrained.Rotation) < .01f, "Limited preview and runtime agree");
                    CameraShotRuntime.SetPreview(constrainedHandle, 0, .5f);
                    CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var half, out _, Project);
                    Check(Vector3.Distance(half.Position, Vector3.Lerp(basePosition, constrained.Position, .5f)) < .001f,
                        "Only the target is constrained, not the transition");
                    CameraShotRuntime.SetPreview(constrainedHandle, 0, 1);
                    actor.transform.position = new Vector3(3, 0, 2);
                    CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var movedTarget, out _, Project);
                    Check(Vector3.Distance(movedTarget.Position, constrained.Position + actor.transform.position) < .001f,
                        "Cached orbit follows the live follow target");
                    low.LookAtPlayer = true;
                    CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var aimedTarget, out _, Project);
                    Check(Vector3.Distance(aimedTarget.Position, movedTarget.Position) < .001f && Mathf.Approximately(aimedTarget.FieldOfView, movedTarget.FieldOfView),
                        "Look-at preserves constrained position and FOV");
                    Check((aimedTarget.Rotation * Vector3.forward - (actor.transform.position + Vector3.up * 1.2f - aimedTarget.Position).normalized).sqrMagnitude < .00000001f,
                        "Look-at overrides the constrained target angle");
                    low.LookAtPlayer = false;
                    low.LimitToGameCameraOrbit = false;
                    CameraShotRuntime.Evaluate(channel, basePosition, baseRotation, 60, out var unrestricted, out _, Project);
                    Check(Vector3.Distance(unrestricted.Position, actor.transform.position + low.TargetPosition) < .001f,
                        "Disabling constraint restores the authored target");
                    CameraShotRuntime.Release(constrainedHandle, true);
                    actor.transform.position = Vector3.zero;
                }
                a = Begin(low);
                Pose(); actor.SetActive(false);
                Check(!CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Disabled actor releases");
                Check(AssetDatabase.LoadAssetAtPath<CameraShotProfile>("Assets/_Project/CameraShot/Profiles/ParryCameraShot.asset") != null, "Preset imports");
                checks += CameraShotAuthoringValidation.Run();
                string result = $"PASS: CameraShot {checks} checks";
                File.WriteAllText("Temp/CameraShotValidation.txt", result); Debug.Log(result);
            }
            catch (Exception e)
            {
                File.WriteAllText("Temp/CameraShotValidation.txt", "FAIL: " + e); Debug.LogException(e);
            }
            finally
            {
                foreach (int h in handles) CameraShotRuntime.Release(h, true);
                UnityEngine.Object.DestroyImmediate(camera); UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(low); UnityEngine.Object.DestroyImmediate(high);
            }
        }
    }
}
