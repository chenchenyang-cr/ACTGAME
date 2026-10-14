using System;
using System.IO;
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
            EditorApplication.delayCall += () => {
                if (!File.Exists("Temp/CameraShotValidation.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete("Temp/CameraShotValidation.request"); Validate();
            };
        }

        [MenuItem("Tools/Camera/Validate Camera Shot")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = new GameObject("CameraShot validation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var actor = new GameObject("Actor"); actor.transform.SetParent(root.transform);
            var focus = new GameObject("LookAt"); focus.transform.SetParent(actor.transform); focus.transform.localPosition = Vector3.up * 1.2f;
            var camera = new GameObject("FreeLook"); camera.transform.SetParent(root.transform);
            var profile = ScriptableObject.CreateInstance<CameraShotProfile>();
            int checks = 0;
            void Check(bool ok, string label) { if (!ok) throw new Exception("CameraShot: " + label); checks++; }
            try {
                var rig = camera.AddComponent<CinemachineFreeLook>();
                rig.Follow = actor.transform; rig.LookAt = focus.transform;
                rig.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                rig.m_Orbits = new[] { new CinemachineFreeLook.Orbit(3, 3.5f), new CinemachineFreeLook.Orbit(1, 4.5f), new CinemachineFreeLook.Orbit(0, 3.5f) };
                rig.m_XAxis.Value = 25; rig.m_YAxis.Value = .5f;
                rig.m_XAxis.m_InputAxisName = rig.m_YAxis.m_InputAxisName = "";
                rig.m_XAxis.m_MaxSpeed = rig.m_YAxis.m_MaxSpeed = 0;
                rig.m_Lens.FieldOfView = 60;
                rig.InternalUpdateCameraState(Vector3.up, -1);
                rig.InternalUpdateCameraState(Vector3.up, -1);
                using (var solver = new CameraShotOrbitSolver()) {
                    Vector2 original = new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value);
                    profile.Candidates[0].RelativePosition = new Vector2(3, -1);
                    profile.Candidates[1].RelativePosition = new Vector2(-3, -1);
                    Check(Mathf.Abs(CameraShotCandidate.AngleFromDirection(Vector2.down)) < .001f &&
                          Vector2.Distance(CameraShotCandidate.DirectionFromAngle(90), Vector2.right) < .001f &&
                          Vector2.Distance(CameraShotCandidate.DirectionFromAngle(180), Vector2.up) < .001f &&
                          Vector2.Distance(CameraShotCandidate.DirectionFromAngle(270), Vector2.left) < .001f,
                        "horizontal angle covers the full orbit");
                    float oldAngle = CameraShotCandidate.AngleFromDirection(profile.Candidates[0].RelativePosition);
                    Check(Vector2.Dot(CameraShotCandidate.DirectionFromAngle(oldAngle),
                          profile.Candidates[0].RelativePosition.normalized) > .999f,
                        "existing relative position keeps its direction as an angle");
                    profile.Candidates[0].Height = profile.Candidates[1].Height = 1.8f;
                    Check(solver.TryCandidate(rig, actor.transform, profile.Candidates[0], out var rightAxes, out var right), "right candidate resolves");
                    Check(solver.TryCandidate(rig, actor.transform, profile.Candidates[1], out var leftAxes, out var left), "left candidate resolves");
                    Check(Mathf.Abs(right.Position.y - 1.8f) < .002f, "requested camera height");
                    Check(right.Position.x > 0 && left.Position.x < 0, "positions relative to player");
                    Check(Mathf.Abs(right.FieldOfView - 60) < .001f, "inherits source lens");
                    var session = new CameraShotSession();
                    Check(session.Capture(profile, actor.transform, rig, original, left.Position, solver), "session captures");
                    Check(session.SelectedCandidate == 1, "nearest position chooses left");
                    var timing = CameraShotTiming.Default;
                    timing.EnterSeconds = .2f; timing.ExitSeconds = .3f;
                    timing.EnterCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, .25f), new Keyframe(1, 1));
                    var atStart = session.Evaluate(rig, solver, original, 0, 1, timing);
                    Check(Vector2.Distance(atStart, original) < .001f, "entry starts at current axes");
                    var half = session.Evaluate(rig, solver, original, .1f, 1, timing);
                    var expectedHalf = CameraShotOrbitSolver.BlendAxes(rig, original, leftAxes, .25f);
                    Check(Vector2.Distance(half, expectedHalf) < .001f, "entry uses custom time and curve");
                    for (int i = 0; i <= 100; i++) {
                        var axes = session.Evaluate(rig, solver, original, i / 100f, 1, timing);
                        var pose = solver.Sample(rig, axes);
                        var local = rig.GetLocalPositionForCameraFromInput(axes.y);
                        float radius = Vector3.ProjectOnPlane(pose.Position - actor.transform.position, Vector3.up).magnitude;
                        Check(Mathf.Abs(radius - Mathf.Abs(local.z)) < .005f && Mathf.Abs(pose.Position.y - local.y) < .005f,
                            "entire transition stays on existing spline " + i);
                    }
                    Check(Vector2.Distance(session.CurrentAxes, original) < .001f, "normal exit returns game axes");
                    actor.transform.rotation = Quaternion.Euler(0, 110, 0);
                    actor.transform.position = new Vector3(2, .5f, 3);
                    session.Evaluate(rig, solver, original, .5f, 1, timing);
                    Check(session.SelectedCandidate == 1, "choice remains locked after player moves");
                    actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    profile.Candidates[0].Height = 100;
                    solver.TryCandidate(rig, actor.transform, profile.Candidates[0], out var highAxes, out var high);
                    Check(highAxes.y >= .999f && high.Position.y <= 3.001f, "height above orbit clamps");
                    profile.Candidates[0].Height = -100;
                    solver.TryCandidate(rig, actor.transform, profile.Candidates[0], out var lowAxes, out var low);
                    Check(lowAxes.y <= .001f && low.Position.y >= -.001f, "height below orbit clamps");
                    profile.Candidates[0].Height = 1.8f;
                    Check(Mathf.Abs(Mathf.Abs(CameraShotOrbitSolver.BlendAxes(rig, new Vector2(170, .5f), new Vector2(-170, .5f), .5f).x) - 180) < .001f, "wrapped yaw takes shortest path");
                    rig.m_XAxis.m_Wrap = false; rig.m_XAxis.m_MinValue = -60; rig.m_XAxis.m_MaxValue = 60;
                    var bounded = CameraShotOrbitSolver.BlendAxes(rig, new Vector2(-50, -1), new Vector2(50, 2), .5f);
                    Check(Mathf.Abs(bounded.x) < .001f && bounded.y >= 0 && bounded.y <= 1, "non-wrapped axes obey limits");
                    rig.m_XAxis.m_Wrap = true; rig.m_XAxis.m_MinValue = -180; rig.m_XAxis.m_MaxValue = 180;
                    timing.GetDurations(out float enter, out float exit);
                    Check(Mathf.Abs(enter - .2f) < .0001f && Mathf.Abs(exit - .3f) < .0001f, "point node preserves configured seconds");
                    Check(Vector2.Distance(new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value), original) < .001f, "sampling leaves source axes unchanged");

                    using (var preview = new CameraShotPreviewRig()) {
                        int sampleCount = 0;
                        void Move(float t) { sampleCount++; actor.transform.position = new Vector3(t * 2, 0, t); }
                        var first = preview.Evaluate(profile, timing, rig, actor.transform, .55f, .1f, 1, Move);
                        Check(Mathf.Abs(first.FieldOfView - rig.m_Lens.FieldOfView) < .001f,
                            "node preview inherits the gameplay field of view");
                        int sampledAtFirstFrame = sampleCount;
                        preview.Evaluate(profile, timing, rig, actor.transform, .55f, .1f, 1, Move);
                        Check(sampleCount == sampledAtFirstFrame, "unchanged preview time does not resample animation");
                        preview.Evaluate(profile, timing, rig, actor.transform, .6f, .1f, 1, Move);
                        Check(sampleCount - sampledAtFirstFrame <= 5, "forward preview reuses previous simulation");
                        int selected = preview.SelectedCandidate;
                        preview.Evaluate(profile, timing, rig, actor.transform, .2f, .1f, 1, Move);
                        var repeated = preview.Evaluate(profile, timing, rig, actor.transform, .55f, .1f, 1, Move);
                        Check(Vector3.Distance(first.Position, repeated.Position) < .002f && Quaternion.Angle(first.Rotation, repeated.Rotation) < .05f,
                            "backward seek and replay preserve output");
                        Check(selected == preview.SelectedCandidate, "replay preserves nearest choice");
                    }
                    actor.transform.position = Vector3.zero;
                    root.SetActive(true);
                    CameraShotRuntime.Reset();
                    int handle = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing);
                    CameraShotRuntime.SetProgress(handle, .5f);
                    bool active = CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver,
                        out var runtimeAxes, out bool locked, out int candidate, out _);
                    Check(active && locked && candidate == 0, "runtime selection and input lock");
                    int other = CameraShotRuntime.Begin(profile, actor.transform, 1, 718, timing: timing);
                    CameraShotRuntime.Complete(other);
                    Check(CameraShotRuntime.ActiveCount == 1, "other channel completion preserves this shot");
                    CameraShotRuntime.Release(handle);
                    CameraShotRuntime.Evaluate(717, rig, original, runtimeAxes, right.Position, solver, out var released, out _, out _, out _);
                    Check(Vector2.Distance(runtimeAxes, released) < .1f, "interruption starts from current orbit");
                    CameraShotRuntime.Complete(handle);
                    Check(!CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out _, out _, out _, out _), "normal completion leaves no extra tail");
                    int disabled = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing);
                    actor.SetActive(false);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out _, out _, out _, out _);
                    Check(CameraShotRuntime.ActiveCount == 0, "disabled actor cleanup");
                    actor.SetActive(true);
                    var kept = new CameraShotSession();
                    kept.Capture(profile, actor.transform, rig, original, left.Position, solver);
                    var keptAxes = kept.Evaluate(rig, solver, original, 1, 1, timing, false);
                    Check(Vector2.Distance(keptAxes, leftAxes) < .001f, "no-return session retains the selected orbit");
                    using (var preview = new CameraShotPreviewRig()) {
                        preview.Evaluate(profile, timing, rig, actor.transform, 1.3f, .1f, .5f, returnToOriginalCamera: false);
                        solver.TryCandidate(rig, actor.transform, profile.Candidates[preview.SelectedCandidate], out var finalAxes, out _);
                        Check(Vector2.Distance(preview.Axes, finalAxes) < .001f, "preview retains final orbit after completion");
                        Check(Vector2.Distance(new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value), original) < .001f,
                            "no-return preview does not serialize scene axes");
                    }
                    int noReturn = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing, returnToOriginalCamera: false);
                    CameraShotRuntime.SetProgress(noReturn, .5f);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out var keptRuntimeAxes, out _, out _, out _);
                    CameraShotRuntime.Complete(noReturn);
                    Check(CameraShotRuntime.TryTakeHandoff(717, out var adopted) && Vector2.Distance(adopted, keptRuntimeAxes) < .001f,
                        "normal no-return completion hands off current axes");
                    Check(!CameraShotRuntime.TryTakeHandoff(717, out _), "handoff is consumed once");
                    noReturn = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing, returnToOriginalCamera: false);
                    CameraShotRuntime.SetProgress(noReturn, .1f);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out var interruptedAxes, out _, out _, out _);
                    CameraShotRuntime.Release(noReturn);
                    Check(CameraShotRuntime.ActiveCount == 0 && CameraShotRuntime.TryTakeHandoff(717, out adopted) && Vector2.Distance(adopted, interruptedAxes) < .001f,
                        "no-return interruption keeps current position without a return tail");
                    noReturn = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing, returnToOriginalCamera: false);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out _, out _, out _, out _);
                    int newer = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing);
                    CameraShotRuntime.Complete(noReturn);
                    Check(!CameraShotRuntime.TryTakeHandoff(717, out _), "old shot cannot override a newer camera owner");
                    CameraShotRuntime.Complete(newer);
                    // Backdate the private clock to exercise real-time progression without sleeps.
                    void Age(int id, double seconds) {
                        var entries = (System.Collections.IDictionary)typeof(CameraShotRuntime)
                            .GetField("shots", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                        var shot = entries[id];
                        shot.GetType().GetField("Started").SetValue(shot, Time.realtimeSinceStartupAsDouble - seconds);
                    }
                    int timed = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing, ignoreHitStop: true);
                    Age(timed, .4);
                    CameraShotRuntime.SetProgress(timed, 0);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out var timedAxes, out _, out _, out _);
                    Check(Vector2.Distance(timedAxes, rightAxes) < .001f, "real-time shot advances while action time is frozen");
                    Age(timed, .85);
                    CameraShotRuntime.Evaluate(717, rig, original, timedAxes, right.Position, solver, out var timedExit, out _, out _, out _);
                    Check(Vector2.Distance(timedExit, rightAxes) > .01f && Vector2.Distance(timedExit, original) > .01f,
                        "real-time exit continues while action time is frozen");
                    Age(timed, 1.1);
                    CameraShotRuntime.Evaluate(717, rig, original, timedExit, right.Position, solver, out var timedEnd, out _, out _, out _);
                    Check(CameraShotRuntime.ActiveCount == 0 && Vector2.Distance(timedEnd, original) < .001f,
                        "real-time shot renders return endpoint and completes independently");
                    timed = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing,
                        returnToOriginalCamera: false, ignoreHitStop: true);
                    Age(timed, 1.1);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out timedEnd, out _, out _, out _);
                    Check(CameraShotRuntime.ActiveCount == 0 && CameraShotRuntime.TryTakeHandoff(717, out adopted) &&
                        Vector2.Distance(adopted, rightAxes) < .001f, "real-time no-return completion hands off final orbit");
                    timed = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing);
                    Age(timed, 1.1); CameraShotRuntime.SetProgress(timed, 0);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out timedAxes, out _, out _, out _);
                    Check(CameraShotRuntime.ActiveCount == 1 && Vector2.Distance(timedAxes, original) < .001f,
                        "default clock remains paused with action time");
                    CameraShotRuntime.Complete(timed);
                    timed = CameraShotRuntime.Begin(profile, actor.transform, 1, 717, timing: timing, ignoreHitStop: true);
                    Age(timed, 1.1); CameraShotRuntime.SetPreview(timed, 0);
                    CameraShotRuntime.Evaluate(717, rig, original, original, right.Position, solver, out timedAxes, out _, out _, out _);
                    Check(CameraShotRuntime.ActiveCount == 1 && Vector2.Distance(timedAxes, original) < .001f,
                        "editor scrubbing keeps its explicit preview time");
                    CameraShotRuntime.Complete(timed);
                    var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.SetParent(root.transform);
                    obstacle.transform.position = Vector3.Lerp(focus.transform.position, right.Position, .5f);
                    obstacle.transform.localScale = new Vector3(1.2f, 3, 1.2f);
                    var collider = rig.gameObject.AddComponent<CinemachineCollider>();
                    collider.m_CollideAgainst = 1; collider.m_CameraRadius = .15f;
                    collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
                    collider.m_Damping = collider.m_DampingWhenOccluded = 0;
                    EditorUtility.SetDirty(rig); Physics.SyncTransforms();
                    solver.TryCandidate(rig, actor.transform, profile.Candidates[0], out _, out var blocked);
                    Check(Vector3.Distance(blocked.Position, focus.transform.position) < Vector3.Distance(right.Position, focus.transform.position) - .2f,
                        "source collider retracts the shot spring arm");
                    var extension = rig.gameObject.AddComponent<CinemachineCameraShotExtension>();
                    rig.m_XAxis.m_MaxSpeed = 75; rig.m_YAxis.m_MaxSpeed = 2;
                    typeof(CinemachineCameraShotExtension).GetMethod("SetInputLocked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(extension, new object[] { true });
                    extension.AdoptOrbit(rightAxes, blocked);
                    extension.enabled = false;
                    Check(Vector2.Distance(new Vector2(rig.m_XAxis.Value, rig.m_YAxis.Value), rightAxes) < .001f,
                        "restoring input preserves the adopted gameplay orbit");
                    Check(Mathf.Abs(rig.m_XAxis.m_MaxSpeed - 75) < .001f && Mathf.Abs(rig.m_YAxis.m_MaxSpeed - 2) < .001f,
                        "handoff preserves original input speed settings");
                }
                checks += CameraShotPointValidation.Run();
                string result = $"PASS: CameraShot point nodes {checks} checks";
                Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/CameraShotValidation.txt", result); Debug.Log(result);
            }
            catch (Exception exception) {
                Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/CameraShotValidation.txt", "FAIL: " + exception);
                Debug.LogException(exception); throw;
            }
            finally { CameraShotRuntime.Reset(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(profile); }
        }
    }
}
// Validation requests are consumed after the next script reload.
