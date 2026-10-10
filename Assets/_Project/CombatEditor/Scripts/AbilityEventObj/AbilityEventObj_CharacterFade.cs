using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "动作事件/人物淡入淡出")]
    public sealed class AbilityEventObj_CharacterFade : AbilityEventObj
    {
        public CharacterNode.NodeType TargetNode = CharacterNode.NodeType.Animator;
        public bool IncludeChildren = true;
        [Tooltip("同时处理武器挂点，适用于武器不在角色挂点子树中的情况。")]
        public bool IncludeWeapon = true;
        [Tooltip("横轴为事件区间；纵轴 0 为不可见，1 为完全可见。")]
        [MyAnimationCurve] public AnimationCurve Visibility = new(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f));

        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_CharacterFade(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_CharacterFade(this);
        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_CharacterFade : AbilityEventEffect
    {
        private CharacterFadeBinding binding;
        private AbilityEventObj_CharacterFade Config => (AbilityEventObj_CharacterFade)_EventObj;
        public AbilityEventEffect_CharacterFade(AbilityEventObj obj) : base(obj) { }
        public override void StartEffect()
        {
            base.StartEffect();
            binding = new CharacterFadeBinding(_combatController, Config);
            binding.Apply(0f);
        }
        public override void EffectRunning(float time) => binding?.Apply(
            Mathf.InverseLerp(eve.GetEventStartTime(), eve.GetEventEndTime(), time));
        public override void EndEffect()
        {
            binding?.Dispose();
            binding = null;
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_CharacterFade : AbilityEventPreview
    {
        private CharacterFadeBinding binding;
        private AbilityEventObj_CharacterFade Config => (AbilityEventObj_CharacterFade)_EventObj;
        public AbilityEventPreview_CharacterFade(AbilityEventObj obj) : base(obj) { }
        public override void PreviewRunning(float time)
        {
            if (Application.isPlaying || eve == null || !eve.Previewable || !Config.IsActive ||
                time < StartTimePercentage || time >= EndTimePercentage)
            { Release(); return; }
            binding ??= new CharacterFadeBinding(_combatController, Config);
            binding.Apply(Mathf.InverseLerp(StartTimePercentage, EndTimePercentage, time));
            UnityEditor.SceneView.RepaintAll();
        }
        public override void BackToStart() => Release();
        public override void DestroyPreview() => Release();
        private void Release() { binding?.Dispose(); binding = null; }
    }
#endif

    /// <summary>Temporary transparent copies preserve source material assets and support overlapping fade tracks.</summary>
    internal sealed class CharacterFadeBinding : IDisposable
    {
        private sealed class FadedMaterial
        {
            public Material Copy;
            public string ColorProperty;
            public Color BaseColor;
            public bool Toon;
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] FadeMaterials;
            public FadedMaterial[] Parts;
            public ShadowCastingMode OriginalShadowMode;
            public bool OriginalForceOff;
            public readonly List<CharacterFadeBinding> Owners = new();
            public bool Fading;
        }

        private static readonly Dictionary<Renderer, RendererState> Active = new();
        private readonly List<RendererState> states = new();
        private readonly AbilityEventObj_CharacterFade config;
        private float visibility = 1f;

        internal static float GetVisibility(Renderer renderer)
        {
            if (renderer == null || !Active.TryGetValue(renderer, out RendererState state)) return 1f;
            float result = 1f;
            foreach (CharacterFadeBinding owner in state.Owners)
                result = Mathf.Min(result, owner.visibility);
            return result;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterReloadCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RestoreAllEditorStates;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += RestoreAllEditorStates;
        }

        private static void RestoreAllEditorStates()
        {
            foreach (RendererState state in Active.Values) RestoreState(state);
            Active.Clear();
        }
#endif

        public CharacterFadeBinding(CombatController controller, AbilityEventObj_CharacterFade settings)
        {
            config = settings;
            if (controller == null) return;
            Transform root = controller.GetNodeTranform(config.TargetNode);
            Transform weapon = config.IncludeWeapon
                ? controller.GetNodeTranform(CharacterNode.NodeType.Weapon) : null;
            var found = new HashSet<Renderer>();
            AddRoot(root, config.IncludeChildren, found);
            AddRoot(weapon, true, found);
        }

        private void AddRoot(Transform root, bool includeChildren, HashSet<Renderer> found)
        {
            if (root == null) return;
            Renderer[] renderers = includeChildren
                ? root.GetComponentsInChildren<Renderer>(true) : root.GetComponents<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !found.Add(renderer) || renderer.gameObject.name.StartsWith("[Character Echo]", StringComparison.Ordinal))
                    continue;
                if (!Active.TryGetValue(renderer, out RendererState state))
                {
                    state = CreateState(renderer);
                    if (state == null) continue;
                    Active.Add(renderer, state);
                }
                state.Owners.Add(this);
                states.Add(state);
            }
        }

        private static RendererState CreateState(Renderer renderer)
        {
            Material[] original = renderer.sharedMaterials;
            if (original.Length == 0) return null;
            var parts = new FadedMaterial[original.Length];
            var fadeMaterials = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                Material source = original[i];
                if (source == null) continue;
                bool toon = source.shader != null && source.shader.name == "Ellen/ZZZToon";
                bool urp = source.HasProperty("_Surface") && source.HasProperty("_BaseColor") &&
                           source.HasProperty("_SrcBlend") && source.HasProperty("_DstBlend") &&
                           source.HasProperty("_ZWrite");
                if (!toon && !urp)
                {
                    Debug.LogWarning($"人物淡入淡出：材质 {source.name} 不支持透明混合。", renderer);
                    fadeMaterials[i] = source;
                    continue;
                }
                var copy = new Material(source) { name = source.name + " [Character Fade]",
                    hideFlags = HideFlags.HideAndDontSave };
                copy.renderQueue = (int)RenderQueue.Transparent;
                copy.SetOverrideTag("RenderType", "Transparent");
                if (toon)
                {
                    copy.SetFloat("_FadeSrcBlend", (float)BlendMode.SrcAlpha);
                    copy.SetFloat("_FadeDstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    copy.SetFloat("_FadeZWrite", 0f);
                }
                else
                {
                    copy.SetFloat("_Surface", 1f);
                    copy.SetFloat("_Blend", 0f);
                    copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    copy.SetFloat("_ZWrite", 0f);
                    copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                }
                string colorProperty = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                parts[i] = new FadedMaterial
                {
                    Copy = copy, ColorProperty = colorProperty,
                    BaseColor = source.HasProperty(colorProperty) ? source.GetColor(colorProperty) : Color.white,
                    Toon = toon
                };
                fadeMaterials[i] = copy;
            }
            return new RendererState
            {
                Renderer = renderer, OriginalMaterials = original, FadeMaterials = fadeMaterials,
                Parts = parts, OriginalShadowMode = renderer.shadowCastingMode,
                OriginalForceOff = renderer.forceRenderingOff
            };
        }

        public void Apply(float normalizedTime)
        {
            visibility = Mathf.Clamp01(AbilityEventObj_CharacterEcho.Evaluate(
                config.Visibility, normalizedTime, 1f));
            foreach (RendererState state in states) UpdateState(state);
        }

        private static void UpdateState(RendererState state)
        {
            if (state.Renderer == null) return;
            float opacity = 1f;
            foreach (CharacterFadeBinding owner in state.Owners)
                opacity = Mathf.Min(opacity, owner.visibility);
            bool fading = opacity < 0.9999f;
            if (state.Fading != fading)
            {
                state.Renderer.sharedMaterials = fading ? state.FadeMaterials : state.OriginalMaterials;
                state.Renderer.shadowCastingMode = fading ? ShadowCastingMode.Off : state.OriginalShadowMode;
                state.Fading = fading;
            }
            state.Renderer.forceRenderingOff = state.OriginalForceOff || opacity <= 0.001f;
            if (!fading) return;
            foreach (FadedMaterial part in state.Parts)
            {
                if (part == null) continue;
                if (part.Toon) part.Copy.SetFloat("_CharacterFade", opacity);
                else
                {
                    Color color = part.BaseColor;
                    color.a *= opacity;
                    part.Copy.SetColor(part.ColorProperty, color);
                }
            }
        }

        public void Dispose()
        {
            foreach (RendererState state in states)
            {
                state.Owners.Remove(this);
                if (state.Owners.Count > 0) { UpdateState(state); continue; }
                RestoreState(state);
                Active.Remove(state.Renderer);
            }
            states.Clear();
        }

        private static void RestoreState(RendererState state)
        {
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.OriginalMaterials;
                state.Renderer.shadowCastingMode = state.OriginalShadowMode;
                state.Renderer.forceRenderingOff = state.OriginalForceOff;
            }
            foreach (FadedMaterial part in state.Parts)
            {
                if (part == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(part.Copy);
                else UnityEngine.Object.DestroyImmediate(part.Copy);
            }
        }
    }
}
