using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "动作事件/角色外轮廓发光")]
    public sealed class AbilityEventObj_CharacterEcho : AbilityEventObj, IPerfectDodgeFeedbackTrack
    {
        [SerializeField, InspectorName("仅完美闪避成功时播放")]
        private bool playOnPerfectDodge;
        public bool IsPerfectDodgeFeedback => playOnPerfectDodge;
        public CharacterNode.NodeType TargetNode = CharacterNode.NodeType.Animator;
        public bool IncludeChildren = true;
        public bool EnableOutline = true;
        [ColorUsage(true, true)] public Color OutlineTint = new(0.4f, 1.5f, 3f);
        [Range(0f, 12f)] public float OutlineWidthPixels = 2.5f;
        [MyAnimationCurve] public AnimationCurve OutlineOpacity =
            AnimationCurve.Linear(0f, 0.8f, 1f, 0.8f);

        // Preserve serialized data in existing events, without exposing retired body/rate controls.
        [HideInInspector] public Material EffectMaterial;
        [HideInInspector] public bool EnableBody;
        [HideInInspector] public AnimationCurve BodyColorBlend;
        [HideInInspector] public bool EnableAfterimages;
        [HideInInspector] public float SnapshotsPerSecond;
        [HideInInspector] public int MaxSnapshots;
        [HideInInspector] public float SnapshotLifetime;
        [HideInInspector] public bool HideOriginalDuringBody;
        [HideInInspector] public Gradient BodyColor;
        [HideInInspector] public AnimationCurve BodyCoverage;
        [HideInInspector] public AnimationCurve BodyOpacity;
        [HideInInspector] public AnimationCurve BodyDissolve;
        [HideInInspector] public Gradient AfterimageColor;
        [HideInInspector] public AnimationCurve SpawnRate;
        [HideInInspector] public AnimationCurve SnapshotLifetimeMultiplier;
        [HideInInspector] public AnimationCurve SnapshotOpacity;
        [HideInInspector] public AnimationCurve SnapshotDissolve;
        [HideInInspector] public AnimationCurve FlowAmount;
        [HideInInspector] public AnimationCurve FlowSpeed;
        [HideInInspector] public AnimationCurve FlowScale;
        [HideInInspector] public AnimationCurve DissolveSoftness;

        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_CharacterEcho(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_CharacterEcho(this);
        public override bool PreviewExist() => true;
#endif
        internal static float Evaluate(AnimationCurve curve, float time, float fallback = 0f) =>
            curve == null || curve.length == 0 ? fallback : curve.Evaluate(Mathf.Clamp01(time));
    }

    public sealed class AbilityEventEffect_CharacterEcho : AbilityEventEffect
    {
        private CharacterEchoVisualSession session;
        private AbilityEventObj_CharacterEcho Config => (AbilityEventObj_CharacterEcho)_EventObj;
        public AbilityEventEffect_CharacterEcho(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            if (_combatController == null) return;
            Transform root = _combatController.GetNodeTranform(Config.TargetNode);
            if (root == null) return;
            var host = _combatController.GetComponent<CharacterEchoRuntimeHost>();
            if (host == null) host = _combatController.gameObject.AddComponent<CharacterEchoRuntimeHost>();
            session = new CharacterEchoVisualSession(root, Config.IncludeChildren, null, false);
            host.Add(session);
            session.ApplyOutline(Config, 0f);
        }

        public override void EffectRunning(float time)
        {
            if (session == null || eve == null) return;
            session.ApplyOutline(Config, Mathf.InverseLerp(eve.GetEventStartTime(),
                eve.GetEventEndTime(), time));
        }

        public override void EndEffect()
        {
            session?.Stop();
            session = null;
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CharacterEcho : AbilityEventPreview
    {
        private CharacterEchoVisualSession session;
        private Transform targetRoot;
        private bool includeChildren;
        private AbilityEventObj_CharacterEcho Config => (AbilityEventObj_CharacterEcho)_EventObj;
        public AbilityEventPreview_CharacterEcho(AbilityEventObj obj) : base(obj) { }

        public override void PreviewRunning(float time)
        {
            if (Application.isPlaying || eve == null || !eve.Previewable || !Config.IsActive ||
                !PreviewInRange(time) || _combatController == null)
            { Release(); return; }
            Transform root = _combatController.GetNodeTranform(Config.TargetNode);
            if (root == null) { Release(); return; }
            if (session != null && (root != targetRoot || Config.IncludeChildren != includeChildren)) Release();
            if (session == null)
            {
                session = new CharacterEchoVisualSession(root, Config.IncludeChildren, null, true);
                targetRoot = root;
                includeChildren = Config.IncludeChildren;
            }
            session.ApplyOutline(Config, Mathf.InverseLerp(StartTimePercentage, EndTimePercentage, time));
            UnityEditor.SceneView.RepaintAll();
        }

        public override void BackToStart() => Release();
        public override void DestroyPreview() => Release();
        private void Release()
        {
            session?.Dispose();
            session = null;
            targetRoot = null;
        }
    }
#endif

}
