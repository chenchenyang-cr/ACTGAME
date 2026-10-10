using System;
using Cinemachine;
using UnityEngine;

namespace CombatCamera.Editor
{
    internal static class CameraShotAuthoringValidation
    {
        internal static int Run()
        {
            int checks = 0;
            void Check(bool condition, string name) { if (!condition) throw new Exception("CameraShot authoring: " + name); checks++; }
            bool Near(CameraShotPose a, CameraShotPose b) => Vector3.Distance(a.Position, b.Position) < .002f &&
                Quaternion.Angle(a.Rotation, b.Rotation) < .05f && Mathf.Abs(a.FieldOfView - b.FieldOfView) < .01f;
            var actor = new GameObject("Shot authoring validation actor") { hideFlags = HideFlags.HideAndDontSave };
            var camera = new GameObject("Shot authoring validation rig") { hideFlags = HideFlags.HideAndDontSave };
            var wall = new GameObject("Shot authoring validation wall") { hideFlags = HideFlags.HideAndDontSave };
            var profile = ScriptableObject.CreateInstance<CameraShotProfile>();
            var handles = new System.Collections.Generic.List<int>();
            const int channel = 981771;
            try
            {
                profile.AvoidObstacles = false; profile.FollowDamping = .1f;
                profile.UseKeyframes = true;
                profile.Keys.Add(new CameraShotKey { Time = 0, Position = new Vector3(0, 2, -5), Rotation = new Vector3(5, 0, 0), FieldOfView = 60 });
                profile.Keys.Add(new CameraShotKey { Time = 1, Position = new Vector3(3, 2, -2), Rotation = new Vector3(10, -30, 0), FieldOfView = 40 });
                Check(profile.SampleMotion(0).Position == new Vector3(0, 2, -5), "first key");
                Check(profile.SampleMotion(1).FieldOfView == 40, "last key lens");
                Check(profile.SampleMotion(.5f).Position == new Vector3(1.5f, 2, -3.5f), "interpolated key");
                profile.Keys.Reverse();
                Check(profile.SampleMotion(.5f).FieldOfView == 50, "unsorted keys");
                var timing = new CameraShotTiming { Enabled = true, Enter = .2f, Exit = .25f };
                Check(timing.Evaluate(0) == 0 && timing.Evaluate(1) == 0 && timing.Evaluate(.5f) == 1, "explicit envelope endpoints");
                Check(Mathf.Abs(timing.Evaluate(.1f) - .5f) < .001f, "entry envelope");
                var rig = camera.AddComponent<CinemachineFreeLook>();
                rig.Follow = rig.LookAt = actor.transform;
                rig.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                rig.m_RecenterToTargetHeading.m_enabled = rig.m_YAxisRecentering.m_enabled = false;
                rig.m_XAxis.Value = 20; rig.m_YAxis.Value = .5f;
                rig.InternalUpdateCameraState(Vector3.up, -1); rig.InternalUpdateCameraState(Vector3.up, -1);
                var baseline = new CameraShotPose { Position = rig.State.CorrectedPosition, Rotation = rig.State.CorrectedOrientation, FieldOfView = rig.State.Lens.FieldOfView };
                float x = rig.m_XAxis.Value, y = rig.m_YAxis.Value;
                using (var preview = new CameraShotPreviewRig())
                {
                    preview.Create(rig, actor.transform);
                    var a = preview.Evaluate(profile, timing, actor.transform, .3f, 0, 1, baseline);
                    var b = preview.Evaluate(profile, timing, actor.transform, .65f, 0, 1, baseline);
                    Check(Vector3.Distance(a.Position, b.Position) > .1f, "stationary actor still has camera motion");
                    Check(actor.transform.position == Vector3.zero, "camera-only preview does not move actor");
                    var repeated = preview.Evaluate(profile, timing, actor.transform, .65f, 0, 1, baseline);
                    Check(Near(b, repeated), "paused camera does not drift");
                    preview.Evaluate(profile, timing, actor.transform, .1f, 0, 1, baseline);
                    var sought = preview.Evaluate(profile, timing, actor.transform, .65f, 0, 1, baseline);
                    Check(Near(b, sought), "backward seek is reproducible");
                    var finished = preview.Evaluate(profile, timing, actor.transform, 1.1f, 0, 1, baseline);
                    Check(Near(finished, preview.Basis), "normal exit completes within clip");
                    preview.Reset();
                    void Move(float t) => actor.transform.position = Vector3.right * t * 4;
                    preview.Evaluate(profile, timing, actor.transform, 0, 0, 1, baseline, Move);
                    var startBasis = preview.Basis;
                    preview.Evaluate(profile, timing, actor.transform, .8f, 0, 1, baseline, Move);
                    Check(Vector3.Distance(startBasis.Position, preview.Basis.Position) > .5f, "edit-mode base camera follows moving actor");
                    var jumped = preview.Output;
                    preview.Reset();
                    for (int i = 0; i <= 48; i++) preview.Evaluate(profile, timing, actor.transform, i / 60f, 0, 1, baseline, Move);
                    Check(Near(jumped, preview.Output), "jump and frame-by-frame playback agree");
                    Check(Mathf.Abs(rig.m_XAxis.Value - x) < .001f && Mathf.Abs(rig.m_YAxis.Value - y) < .001f, "preview preserves source camera axes");
                }
                actor.transform.position = Vector3.zero;
                int h = CameraShotRuntime.Begin(profile, actor.transform, channel: channel, timing: timing); handles.Add(h);
                CameraShotRuntime.SetProgress(h, .5f);
                CameraShotRuntime.Evaluate(channel, baseline.Position, baseline.Rotation, baseline.FieldOfView, out var first, out _);
                int next = CameraShotRuntime.Begin(profile, actor.transform, channel: channel, timing: timing); handles.Add(next);
                CameraShotRuntime.SetProgress(next, 0);
                CameraShotRuntime.Evaluate(channel, baseline.Position, baseline.Rotation, baseline.FieldOfView, out var chained, out _);
                Check(Near(first, chained), "new shot starts from current composition");
                CameraShotRuntime.Release(next, true);
                CameraShotRuntime.SetProgress(h, 1); CameraShotRuntime.Release(h);
                Check(!CameraShotRuntime.Evaluate(channel, baseline.Position, baseline.Rotation, baseline.FieldOfView, out _, out _), "normal completion has no extra exit tail");
                var box = wall.AddComponent<BoxCollider>(); wall.layer = 3;
                wall.transform.position = new Vector3(0, 1.2f, -2); box.size = new Vector3(3, 3, .3f);
                Physics.SyncTransforms(); profile.AvoidObstacles = true;
                var blocked = CameraShotRuntime.ConstrainFinalPose(profile, Vector3.zero,
                    new CameraShotPose { Position = new Vector3(0, 1.2f, -5), Rotation = Quaternion.identity, FieldOfView = 60 });
                Check(blocked.Position.z > -2, "final blended position respects obstacles");
                profile.AvoidObstacles = false;
                // Exercise the same world-to-local write path used by viewport navigation.
                var window = ScriptableObject.CreateInstance<CameraShotWindow>();
                try
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    void Set(string field, object value) => typeof(CameraShotWindow).GetField(field, flags).SetValue(window, value);
                    object Call(string method, params object[] args) => typeof(CameraShotWindow).GetMethod(method, flags).Invoke(window, args);
                    var sourceCamera = camera.AddComponent<Camera>(); sourceCamera.enabled = false;
                    Set("profile", profile); Set("actor", actor.transform); Set("source", sourceCamera); Set("gameRig", rig);
                    Set("actionEvent", null); Set("timeline", false); Set("handles", false);
                    Set("elapsed", .5f); Set("progress", .5f); Set("duration", 1f);
                    Call("ResetBasis"); Check((bool)Call("Evaluate"), "authoring window evaluates without Play mode");
                    var desired = new CameraShotPose { Position = new Vector3(2, 3, -4), Rotation = Quaternion.Euler(10, 20, 0), FieldOfView = 37 };
                    Call("WritePose", desired, true); Call("Evaluate");
                    var authored = (CameraShotPose)typeof(CameraShotWindow).GetField("pose", flags).GetValue(window);
                    Check(Near(authored, desired), "viewport records desired world pose and FOV");
                    Check(profile.Keys.Count == 3 && profile.SampleMotion(0).FieldOfView == 60 && profile.SampleMotion(1).FieldOfView == 40,
                        "editing middle key preserves endpoints");
                    Check(actor.transform.position == Vector3.zero, "authoring does not add character displacement");
                }
                finally { UnityEngine.Object.DestroyImmediate(window); }
                return checks;
            }
            finally
            {
                foreach (int h in handles) CameraShotRuntime.Release(h, true);
                UnityEngine.Object.DestroyImmediate(camera); UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
