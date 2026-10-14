using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Global Hit Stop")]
    public sealed class AbilityEventObj_GlobalHitStop : AbilityEventObj
    {
        [Min(1)]
        [InspectorName("顿帧时长（帧）")]
        [Tooltip("包含淡入和淡出的总时长，按动作编辑器的 60 FPS 换算为实际时间。")]
        public int DurationFrames = 3;

        [Range(0f, 1f)]
        [InspectorName("顿帧播放速度")]
        [Tooltip("0 为完全暂停，1 为原速。作用于场景中所有 Animator 和 ParticleSystem。")]
        public float Speed = 0f;

        [InspectorName("启用顿帧淡入")]
        public bool EnableFadeIn;
        [InspectorName("淡入时长（帧）")]
        [Tooltip("从原速过渡到顿帧播放速度，占用顿帧总时长。")]
        [Min(0)] public int FadeInFrames = 2;
        [InspectorName("淡入曲线")]
        [Tooltip("横轴为淡入进度；纵轴 0 为原速，1 为顿帧播放速度。")]
        public AnimationCurve FadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [InspectorName("启用顿帧淡出")]
        public bool EnableFadeOut;
        [InspectorName("淡出时长（帧）")]
        [Tooltip("从顿帧播放速度恢复到原速，占用顿帧总时长。")]
        [Min(0)] public int FadeOutFrames = 2;
        [InspectorName("淡出曲线")]
        [Tooltip("横轴为淡出进度；纵轴 1 为顿帧播放速度，0 为原速。")]
        public AnimationCurve FadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        public override EventTimeType GetEventTimeType() => EventTimeType.EventTime;

        public override AbilityEventEffect Initialize() => new AbilityEventEffect_GlobalHitStop(this);
    }

    public sealed class AbilityEventEffect_GlobalHitStop : AbilityEventEffect
    {
        public AbilityEventEffect_GlobalHitStop(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            var config = (AbilityEventObj_GlobalHitStop)_EventObj;
            GlobalHitStopRuntime.Play(config.DurationFrames, config.Speed, new HitStopFadeSettings
            {
                FadeIn = config.EnableFadeIn,
                FadeInFrames = config.FadeInFrames,
                FadeInCurve = config.FadeInCurve,
                FadeOut = config.EnableFadeOut,
                FadeOutFrames = config.FadeOutFrames,
                FadeOutCurve = config.FadeOutCurve
            });
        }
    }
}
