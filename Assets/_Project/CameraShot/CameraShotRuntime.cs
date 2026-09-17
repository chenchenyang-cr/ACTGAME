using System.Collections.Generic;
using UnityEngine;

namespace CombatCamera
{
    public struct CameraShotPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
    }

    // Independent of the action editor. Each caller owns a handle; ending one
    // shot never restores stale camera state or cancels another caller's shot.
    public static class CameraShotRuntime
    {
        private sealed class Shot
        {
            public int Handle, Channel;
            public CameraShotProfile Profile;
            public Transform Actor, Target;
            public Behaviour LifetimeOwner;
            public bool HadLifetimeOwner, Releasing, Preview, Initialized, UnscaledTime, ProgressDriven;
            public double Started, Released;
            public float Duration, Progress, ReleaseWeight, PreviewWeight;
            public Vector3 AnchorPosition, LookPosition;
            public Quaternion Frame;
            public Quaternion ActorStartFrame;
            public bool HasActorStartFrame;
            public CameraShotPose LastPose;
        }

        private static readonly Dictionary<int, Shot> shots = new();
        private static readonly List<Shot> ordered = new();
        private static readonly List<int> expired = new();
        private static int nextHandle;
        public static int ActiveCount => shots.Count;

        public static bool TryGetTimelineReturn(int channel, CameraShotPose basis,
            out int handle, out CameraShotPose from, out float blend)
        {
            handle = 0; from = default; blend = 0;
            Shot selected = null;
            foreach (var candidate in shots.Values)
            {
                if (candidate.Channel != channel || candidate.Profile == null || candidate.Actor == null) continue;
                // Another shot owns the underlying composition; do not replace it with FreeLook.
                if (selected != null) return false;
                selected = candidate;
            }
            if (selected == null || (!selected.ProgressDriven && !selected.Preview) ||
                !selected.Profile.TryGetReturnBlend(selected.Progress, out float start, out blend)) return false;
            float progress = selected.Progress;
            selected.Progress = start;
            bool valid = TryPose(selected, basis.Position, basis.Rotation, basis.FieldOfView, out var target);
            selected.Progress = progress;
            if (!valid) return false;
            float weight = selected.Profile.EvaluateWeight(start);
            from = new CameraShotPose {
                Position = Vector3.Lerp(basis.Position, target.Position, weight),
                Rotation = Quaternion.Slerp(basis.Rotation, target.Rotation, weight),
                FieldOfView = Mathf.Lerp(basis.FieldOfView, target.FieldOfView, weight)
            };
            handle = selected.Handle;
            return true;
        }

        public static bool TryGetExit(int channel, out int handle, out float remaining, out bool unscaled)
        {
            Shot top = null;
            handle = 0; remaining = 0; unscaled = true;
            foreach (var shot in shots.Values)
            {
                if (shot.Channel != channel || shot.Profile == null || shot.Preview) continue;
                // A lower live shot still owns the camera; don't hand control back yet.
                if (!shot.Releasing) return false;
                remaining = Mathf.Max(remaining, shot.Profile.BlendOut - (float)(Now(shot.UnscaledTime) - shot.Released));
                if (top == null || shot.Profile.Priority > top.Profile.Priority ||
                    (shot.Profile.Priority == top.Profile.Priority && shot.Handle > top.Handle)) top = shot;
            }
            if (top == null) return false;
            handle = top.Handle; unscaled = top.UnscaledTime;
            return true;
        }

        // An isolated evaluator for authoring: same pose calculation as gameplay,
        // without registering a live shot or changing any scene camera.
        public sealed class PreviewSession
        {
            private readonly Shot shot = new Shot();
            public Quaternion Frame => shot.Frame;
            public Vector3 PositionAnchor => shot.AnchorPosition;
            public Vector3 LookAnchor => shot.LookPosition;
            public Vector3 LookAt => shot.LookPosition + shot.Frame * shot.Profile.LookAtOffset;
            public void Reset() { shot.Initialized = false; shot.HasActorStartFrame = false; }
            public void CaptureStart(CameraShotProfile profile, Transform actor, Transform target,
                Vector3 actorPosition, Vector3 actorForward, Vector3? targetPosition, Quaternion cameraRotation)
            {
                shot.Profile = profile; shot.Actor = actor; shot.Target = target;
                shot.AnchorPosition = AnchorPositionFor(actorPosition, targetPosition, profile.PositionAnchor);
                shot.LookPosition = AnchorPositionFor(actorPosition, targetPosition, profile.LookAtAnchor);
                shot.Frame = ReferenceRotation(profile, actorPosition, actorForward, targetPosition, cameraRotation);
                shot.ActorStartFrame = ActorFrame(actorForward);
                shot.HasActorStartFrame = true;
                shot.Initialized = true;
            }
            public CameraShotPose Evaluate(CameraShotProfile profile, Transform actor, Transform target,
                Quaternion initialCameraRotation, float baseFov, float progress, Vector3 basePosition = default)
            {
                if (shot.Profile != profile || shot.Actor != actor || shot.Target != target) Reset();
                shot.Profile = profile; shot.Actor = actor; shot.Target = target;
                shot.Progress = Mathf.Clamp01(progress);
                if (profile == null || actor == null) return default;
                TryPose(shot, basePosition, initialCameraRotation, baseFov, out var pose);
                return pose;
            }
            public Vector3 ToPositionOffset(Vector3 worldPosition) =>
                Quaternion.Inverse(Frame) * (worldPosition - PositionAnchor);
            public Vector3 ToLookOffset(Vector3 worldPosition) =>
                Quaternion.Inverse(Frame) * (worldPosition - LookAnchor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { shots.Clear(); ordered.Clear(); expired.Clear(); }

        /// <param name="duration">Seconds held before blend-out. Negative means explicit Release.</param>
        public static int Begin(CameraShotProfile profile, Transform actor, Transform target = null,
            float duration = -1f, int channel = 0, Behaviour lifetimeOwner = null)
        {
            if (profile == null || actor == null) return 0;
            int handle = ++nextHandle;
            shots.Add(handle, new Shot { Handle = handle, Channel = channel, Profile = profile,
                Actor = actor, Target = target, Duration = duration, Started = Now(profile.UseUnscaledTime),
                UnscaledTime = profile.UseUnscaledTime,
                ActorStartFrame = ActorFrame(actor.forward), HasActorStartFrame = true,
                LifetimeOwner = lifetimeOwner, HadLifetimeOwner = lifetimeOwner != null });
            return handle;
        }

        public static void SetProgress(int handle, float normalizedTime)
        {
            if (shots.TryGetValue(handle, out Shot shot))
            {
                shot.ProgressDriven = true;
                shot.Progress = Mathf.Clamp01(normalizedTime);
            }
        }

        // Deterministic timeline scrubbing: no accumulated editor time and no exit tail.
        public static void SetPreview(int handle, float normalizedTime, float weight)
        {
            if (!shots.TryGetValue(handle, out Shot shot)) return;
            shot.Preview = true;
            shot.Progress = Mathf.Clamp01(normalizedTime);
            shot.PreviewWeight = Mathf.Clamp01(weight);
        }

        public static void Release(int handle, bool immediate = false)
        {
            if (!shots.TryGetValue(handle, out Shot shot)) return;
            if (immediate || shot.Preview || shot.ProgressDriven || shot.Profile == null)
            { shots.Remove(handle); return; }
            BeginRelease(shot, Now(shot.UnscaledTime));
        }

        private static double Now(bool unscaled) => unscaled
            ? Time.realtimeSinceStartupAsDouble : Time.timeAsDouble;

        private static float EntryWeight(Shot shot, double now) => shot.Profile.BlendIn <= 0f
            ? 1f : Mathf.SmoothStep(0f, 1f, (float)(now - shot.Started) / shot.Profile.BlendIn);

        private static void BeginRelease(Shot shot, double now)
        {
            if (shot.Releasing) return;
            shot.ReleaseWeight = EntryWeight(shot, now);
            shot.Released = now;
            shot.Releasing = true;
        }

        private static float Weight(Shot shot, double now)
        {
            if (shot.Preview) return shot.PreviewWeight;
            if (shot.ProgressDriven) return shot.Profile.EvaluateWeight(shot.Progress);
            if (!shot.Releasing) return EntryWeight(shot, now);
            if (shot.Profile.BlendOut <= 0f) return 0f;
            return shot.ReleaseWeight * (1f - Mathf.SmoothStep(0f, 1f,
                (float)(now - shot.Released) / shot.Profile.BlendOut));
        }

        public static bool Evaluate(int channel, Vector3 basePosition, Quaternion baseRotation,
            float baseFov, out CameraShotPose pose, out bool lockInput)
        {
            pose = new CameraShotPose { Position = basePosition, Rotation = baseRotation, FieldOfView = baseFov };
            lockInput = false;
            ordered.Clear(); expired.Clear();
            foreach (Shot shot in shots.Values)
            {
                if (shot.Profile == null) { expired.Add(shot.Handle); continue; }
                double now = Now(shot.UnscaledTime);
                bool ownerGone = shot.Actor == null || !shot.Actor.gameObject.activeInHierarchy ||
                    (shot.HadLifetimeOwner && (shot.LifetimeOwner == null || !shot.LifetimeOwner.isActiveAndEnabled));
                if (ownerGone && (shot.Preview || shot.ProgressDriven)) { expired.Add(shot.Handle); continue; }
                if (ownerGone) BeginRelease(shot, now);
                if (!shot.Preview && !shot.ProgressDriven && shot.Duration >= 0f)
                {
                    shot.Progress = shot.Duration > 0f ? Mathf.Clamp01((float)(now - shot.Started) / shot.Duration) : 1f;
                    if (now >= shot.Started + shot.Duration) BeginRelease(shot, shot.Started + shot.Duration);
                }
                if (shot.Releasing && Weight(shot, now) <= 0f) { expired.Add(shot.Handle); continue; }
                if (shot.Channel == channel) ordered.Add(shot);
            }
            foreach (int handle in expired) shots.Remove(handle);
            // Lower priority blends beneath higher priority; newest wins ties.
            ordered.Sort((a, b) => a.Profile.Priority != b.Profile.Priority
                ? a.Profile.Priority.CompareTo(b.Profile.Priority) : a.Handle.CompareTo(b.Handle));
            bool active = false;
            foreach (Shot shot in ordered)
            {
                float weight = Weight(shot, Now(shot.UnscaledTime));
                if (!TryPose(shot, basePosition, baseRotation, baseFov, out CameraShotPose target)) continue;
                active = true;
                lockInput |= shot.Profile.LockCameraInput;
                pose.Position = Vector3.Lerp(pose.Position, target.Position, weight);
                pose.Rotation = Quaternion.Slerp(pose.Rotation, target.Rotation, weight);
                if (shot.Profile.OverrideFieldOfView || shot.Profile.Composition == CameraShotComposition.ActorStartPose)
                    pose.FieldOfView = Mathf.Lerp(pose.FieldOfView, target.FieldOfView, weight);
            }
            return active;
        }

        private static Vector3 Anchor(Shot shot, CameraShotAnchor mode)
        {
            return AnchorPositionFor(shot.Actor.position, shot.Target != null ? shot.Target.position : (Vector3?)null, mode);
        }

        private static Vector3 AnchorPositionFor(Vector3 actor, Vector3? target, CameraShotAnchor mode) =>
            !target.HasValue || mode == CameraShotAnchor.Actor ? actor :
            mode == CameraShotAnchor.Target ? target.Value : (actor + target.Value) * .5f;

        private static Quaternion ReferenceRotation(CameraShotProfile p, Vector3 position, Vector3 actorForward,
            Vector3? target, Quaternion cameraRotation)
        {
            Vector3 forward = p.ReferenceFrame == CameraShotFrame.CameraFacing ? cameraRotation * Vector3.forward : actorForward;
            if (p.ReferenceFrame == CameraShotFrame.TowardTarget && target.HasValue) forward = target.Value - position;
            forward.y = 0;
            if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        private static Quaternion ActorFrame(Vector3 forward)
        {
            forward.y = 0;
            return Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : Vector3.forward, Vector3.up);
        }

        private static bool TryPose(Shot shot, Vector3 cameraPosition, Quaternion cameraRotation, float baseFov, out CameraShotPose pose)
        {
            pose = shot.LastPose;
            if (shot.Actor == null) return shot.Initialized;
            CameraShotProfile p = shot.Profile;
            if (p.Composition == CameraShotComposition.ActorStartPose)
            {
                if (!shot.HasActorStartFrame)
                {
                    shot.ActorStartFrame = ActorFrame(shot.Actor.forward);
                    shot.HasActorStartFrame = true;
                }
                shot.Frame = shot.ActorStartFrame;
                shot.AnchorPosition = shot.Actor.position;
                float amount = p.MotionCurve != null ? p.MotionCurve.Evaluate(shot.Progress) : shot.Progress;
                Vector3 destination = shot.AnchorPosition + shot.Frame * (p.TargetPosition + p.PositionTravel * amount);
                Vector3 origin = shot.Actor.position + Vector3.up * 1.2f;
                Vector3 displacement = destination - origin;
                if (p.AvoidObstacles && displacement.sqrMagnitude > .0001f &&
                    Physics.SphereCast(origin, Mathf.Max(.01f, p.CollisionRadius), displacement.normalized,
                        out RaycastHit obstacle, displacement.magnitude, p.ObstacleLayers, QueryTriggerInteraction.Ignore))
                    destination = origin + displacement.normalized * Mathf.Max(.01f, obstacle.distance - .02f);
                pose = new CameraShotPose { Position = destination,
                    Rotation = shot.Frame * Quaternion.Euler(p.TargetRotation),
                    FieldOfView = Mathf.Clamp(p.FieldOfView, 1, 179) };
                shot.Initialized = true; shot.LastPose = pose;
                return true;
            }
            if (p.Composition == CameraShotComposition.GameCameraRelative)
            {
                // Base pose is Cinemachine's normal output before any Shot or shake.
                // Always derive from it, never from last frame's modified camera.
                shot.AnchorPosition = cameraPosition;
                shot.Frame = cameraRotation;
                float amount = p.MotionCurve != null ? p.MotionCurve.Evaluate(shot.Progress) : shot.Progress;
                Vector3 displacement = cameraRotation * (p.CameraPositionOffset + p.PositionTravel * amount);
                Vector3 finalPosition = cameraPosition + displacement;
                if (p.AvoidObstacles && displacement.sqrMagnitude > .0001f &&
                    Physics.SphereCast(cameraPosition, Mathf.Max(.01f, p.CollisionRadius), displacement.normalized,
                        out RaycastHit obstruction, displacement.magnitude, p.ObstacleLayers, QueryTriggerInteraction.Ignore))
                    finalPosition = cameraPosition + displacement.normalized * Mathf.Max(0, obstruction.distance - .02f);
                pose = new CameraShotPose {
                    Position = finalPosition,
                    Rotation = cameraRotation * Quaternion.Euler(p.CameraRotationOffset),
                    FieldOfView = p.OverrideFieldOfView ? Mathf.Clamp(p.FieldOfView, 1, 179) : baseFov
                };
                shot.Initialized = true; shot.LastPose = pose;
                return true;
            }
            if (!shot.Initialized || p.FollowPosition)
            {
                shot.AnchorPosition = Anchor(shot, p.PositionAnchor);
                shot.LookPosition = Anchor(shot, p.LookAtAnchor);
            }
            if (!shot.Initialized || p.FollowRotation)
            {
                shot.Frame = ReferenceRotation(p, shot.Actor.position, shot.Actor.forward,
                    shot.Target != null ? shot.Target.position : (Vector3?)null, cameraRotation);
            }
            float motion = p.MotionCurve != null ? p.MotionCurve.Evaluate(shot.Progress) : shot.Progress;
            Vector3 position = shot.AnchorPosition + shot.Frame * (p.PositionOffset + p.PositionTravel * motion);
            Vector3 lookAt = shot.LookPosition + shot.Frame * p.LookAtOffset;
            Vector3 ray = position - lookAt;
            if (p.AvoidObstacles && ray.sqrMagnitude > 0.0001f &&
                Physics.SphereCast(lookAt, Mathf.Max(0.01f, p.CollisionRadius), ray.normalized,
                    out RaycastHit hit, ray.magnitude, p.ObstacleLayers, QueryTriggerInteraction.Ignore))
                position = lookAt + ray.normalized * Mathf.Max(0.01f, hit.distance - 0.02f);
            Vector3 aim = lookAt - position;
            pose = new CameraShotPose { Position = position,
                Rotation = (aim.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aim, Vector3.up) : cameraRotation) *
                    Quaternion.Euler(0f, 0f, p.Roll),
                FieldOfView = p.OverrideFieldOfView ? Mathf.Clamp(p.FieldOfView, 1f, 179f) : baseFov };
            shot.Initialized = true;
            shot.LastPose = pose;
            return true;
        }
    }
}
