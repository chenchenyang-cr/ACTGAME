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
        public delegate bool TargetConstraint(CameraShotPose desired, out CameraShotPose constrained);
        private sealed class Shot
        {
            public int Handle, Channel;
            public CameraShotProfile Profile;
            public Transform Actor;
            public Behaviour LifetimeOwner;
            public bool HadLifetimeOwner, Releasing, Preview, Initialized, UnscaledTime, ProgressDriven;
            public double Started, Released;
            public float Duration, Progress, ReleaseWeight, PreviewWeight;
            public double PoseUpdated;
            public Vector3 AnchorVelocity;
            public float ObstacleDistance, ObstacleVelocity;
            public bool HasObstacleDistance;
            public Vector3 AnchorPosition;
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

        public static void FinishReleases(int channel)
        {
            expired.Clear();
            foreach (var shot in shots.Values)
                if (shot.Channel == channel && shot.Releasing) expired.Add(shot.Handle);
            foreach (int handle in expired) shots.Remove(handle);
        }

        public static bool TryGetTimelineReturn(int channel, CameraShotPose basis,
            out int handle, out CameraShotPose from, out float blend, TargetConstraint constraint = null)
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
            if (selected == null || selected.Releasing ||
                (!selected.ProgressDriven && !selected.Preview) ||
                !selected.Profile.TryGetReturnBlend(selected.Progress, out float start, out blend)) return false;
            // Sampling a return origin must not advance the live follow/obstacle integrators.
            var sample = new Shot { Profile = selected.Profile, Actor = selected.Actor,
                Progress = start, Preview = true, Initialized = selected.Initialized,
                AnchorPosition = selected.AnchorPosition, Frame = selected.Frame,
                ActorStartFrame = selected.ActorStartFrame, HasActorStartFrame = selected.HasActorStartFrame };
            bool valid = TryPose(sample, basis.Position, basis.Rotation, basis.FieldOfView, out var target, constraint);
            if (!valid) return false;
            float weight = selected.Profile.EvaluateWeight(start);
            from = Blend(basis, target, weight, selected.Actor.position + Vector3.up * 1.2f,
                selected.Profile.OrbitBlend && selected.Profile.Composition != CameraShotComposition.GameCameraRelative);
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
            private readonly Shot shot = new Shot { Preview = true };
            public Quaternion Frame => shot.Frame;
            public Vector3 PositionAnchor => shot.AnchorPosition;
            public void Reset() { shot.Initialized = false; shot.HasActorStartFrame = false; }
            public void CaptureStart(CameraShotProfile profile, Transform actor, Vector3 actorPosition, Vector3 actorForward)
            {
                shot.Profile = profile; shot.Actor = actor;
                shot.AnchorPosition = actorPosition;
                shot.Frame = shot.ActorStartFrame = ActorFrame(actorForward);
                shot.HasActorStartFrame = true;
                shot.Initialized = true;
            }
            public CameraShotPose Evaluate(CameraShotProfile profile, Transform actor,
                Quaternion initialCameraRotation, float baseFov, float progress, Vector3 basePosition = default, TargetConstraint constraint = null)
            {
                if (shot.Profile != profile || shot.Actor != actor) Reset();
                shot.Profile = profile; shot.Actor = actor;
                shot.Progress = Mathf.Clamp01(progress);
                if (profile == null || actor == null) return default;
                TryPose(shot, basePosition, initialCameraRotation, baseFov, out var pose, constraint);
                return pose;
            }
            public Vector3 ToPositionOffset(Vector3 worldPosition) =>
                Quaternion.Inverse(Frame) * (worldPosition - PositionAnchor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { shots.Clear(); ordered.Clear(); expired.Clear(); }

        /// <param name="duration">Seconds held before blend-out. Negative means explicit Release.</param>
        public static int Begin(CameraShotProfile profile, Transform actor,
            float duration = -1f, int channel = 0, Behaviour lifetimeOwner = null)
        {
            if (profile == null || actor == null) return 0;
            int handle = ++nextHandle;
            shots.Add(handle, new Shot { Handle = handle, Channel = channel, Profile = profile,
                Actor = actor, Duration = duration, Started = Now(profile.UseUnscaledTime),
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
            // Timeline-driven shots still need their BlendOut tail. The caller drops
            // its handle, but the runtime owns the short release until it completes.
            if (immediate || shot.Preview || shot.Profile == null)
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
            shot.ReleaseWeight = shot.ProgressDriven
                ? shot.Profile.EvaluateWeight(shot.Progress)
                : EntryWeight(shot, now);
            shot.Released = now;
            shot.Releasing = true;
        }

        private static float Weight(Shot shot, double now)
        {
            if (shot.Preview) return shot.PreviewWeight;
            if (shot.ProgressDriven && !shot.Releasing)
                return shot.Profile.EvaluateWeight(shot.Progress);
            if (!shot.Releasing) return EntryWeight(shot, now);
            if (shot.Profile.BlendOut <= 0f) return 0f;
            return shot.ReleaseWeight * (1f - Mathf.SmoothStep(0f, 1f,
                (float)(now - shot.Released) / shot.Profile.BlendOut));
        }

        public static bool Evaluate(int channel, Vector3 basePosition, Quaternion baseRotation,
            float baseFov, out CameraShotPose pose, out bool lockInput, TargetConstraint constraint = null)
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
                if (!TryPose(shot, basePosition, baseRotation, baseFov, out CameraShotPose target, constraint)) continue;
                active = true;
                lockInput |= shot.Profile.LockCameraInput;
                if (!shot.Profile.OverrideFieldOfView && shot.Profile.Composition != CameraShotComposition.ActorStartPose)
                    target.FieldOfView = pose.FieldOfView;
                pose = Blend(pose, target, weight, shot.AnchorPosition + Vector3.up * 1.2f,
                    shot.Profile.OrbitBlend && shot.Profile.Composition != CameraShotComposition.GameCameraRelative);
            }
            return active;
        }

        private static Vector3 Follow(Vector3 current, Vector3 target, ref Vector3 velocity,
            Shot shot, float dt) => !shot.Initialized || shot.Preview || !Application.isPlaying || shot.Profile.FollowDamping <= 0f
                ? target : dt <= 0f ? current : Vector3.SmoothDamp(current, target, ref velocity, shot.Profile.FollowDamping, Mathf.Infinity, dt);

        // Follow an arc around the subject and compensate aim so the path does not
        // pull the subject sideways merely because position and rotation blend separately.
        public static CameraShotPose Blend(CameraShotPose from, CameraShotPose to, float t, Vector3 focus, bool orbit)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            var pose = new CameraShotPose { Position = Vector3.Lerp(from.Position, to.Position, t),
                Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
                FieldOfView = Mathf.Lerp(from.FieldOfView, to.FieldOfView, t) };
            Vector3 a = from.Position - focus, b = to.Position - focus;
            if (!orbit || a.sqrMagnitude < .01f || b.sqrMagnitude < .01f) return pose;
            Vector3 offset = Vector3.Slerp(a.normalized, b.normalized, t) * Mathf.Lerp(a.magnitude, b.magnitude, t);
            // Interpolate framing relative to the subject, rather than world rotations.
            Quaternion aimA = Quaternion.LookRotation(-a, Vector3.up);
            Quaternion aimB = Quaternion.LookRotation(-b, Vector3.up);
            pose.Position = focus + offset;
            pose.Rotation = Quaternion.LookRotation(-offset, Vector3.up) * Quaternion.Slerp(
                Quaternion.Inverse(aimA) * from.Rotation, Quaternion.Inverse(aimB) * to.Rotation, t);
            return pose;
        }

        private static Vector3 Avoid(Shot shot, Vector3 origin, Vector3 destination, float dt)
        {
            var p = shot.Profile;
            if (!p.AvoidObstacles) { shot.HasObstacleDistance = false; return destination; }
            Vector3 ray = destination - origin;
            float length = ray.magnitude;
            if (length < .0001f) return destination;
            float distance = length;
            bool blocked = Physics.SphereCast(origin, Mathf.Max(.01f, p.CollisionRadius), ray / length,
                out var hit, length, p.ObstacleLayers, QueryTriggerInteraction.Ignore);
            if (!blocked && !shot.HasObstacleDistance) return destination;
            if (blocked)
                distance = Mathf.Max(0f, hit.distance - .02f);
            // Retract immediately for safety; ease outward after the obstacle clears.
            if (!shot.HasObstacleDistance || shot.Preview || !Application.isPlaying ||
                distance <= shot.ObstacleDistance || p.ObstacleRecovery <= 0f)
            { shot.ObstacleDistance = distance; shot.ObstacleVelocity = 0f; }
            else if (dt > 0f) shot.ObstacleDistance = Mathf.SmoothDamp(shot.ObstacleDistance, distance,
                ref shot.ObstacleVelocity, p.ObstacleRecovery, Mathf.Infinity, dt);
            shot.HasObstacleDistance = blocked || distance - shot.ObstacleDistance > .001f;
            return origin + ray / length * Mathf.Min(distance, shot.ObstacleDistance);
        }

        private static Quaternion ActorFrame(Vector3 forward)
        {
            forward.y = 0;
            return Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : Vector3.forward, Vector3.up);
        }

        private static bool TryPose(Shot shot, Vector3 cameraPosition, Quaternion cameraRotation, float baseFov, out CameraShotPose pose, TargetConstraint constraint = null)
        {
            pose = shot.LastPose;
            if (shot.Actor == null) return shot.Initialized;
            CameraShotProfile p = shot.Profile;
            double now = Now(shot.UnscaledTime);
            float dt = shot.Initialized ? Mathf.Max(0f, (float)(now - shot.PoseUpdated)) : 0f;
            shot.PoseUpdated = now;
            if (p.Composition == CameraShotComposition.ActorStartPose)
            {
                if (!shot.HasActorStartFrame)
                {
                    shot.ActorStartFrame = ActorFrame(shot.Actor.forward);
                    shot.HasActorStartFrame = true;
                }
                shot.Frame = shot.ActorStartFrame;
                shot.AnchorPosition = Follow(shot.AnchorPosition, shot.Actor.position, ref shot.AnchorVelocity, shot, dt);
                float amount = p.MotionCurve != null ? p.MotionCurve.Evaluate(shot.Progress) : shot.Progress;
                Vector3 destination = shot.AnchorPosition + shot.Frame * (p.TargetPosition + p.PositionTravel * amount);
                Vector3 origin = shot.Actor.position + Vector3.up * 1.2f;
                if (!p.LimitToGameCameraOrbit) destination = Avoid(shot, origin, destination, dt);
                pose = new CameraShotPose { Position = destination,
                    Rotation = shot.Frame * Quaternion.Euler(p.TargetRotation),
                    FieldOfView = Mathf.Clamp(p.FieldOfView, 1, 179) };
                if (p.LimitToGameCameraOrbit)
                {
                    if (constraint == null || !constraint(pose, out var constrained)) return false;
                    constrained.FieldOfView = pose.FieldOfView;
                    pose = constrained;
                }
                if (p.LookAtPlayer)
                {
                    Vector3 direction = origin - pose.Position;
                    if (direction.sqrMagnitude > .0001f)
                    {
                        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .999f
                            ? Vector3.forward : Vector3.up;
                        pose.Rotation = Quaternion.LookRotation(direction, up);
                    }
                }
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
                finalPosition = Avoid(shot, cameraPosition, finalPosition, dt);
                pose = new CameraShotPose {
                    Position = finalPosition,
                    Rotation = cameraRotation * Quaternion.Euler(p.CameraRotationOffset),
                    FieldOfView = p.OverrideFieldOfView ? Mathf.Clamp(p.FieldOfView, 1, 179) : baseFov
                };
                shot.Initialized = true; shot.LastPose = pose;
                return true;
            }
            return false;
        }
    }
}
