using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace CombatCamera
{
    public struct CameraShotPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public static CameraShotPose FromState(CameraState state) => new CameraShotPose {
            Position = state.CorrectedPosition, Rotation = state.CorrectedOrientation,
            FieldOfView = state.Lens.FieldOfView
        };
    }

    // Shared by runtime and the action editor. Only axis values are interpolated:
    // every sampled pose comes from the original FreeLook pipeline and orbit spline.
    public sealed class CameraShotSession
    {
        public int SelectedCandidate { get; private set; } = -1;
        public Vector2 EntryAxes { get; private set; }
        public Vector2 CurrentAxes { get; private set; }
        private CameraShotProfile profile;
        private Transform actor;

        public bool Capture(CameraShotProfile config, Transform target, CinemachineFreeLook rig,
            Vector2 entry, Vector3 currentPosition, CameraShotOrbitSolver solver, int forcedCandidate = -1)
        {
            profile = config; actor = target; EntryAxes = CurrentAxes = entry; SelectedCandidate = -1;
            if (config == null || config.Candidates == null || target == null || rig == null) return false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < config.Candidates.Count; i++) {
                if (forcedCandidate >= 0 && forcedCandidate != i) continue;
                if (!solver.TryCandidate(rig, target, config.Candidates[i], out var axes, out var pose)) continue;
                float distance = (pose.Position - currentPosition).sqrMagnitude;
                if (distance < best) { best = distance; SelectedCandidate = i; }
            }
            return SelectedCandidate >= 0;
        }

        public Vector2 Evaluate(CinemachineFreeLook rig, CameraShotOrbitSolver solver,
            Vector2 gameAxes, float seconds, float duration, CameraShotTiming timing, bool returnToOriginalCamera = true)
        {
            if (profile == null || actor == null || SelectedCandidate < 0 ||
                SelectedCandidate >= profile.Candidates.Count ||
                !solver.TryCandidate(rig, actor, profile.Candidates[SelectedCandidate], out var target, out _))
                return CurrentAxes = gameAxes;
            timing.GetDurations(out float enter, out float exit);
            if (returnToOriginalCamera && exit > 0 && seconds >= duration - exit)
                return CurrentAxes = CameraShotOrbitSolver.BlendAxes(rig, target, gameAxes,
                    CameraShotTiming.EvaluateCurve(timing.ExitCurve, (seconds - duration + exit) / exit));
            return CurrentAxes = CameraShotOrbitSolver.BlendAxes(rig, EntryAxes, target,
                enter <= 0 ? 1 : CameraShotTiming.EvaluateCurve(timing.EnterCurve, seconds / enter));
        }
    }

    public static class CameraShotRuntime
    {
        private sealed class Shot
        {
            public int Handle, Channel;
            public CameraShotProfile Profile;
            public Transform Actor;
            public Behaviour Owner;
            public bool HadOwner, Preview, Releasing, Captured, ReturnToOriginalCamera, IgnoreHitStop;
            public float Duration, Seconds;
            public double Released, Started;
            public Vector2 ReleaseAxes;
            public Vector2 PreviewAxes;
            public CameraShotPose? PreviewPose;
            public int PreviewCandidate;
            public CameraShotTiming Timing;
            public readonly CameraShotSession Session = new CameraShotSession();
        }
        private static readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();
        private static readonly List<int> expired = new List<int>();
        private static readonly Dictionary<int, int> outputOwners = new Dictionary<int, int>();
        private static readonly Dictionary<int, Vector2> handoffs = new Dictionary<int, Vector2>();
        private static int nextHandle;
        public static int ActiveCount => shots.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { shots.Clear(); expired.Clear(); outputOwners.Clear(); handoffs.Clear(); nextHandle = 0; }

        public static int Begin(CameraShotProfile profile, Transform actor, float duration,
            int channel = 0, Behaviour lifetimeOwner = null, CameraShotTiming timing = default,
            bool returnToOriginalCamera = true, bool ignoreHitStop = false)
        {
            if (profile == null || actor == null || duration <= 0) return 0;
            var shot = new Shot { Handle = ++nextHandle, Profile = profile, Actor = actor,
                Duration = duration, Channel = channel, Owner = lifetimeOwner,
                HadOwner = lifetimeOwner != null, Timing = timing, ReturnToOriginalCamera = returnToOriginalCamera,
                IgnoreHitStop = ignoreHitStop, Started = Time.realtimeSinceStartupAsDouble };
            shots.Add(shot.Handle, shot);
            // Capture at the event start, before the next camera update or player turn.
            var extension = CinemachineCameraShotExtension.FindActive(channel);
            if (extension != null) shot.Captured = extension.Capture(shot.Session, profile, actor);
            return shot.Handle;
        }

        public static void SetProgress(int handle, float normalizedTime)
        {
            if (shots.TryGetValue(handle, out var shot) && !shot.IgnoreHitStop)
                shot.Seconds = Mathf.Clamp01(normalizedTime) * shot.Duration;
        }

        public static void SetPreview(int handle, float normalizedTime)
        {
            if (!shots.TryGetValue(handle, out var shot)) return;
            shot.Preview = true; shot.Seconds = Mathf.Clamp01(normalizedTime) * shot.Duration;
        }

        public static void SetPreviewOutput(int handle, Vector2 axes, CameraShotPose pose, int candidate)
        {
            if (!shots.TryGetValue(handle, out var shot)) return;
            shot.PreviewAxes = axes; shot.PreviewPose = pose; shot.PreviewCandidate = candidate;
        }

        public static void Release(int handle, bool immediate = false)
        {
            if (!shots.TryGetValue(handle, out var shot)) return;
            if (immediate || shot.Preview || shot.Profile == null || !shot.Captured || shot.Timing.ExitSeconds <= 0) {
                if (!immediate) QueueHandoff(shot);
                shots.Remove(handle); return;
            }
            if (!shot.ReturnToOriginalCamera) { QueueHandoff(shot); shots.Remove(handle); return; }
            if (shot.Releasing) return;
            shot.ReleaseAxes = shot.Session.CurrentAxes;
            shot.Released = Time.realtimeSinceStartupAsDouble; shot.Releasing = true;
        }

        public static void Complete(int handle)
        {
            if (!shots.TryGetValue(handle, out var shot)) return;
            QueueHandoff(shot); shots.Remove(handle);
        }

        private static void QueueHandoff(Shot shot)
        {
            if (shot.Profile == null || shot.ReturnToOriginalCamera || shot.Preview || !shot.Captured ||
                !outputOwners.TryGetValue(shot.Channel, out int owner) || owner != shot.Handle) return;
            // A lower shot ending must not change the base orbit underneath a newer owner.
            foreach (var other in shots.Values)
                if (other.Channel == shot.Channel && other.Handle != shot.Handle && other.Profile != null &&
                    (other.Profile.Priority > shot.Profile.Priority ||
                    (other.Profile.Priority == shot.Profile.Priority && other.Handle > shot.Handle))) return;
            handoffs[shot.Channel] = shot.Session.CurrentAxes;
        }

        public static bool TryTakeHandoff(int channel, out Vector2 axes)
        {
            if (!handoffs.TryGetValue(channel, out axes)) return false;
            handoffs.Remove(channel); return true;
        }

        public static bool Evaluate(int channel, CinemachineFreeLook rig, Vector2 gameAxes,
            Vector2 currentAxes, Vector3 currentPosition, CameraShotOrbitSolver solver,
            out Vector2 axes, out bool lockInput, out int candidate, out CameraShotPose? previewPose)
        {
            axes = gameAxes; lockInput = false; candidate = -1; previewPose = null;
            Shot selected = null;
            expired.Clear();
            foreach (var shot in shots.Values) {
                bool ownerGone = shot.Actor == null || !shot.Actor.gameObject.activeInHierarchy ||
                    (shot.HadOwner && (shot.Owner == null || !shot.Owner.isActiveAndEnabled));
                if (shot.Profile == null || ownerGone ||
                    (shot.Releasing && Time.realtimeSinceStartupAsDouble - shot.Released >= shot.Timing.ExitSeconds)) {
                    if (shot.Profile != null) QueueHandoff(shot);
                    expired.Add(shot.Handle); continue;
                }
                if (shot.Channel != channel) continue;
                if (selected == null || shot.Profile.Priority > selected.Profile.Priority ||
                    (shot.Profile.Priority == selected.Profile.Priority && shot.Handle > selected.Handle)) selected = shot;
            }
            foreach (int handle in expired) shots.Remove(handle);
            if (selected == null) { outputOwners.Remove(channel); return false; }
            outputOwners[channel] = selected.Handle;
            if (selected.Preview && selected.PreviewPose.HasValue) {
                axes = selected.PreviewAxes; previewPose = selected.PreviewPose;
                candidate = selected.PreviewCandidate; return true;
            }
            if (!selected.Captured) selected.Captured = selected.Session.Capture(selected.Profile,
                selected.Actor, rig, currentAxes, currentPosition, solver);
            if (!selected.Captured) return false;
            if (selected.IgnoreHitStop && !selected.Preview && !selected.Releasing)
                selected.Seconds = Mathf.Clamp((float)(Time.realtimeSinceStartupAsDouble - selected.Started), 0, selected.Duration);
            if (selected.Releasing) {
                float progress = (float)(Time.realtimeSinceStartupAsDouble - selected.Released) / selected.Timing.ExitSeconds;
                axes = CameraShotOrbitSolver.BlendAxes(rig, selected.ReleaseAxes, gameAxes,
                    CameraShotTiming.EvaluateCurve(selected.Timing.ExitCurve, progress));
            }
            else axes = selected.Session.Evaluate(rig, solver, gameAxes, selected.Seconds, selected.Duration, selected.Timing, selected.ReturnToOriginalCamera);
            candidate = selected.Session.SelectedCandidate; lockInput = selected.Profile.LockCameraInput;
            // Render the exact endpoint before handing control back on the next camera update.
            // This also records the final orbit for nodes with return disabled.
            if (selected.IgnoreHitStop && !selected.Preview && !selected.Releasing && selected.Seconds >= selected.Duration)
                Complete(selected.Handle);
            return true;
        }
    }
}
