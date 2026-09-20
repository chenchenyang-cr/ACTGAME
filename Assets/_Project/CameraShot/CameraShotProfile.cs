using UnityEngine;

namespace CombatCamera
{
    // Keep serialized values stable after removing the former mode at value 0.
    public enum CameraShotComposition
    {
        [InspectorName("游戏相机相对")] GameCameraRelative = 1,
        [InspectorName("角色起始朝向")] ActorStartPose = 2
    }

    [CreateAssetMenu(menuName = "Combat Camera/Camera Shot Profile", fileName = "CameraShotProfile")]
    public sealed class CameraShotProfile : ScriptableObject
    {
        [Header("Target Camera")]
        public CameraShotComposition Composition = CameraShotComposition.ActorStartPose;
        public Vector3 TargetPosition = new Vector3(.4f, 1.55f, -3f);
        public Vector3 TargetRotation = new Vector3(6f, 0f, 0f);
        [Tooltip("Offset from the normal game camera, in its local axes. Zero keeps its position.")]
        public Vector3 CameraPositionOffset;
        [Tooltip("Local pitch, yaw and roll relative to the normal game camera. Zero keeps its angle.")]
        public Vector3 CameraRotationOffset;
        [Tooltip("只将目标位置和角度调整到当前 FreeLook 可实现的机位；保留 FOV 和过渡路径。")]
        public bool LimitToGameCameraOrbit;
        [Tooltip("持续朝向玩家上身（根节点上方 1.2 米）。与轨道限制同时开启时，位置仍受限制，朝向优先看向玩家。")]
        public bool LookAtPlayer;

        [Header("Motion During Shot")]
        public Vector3 PositionTravel;
        public AnimationCurve MotionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool OverrideFieldOfView = true;
        [Range(1f, 179f)] public float FieldOfView = 50f;

        [Header("Blending and Priority")]
        [Tooltip("X: progress through the Camera Shot track (0–1). Y: shot weight (0 = game camera, 1 = target composition).")]
        public AnimationCurve WeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.2f, 1f),
            new Keyframe(.75f, 1f), new Keyframe(1f, 0f));
        public float EvaluateWeight(float progress) => WeightCurve != null && WeightCurve.length > 0
            ? Mathf.Clamp01(WeightCurve.Evaluate(Mathf.Clamp01(progress))) : 0f;
        // Explicit timing avoids interpreting a nearly flat hold as an early return.
        public bool TryGetReturnBlend(float progress, out float start, out float blend)
        {
            start = Mathf.Clamp(ReturnStart, 0f, .99f);
            blend = 0f;
            float weight = EvaluateWeight(start);
            if (progress < start || weight <= .0001f || EvaluateWeight(1f) >= weight - .0001f) return false;
            blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, 1f, progress));
            return true;
        }
        public int Priority = 10;
        [Min(0f)] public float BlendIn = 0.12f;
        [Min(0f)] public float BlendOut = 0.2f;
        public bool UseUnscaledTime = true;
        [Tooltip("Freeze FreeLook orbit input until the shot has blended out.")]
        public bool LockCameraInput = true;
        [Range(0f, .99f)] public float ReturnStart = .75f;
        [Min(0f)] public float FollowDamping = .08f;
        [Min(0f)] public float ObstacleRecovery = .15f;
        public bool OrbitBlend;

        [Header("Obstacle Avoidance")]
        public bool AvoidObstacles = true;
        [Tooltip("Only scenery layers; exclude character colliders.")]
        public LayerMask ObstacleLayers = 1 << 3;
        [Min(0.01f)] public float CollisionRadius = 0.15f;
    }
}
