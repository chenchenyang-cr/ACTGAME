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
            var target = new GameObject("Shot validation target") { hideFlags = HideFlags.HideAndDontSave };
            var camera = new GameObject("Shot validation camera") { hideFlags = HideFlags.HideAndDontSave };
            var low = ScriptableObject.CreateInstance<CameraShotProfile>();
            var high = ScriptableObject.CreateInstance<CameraShotProfile>();
            var handles = new List<int>();
            const int channel = 991107;
            int checks = 0;
            void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
            int Begin(CameraShotProfile p, Transform t = null, float duration = -1f)
            {
                int h = CameraShotRuntime.Begin(p, actor.transform, t, duration, channel);
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
                low.PositionOffset = new Vector3(0, 1, -4); high.PositionOffset = new Vector3(2, 2, -2);
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
                var relativePreview = new CameraShotRuntime.PreviewSession().Evaluate(high, actor.transform, null, baseRotation, 67, 0, basePosition);
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
                fixedPreview.CaptureStart(high, actor.transform, null, Vector3.zero, Vector3.forward, null, Quaternion.identity);
                var fixedPreviewPose = fixedPreview.Evaluate(high, actor.transform, null, Quaternion.Euler(55, 120, 0), 75, 0);
                Check(Vector3.Distance(fixedPreviewPose.Position, fixedB.Position) < .001f && Quaternion.Angle(fixedPreviewPose.Rotation, fixedB.Rotation) < .01f, "Fixed target timeline preview matches runtime");
                CameraShotRuntime.Release(fixedHandle);
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                high.Composition = CameraShotComposition.ActorAnchored;
                high.OverrideFieldOfView = true;
                low.ReferenceFrame = CameraShotFrame.ActorFacing;
                actor.transform.SetPositionAndRotation(new Vector3(10, 0, 7), Quaternion.Euler(0, 90, 0));
                var preview = new CameraShotRuntime.PreviewSession();
                var previewPose = preview.Evaluate(low, actor.transform, null, Quaternion.identity, 60, 0);
                int comparison = Begin(low);
                var runtimePose = Pose();
                Check(Vector3.Distance(runtimePose.Position, previewPose.Position) < .001f, "Preview and gameplay positions agree");
                Check(Quaternion.Angle(runtimePose.Rotation, previewPose.Rotation) < .001f, "Preview and gameplay rotations agree");
                Check(Vector3.Distance(preview.ToPositionOffset(previewPose.Position), low.PositionOffset) < .001f, "Capture converts world position to relative offset");
                Check(Vector3.Distance(preview.ToLookOffset(preview.LookAt), low.LookAtOffset) < .001f, "Capture converts look point to relative offset");
                CameraShotRuntime.Release(comparison);
                actor.transform.SetPositionAndRotation(new Vector3(-5, 0, 4), Quaternion.Euler(0, 180, 0));
                preview.Reset();
                previewPose = preview.Evaluate(low, actor.transform, null, Quaternion.identity, 60, 0);
                Check(Vector3.Distance(previewPose.Position, actor.transform.position + actor.transform.rotation * low.PositionOffset) < .001f, "Same profile preserves composition after relocation and rotation");
                low.FollowPosition = false;
                preview.CaptureStart(low, actor.transform, null, Vector3.zero, Vector3.forward, null, Quaternion.identity);
                previewPose = preview.Evaluate(low, actor.transform, null, Quaternion.identity, 60, .5f);
                Check(Vector3.Distance(previewPose.Position, low.PositionOffset) < .001f, "Timeline scrubbing uses event-start basis for frozen shots");
                low.FollowPosition = true;
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                low.ReferenceFrame = CameraShotFrame.CameraFacing;
                int a = Begin(low);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset) < .001f, "Base composition");
                int b = Begin(high);
                Check(Vector3.Distance(Pose().Position, high.PositionOffset) < .001f, "Higher priority wins");
                CameraShotRuntime.SetPreview(b, 0, .5f);
                Check(Vector3.Distance(Pose().Position, (low.PositionOffset + high.PositionOffset) * .5f) < .001f, "Weighted blend");
                CameraShotRuntime.Release(b);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset) < .001f, "Release reveals lower shot");
                Check(!CameraShotRuntime.Evaluate(channel + 1, Vector3.zero, Quaternion.identity, 60, out _, out _), "Channel isolation");
                low.PositionTravel = Vector3.right * 2;
                CameraShotRuntime.SetProgress(a, .5f);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset + Vector3.right) < .001f, "Motion curve progress");
                low.PositionTravel = Vector3.zero;
                // Timeline weight must be deterministic in either scrub direction,
                // independent of the legacy seconds-based blend settings.
                low.BlendIn = low.BlendOut = 10f;
                low.WeightCurve = AnimationCurve.Linear(0, 0, 1, 1);
                CameraShotRuntime.SetProgress(a, .25f);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset * .25f) < .001f, "Timeline quarter weight");
                CameraShotRuntime.SetProgress(a, .75f);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset * .75f) < .001f, "Timeline three-quarter weight");
                CameraShotRuntime.SetProgress(a, .25f);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset * .25f) < .001f, "Reverse scrubbing is deterministic");
                CameraShotRuntime.Release(a);
                Check(!CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Interrupted timeline has no seconds-based tail");
                low.WeightCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, 1), new Keyframe(.75f, 1), new Keyframe(1, 0));
                a = Begin(low);
                CameraShotRuntime.SetProgress(a, 0);
                Check(Pose().Position.sqrMagnitude < .001f, "Timeline starts at game camera");
                CameraShotRuntime.SetProgress(a, .5f);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset) < .001f, "Timeline holds target composition");
                var returnBasis = new CameraShotPose { Rotation = Quaternion.identity, FieldOfView = 60f };
                Check(!CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out _, out _, out _), "Hold does not start return search");
                CameraShotRuntime.SetProgress(a, .9f);
                Check(CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out int returnHandle, out var returnFrom, out float returnBlend) &&
                    returnHandle == a && Vector3.Distance(returnFrom.Position, low.PositionOffset) < .001f && returnBlend > 0f && returnBlend < 1f,
                    "Curve tail requests nearest return with curve blend");
                b = Begin(high);
                Check(!CameraShotRuntime.TryGetTimelineReturn(channel, returnBasis, out _, out _, out _), "Layered shots keep their underlying owner");
                CameraShotRuntime.Release(b, true);
                CameraShotRuntime.SetProgress(a, 1);
                Check(Pose().Position.sqrMagnitude < .001f, "Timeline returns inside its range");
                low.BlendIn = low.BlendOut = 0f;
                low.PositionAnchor = CameraShotAnchor.Midpoint;
                target.transform.position = Vector3.right * 4;
                CameraShotRuntime.Release(a);
                a = Begin(low, target.transform);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset + Vector3.right * 2) < .001f, "Midpoint anchor");
                UnityEngine.Object.DestroyImmediate(target);
                Check(Vector3.Distance(Pose().Position, low.PositionOffset) < .001f, "Destroyed target falls back");
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
                vcam.m_XAxis.Value = 120; vcam.m_YAxis.Value = .75f;
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                var desired = new CameraShotPose { Position = vcam.State.CorrectedPosition,
                    Rotation = vcam.State.CorrectedOrientation, FieldOfView = vcam.State.Lens.FieldOfView };
                vcam.m_XAxis.Value = -30; vcam.m_YAxis.Value = .1f;
                vcam.InternalUpdateCameraState(Vector3.up, -1);
                var oldReturnPose = new CameraShotPose { Position = vcam.State.CorrectedPosition,
                    Rotation = vcam.State.CorrectedOrientation, FieldOfView = vcam.State.Lens.FieldOfView };
                using (var previewReturn = new CameraShotReturnPreview())
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
                a = Begin(low);
                Pose(); actor.SetActive(false);
                Check(!CameraShotRuntime.Evaluate(channel, Vector3.zero, Quaternion.identity, 60, out _, out _), "Disabled actor releases");
                Check(AssetDatabase.LoadAssetAtPath<CameraShotProfile>("Assets/_Project/CameraShot/Profiles/ParryCameraShot.asset") != null, "Preset imports");
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
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(low); UnityEngine.Object.DestroyImmediate(high);
            }
        }
    }
}
