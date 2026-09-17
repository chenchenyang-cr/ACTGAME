using UnityEngine;

namespace CombatCamera
{
    public enum CameraShotAnchor { Actor, Target, Midpoint }
    public enum CameraShotFrame { ActorFacing, TowardTarget, CameraFacing }
    public enum CameraShotComposition { ActorAnchored, GameCameraRelative, ActorStartPose }

    [CreateAssetMenu(menuName = "Combat Camera/Camera Shot Profile", fileName = "CameraShotProfile")]
    public sealed class CameraShotProfile : ScriptableObject
    {
        [Header("Target Camera")]
        public CameraShotComposition Composition;
        public Vector3 TargetPosition = new Vector3(.4f, 1.55f, -3f);
        public Vector3 TargetRotation = new Vector3(6f, 0f, 0f);
        [Tooltip("Offset from the normal game camera, in its local axes. Zero keeps its position.")]
        public Vector3 CameraPositionOffset;
        [Tooltip("Local pitch, yaw and roll relative to the normal game camera. Zero keeps its angle.")]
        public Vector3 CameraRotationOffset;
        [Header("Composition")]
        public CameraShotAnchor PositionAnchor = CameraShotAnchor.Actor;
        public CameraShotAnchor LookAtAnchor = CameraShotAnchor.Actor;
        public CameraShotFrame ReferenceFrame = CameraShotFrame.CameraFacing;
        [Tooltip("Metres in the reference frame: X right, Y up, Z forward.")]
        public Vector3 PositionOffset = new Vector3(0.4f, 1.5f, -3f);
        public Vector3 LookAtOffset = new Vector3(0f, 1.2f, 0f);
        public bool FollowPosition = true;
        public bool FollowRotation;

        [Header("Motion During Shot")]
        public Vector3 PositionTravel;
        public AnimationCurve MotionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Range(-45f, 45f)] public float Roll;
        public bool OverrideFieldOfView = true;
        [Range(1f, 179f)] public float FieldOfView = 50f;

        [Header("Blending and Priority")]
        [Tooltip("X: progress through the Camera Shot track (0–1). Y: shot weight (0 = game camera, 1 = target composition).")]
        public AnimationCurve WeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.2f, 1f),
            new Keyframe(.75f, 1f), new Keyframe(1f, 0f));
        public float EvaluateWeight(float progress) => WeightCurve != null && WeightCurve.length > 0
            ? Mathf.Clamp01(WeightCurve.Evaluate(Mathf.Clamp01(progress))) : 0f;
        // Last maximum marks the end of the hold, including flat plateaus.
        public bool TryGetReturnBlend(float progress, out float start, out float blend)
        {
            start = blend = 0f;
            float peak = 0f;
            for (int i = 0; i <= 128; i++)
            {
                float t = i / 128f, weight = EvaluateWeight(t);
                if (weight >= peak) { peak = weight; start = t; }
            }
            if (peak <= .0001f || EvaluateWeight(1f) >= peak - .0001f || progress < start) return false;
            blend = Mathf.Clamp01(1f - EvaluateWeight(progress) / peak);
            return true;
        }
        public int Priority = 10;
        [Min(0f)] public float BlendIn = 0.12f;
        [Min(0f)] public float BlendOut = 0.2f;
        public bool UseUnscaledTime = true;
        [Tooltip("Freeze FreeLook orbit input until the shot has blended out.")]
        public bool LockCameraInput = true;

        [Header("Obstacle Avoidance")]
        public bool AvoidObstacles = true;
        [Tooltip("Only scenery layers; exclude character colliders.")]
        public LayerMask ObstacleLayers = 1 << 3;
        [Min(0.01f)] public float CollisionRadius = 0.15f;
    }
}
