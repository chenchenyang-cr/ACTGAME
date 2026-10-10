using CombatCamera;
using UnityEngine;

namespace CombatEditor
{
    [CreateAssetMenu(menuName = "Combat/Hit Profile", fileName = "HitProfile")]
    public sealed class CombatHitProfile : ScriptableObject
    {
        [Header("Damage")]
        [Min(0f)] public float Damage = 10f;
        [Min(0f)] public float PoiseDamage = 10f;
        public CombatHitReactionPolicy HitReaction =
            CombatHitReactionPolicy.FirstHitOnly;
        [Min(0f)] public float StaggerDuration;

        [Header("伤害跳字")]
        [InspectorName("禁用跳字")]
        public bool SuppressDamageNumber;
        [InspectorName("跳字预设覆盖")]
        [Tooltip("留空时使用场景跳字管理器的默认预设。")]
        public UnityLearning.UI.DamageNumberProfile DamageNumberOverride;

        [Header("Hit Rules")]
        public CombatHitMode HitMode = CombatHitMode.Single;
        [Min(1)] public int RepeatIntervalFrames = 6;
        [Tooltip("0 means unlimited until the hit-box window closes.")]
        [Min(0)] public int MaximumHitsPerTarget;
        public LayerMask TargetLayers = ~0;
        public bool AllowFriendlyFire;
        [Tooltip("Whether this attack can be deflected during a parry window.")]
        public bool CanBeParried = true;

        [Header("粒子碰撞体")]
        [InspectorName("碰撞体延迟生成帧数")]
        [Tooltip("仅用于粒子轨道。从粒子事件开始帧起按动作时间线 60 FPS 计算；0 为立即生成。动画切换后按游戏时间继续计时，并遵循粒子的受顿帧影响设置；不受粒子播放速度影响。")]
        [Min(0)] public int ParticleCollisionDelayFrames;

        [Header("命中后镜头震动")]
        [InspectorName("启用命中震动")]
        [Tooltip("确认命中后触发震动；是否等待双方顿帧结束由震动预设中的“顿帧后开始震动”控制。")]
        public bool EnableHitCameraShake;
        [InspectorName("震动预设")]
        public CameraShakeProfile HitCameraShakeProfile;

        // Backwards-compatible fallback for ability assets not migrated yet.
        [HideInInspector]
        [Min(0.01f)] public float HitCameraShakeDuration = 0.16f;
        [HideInInspector]
        public bool HitCameraShakeUseUnscaledTime = true;
        [HideInInspector]
        public CameraShakeSettings HitCameraShakeSettings = new CameraShakeSettings
        {
            Channel = CameraShakeChannel.Impact,
            EnableDirectionalImpulse = true
        };

        [Header("编辑器预览")]
        [InspectorName("预览命中震动")]
        [Tooltip("在碰撞盒区间起点模拟一次确认命中。")]
        public bool PreviewHitCameraShake;

        public CameraShakeSettings ResolveHitCameraShakeSettings()
        {
            return HitCameraShakeProfile != null
                ? HitCameraShakeProfile.Settings
                : HitCameraShakeSettings;
        }

        public float ResolveHitCameraShakeDuration()
        {
            return HitCameraShakeProfile != null
                ? HitCameraShakeProfile.Duration
                : HitCameraShakeDuration;
        }

        public bool ResolveHitCameraShakeUseUnscaledTime()
        {
            return HitCameraShakeProfile != null
                ? HitCameraShakeProfile.UseUnscaledTime
                : HitCameraShakeUseUnscaledTime;
        }

        public bool ResolveHitCameraShakeAffectedByHitStop()
        {
            return HitCameraShakeProfile != null &&
                   HitCameraShakeProfile.AffectedByHitStop;
        }

        public bool ResolveHitCameraShakeStartAfterHitStop()
        {
            return HitCameraShakeProfile != null &&
                   HitCameraShakeProfile.StartAfterHitStop;
        }

        public float ResolveHitCameraShakeHitStopWeight()
        {
            return HitCameraShakeProfile != null
                ? Mathf.Clamp01(HitCameraShakeProfile.HitStopWeight)
                : 0f;
        }

        [Header("命中顿帧")]
        [InspectorName("启用命中顿帧")]
        [Tooltip("命中确认后，攻击者立即应用顿帧，受击者延迟 1 个游戏帧应用。")]
        public bool EnableHitAnimationSpeed;
        [InspectorName("顿帧帧数")]
        [Tooltip("按动作时间线 60 FPS 换算持续时间，6 帧 = 0.1 秒。0 表示不顿帧；不受游戏帧率和动画速度影响。")]
        [Min(0)] public int HitStopFrames = 5;
        [InspectorName("顿帧动画速度")]
        [Tooltip("相对于当前动作速度的倍率：0 完全暂停，0.5 半速，1 保持原速。只在顿帧持续时间内生效。")]
        [Range(0f, 1f)] public float HitStopAnimationSpeed;
        [InspectorName("启用顿帧淡入")]
        [Tooltip("开启后，动画速度在淡入帧数内从正常速度过渡到顿帧动画速度。曲线横轴为淡入进度，纵轴为顿帧强度：0 为正常速度，1 为顿帧速度。")]
        public bool EnableHitStopFadeIn;
        [InspectorName("淡入帧数")]
        [Min(0)] public int HitStopFadeInFrames = 2;
        [InspectorName("淡入曲线")]
        public AnimationCurve HitStopFadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [InspectorName("启用顿帧淡出")]
        [Tooltip("开启后，动画速度在顿帧结束前的淡出帧数内恢复到正常速度。曲线横轴为淡出进度，纵轴为顿帧强度：1 为顿帧速度，0 为正常速度。")]
        public bool EnableHitStopFadeOut;
        [InspectorName("淡出帧数")]
        [Min(0)] public int HitStopFadeOutFrames = 2;
        [InspectorName("淡出曲线")]
        public AnimationCurve HitStopFadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Confirmed Hit VFX")]
        [Tooltip("Spawn this effect only after the hit has been accepted by the target.")]
        public bool EnableHitVfx;
        public GameObject HitVfxPrefab;
        public CombatHitVfxDirectionMode HitVfxDirection =
            CombatHitVfxDirectionMode.AttackDirection;
        [InspectorName("粒子生成位置模式")]
        public CombatHitVfxPositionMode HitVfxPositionMode = CombatHitVfxPositionMode.HitPoint;
        [InspectorName("固定位置偏移")]
        [Tooltip("命中点模式：按特效朝向计算偏移。对手位置模式：世界坐标偏移，不随特效旋转。")]
        public Vector3 HitVfxPositionOffset;
        [InspectorName("随机位置偏移幅度")]
        [Tooltip("仅对手位置模式生效。每次生成时，各世界坐标轴独立在 [-幅度, +幅度] 内随机取值；0 表示该轴不随机，负数按 0 处理。")]
        public Vector3 HitVfxRandomPositionAmplitude;
        public Vector3 HitVfxRotationOffset;
        [Min(0f)] public float HitVfxScale = 1f;
        [Tooltip("Seconds before the spawned object is destroyed. 0 automatically estimates particle duration.")]
        [Min(0f)] public float HitVfxLifetime;
        public CombatHitResultMask HitVfxResultMask =
            CombatHitResultMask.Normal | CombatHitResultMask.Critical;

    }
}
