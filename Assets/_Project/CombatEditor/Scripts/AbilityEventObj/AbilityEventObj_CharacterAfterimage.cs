using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "动作事件/单次角色残影")]
    public sealed class AbilityEventObj_CharacterAfterimage : AbilityEventObj, IPerfectDodgeFeedbackTrack
    {
        [SerializeField, InspectorName("仅完美闪避成功时播放")]
        private bool playOnPerfectDodge;
        public bool IsPerfectDodgeFeedback => playOnPerfectDodge;
        public CharacterNode.NodeType TargetNode = CharacterNode.NodeType.Animator;
        public bool IncludeChildren = true;
        [Tooltip("武器不在角色挂点子树中时，也捕获武器挂点下的模型。")]
        public bool IncludeWeapon = true;
        [Tooltip("留空时使用 CharacterEchoURP 模板材质。")]
        public Material EffectMaterial;
        [Min(0.02f)] public float Lifetime = 0.25f;

        internal Material ResolveMaterial()
        {
            Material material = EffectMaterial != null
                ? EffectMaterial : Resources.Load<Material>("CharacterEchoURP");
            return material != null && material.HasProperty("_EffectAlpha") ? material : null;
        }

        public override EventTimeType GetEventTimeType() => EventTimeType.EventTime;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_CharacterAfterimage(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_CharacterAfterimage(this);
        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_CharacterAfterimage : AbilityEventEffect
    {
        private CharacterEchoVisualSession session;
        private AbilityEventObj_CharacterAfterimage Config => (AbilityEventObj_CharacterAfterimage)_EventObj;
        public AbilityEventEffect_CharacterAfterimage(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            if (_combatController == null) return;
            Transform root = _combatController.GetNodeTranform(Config.TargetNode);
            Material material = Config.ResolveMaterial();
            if (root == null || material == null)
            {
                Debug.LogWarning("单次角色残影需要有效目标和支持 _EffectAlpha 的 LTY 风格材质。", Config);
                return;
            }
            Transform weapon = Config.IncludeWeapon
                ? _combatController.GetNodeTranform(CharacterNode.NodeType.Weapon) : null;
            var host = _combatController.GetComponent<CharacterEchoRuntimeHost>();
            if (host == null) host = _combatController.gameObject.AddComponent<CharacterEchoRuntimeHost>();
            session = new CharacterEchoVisualSession(new[] { root, weapon }, Config.IncludeChildren,
                material, false);
            host.Add(session);
            session.CaptureGhost(Config.Lifetime, 0f);
        }

        public override void EndEffect()
        {
            session?.Stop();
            session = null;
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CharacterAfterimage : AbilityEventPreview
    {
        private CharacterEchoVisualSession session;
        public System.Action<float> SamplePose;
        private AbilityEventObj_CharacterAfterimage Config => (AbilityEventObj_CharacterAfterimage)_EventObj;
        public AbilityEventPreview_CharacterAfterimage(AbilityEventObj obj) : base(obj) { }

        public override void PreviewRunning(float time)
        {
            if (Application.isPlaying || eve == null || !eve.Previewable || !Config.IsActive ||
                _combatController == null || AnimObj == null || AnimObj.Clip == null ||
                time < StartTimePercentage)
            { Release(); return; }
            float age = (time - StartTimePercentage) * AnimObj.Clip.length;
            if (age >= Mathf.Max(0.02f, Config.Lifetime)) { Release(); return; }
            Transform root = _combatController.GetNodeTranform(Config.TargetNode);
            Material material = Config.ResolveMaterial();
            if (root == null || material == null) { Release(); return; }
            if (session == null)
            {
                Transform weapon = Config.IncludeWeapon
                    ? _combatController.GetNodeTranform(CharacterNode.NodeType.Weapon) : null;
                session = new CharacterEchoVisualSession(new[] { root, weapon },
                    Config.IncludeChildren, material, true);
            }
            session.ClearGhosts();
            try
            {
                SamplePose?.Invoke(StartTimePercentage);
                session.CaptureGhost(Config.Lifetime, age);
            }
            finally { SamplePose?.Invoke(time); }
            UnityEditor.SceneView.RepaintAll();
        }

        public override void BackToStart() => Release();
        public override void DestroyPreview() => Release();
        private void Release() { session?.Dispose(); session = null; }
    }
#endif

}
