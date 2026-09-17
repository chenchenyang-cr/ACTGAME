using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    public enum MaterialCurveValueType { Float, Color }
    public enum MaterialCurveOperation { MultiplyOriginal, Override }
    public enum MaterialCurveTargetMode
    {
        [InspectorName("自动（指定材质时查找场景，否则使用挂点）")] Auto,
        [InspectorName("角色挂点")] CharacterNode,
        [InspectorName("场景中使用目标材质的对象")] SceneMaterial
    }

    [Serializable]
    public sealed class MaterialPropertyCurve
    {
        [Tooltip("Shader 属性名，例如 _EmissionColor、_BaseColor、_Dissolve、_Intensity。")]
        public string PropertyName = "_EmissionColor";
        public MaterialCurveValueType ValueType = MaterialCurveValueType.Color;
        public MaterialCurveOperation Operation = MaterialCurveOperation.MultiplyOriginal;
        public float Value = 1f;
        [ColorUsage(true, true)] public Color Color = Color.white;
        [Tooltip("仅颜色属性生效。关闭时只改变 RGB，保留原透明度；开启后曲线也控制 Alpha。")]
        public bool AffectAlpha;
        [Tooltip("横轴为本轨道区间进度 0~1，纵轴乘以 Value。支持负数和大于 1 的值。")]
        [MyAnimationCurve]
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public float Evaluate(float time) => Value *
            (Curve == null || Curve.length == 0 ? 1f : Curve.Evaluate(Mathf.Clamp01(time)));
    }

    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents/Material Property Curve")]
    public sealed class AbilityEventObj_MaterialPropertyCurve : AbilityEventObj
    {
        public MaterialCurveTargetMode TargetMode;
        public bool UsesSceneMaterial => TargetMode == MaterialCurveTargetMode.SceneMaterial ||
            (TargetMode == MaterialCurveTargetMode.Auto && TargetMaterial != null);
        public CharacterNode.NodeType TargetNode = CharacterNode.NodeType.Weapon;
        public bool IncludeChildren = true;
        [Tooltip("自动模式：指定材质后作用于场景中所有使用它的 Renderer。角色挂点模式：仅作为挂点内的材质筛选。")]
        public Material TargetMaterial;
        [Tooltip("可在一条轨道上控制多个材质属性。默认配置为基础颜色和发光颜色的亮度倍率。")]
        public List<MaterialPropertyCurve> Properties = new()
        {
            new MaterialPropertyCurve { PropertyName = "_BaseColor" },
            new MaterialPropertyCurve { PropertyName = "_EmissionColor" }
        };
        public override EventTimeType GetEventTimeType() => EventTimeType.EventRange;
        public override AbilityEventEffect Initialize() => new AbilityEventEffect_MaterialPropertyCurve(this);
#if UNITY_EDITOR
        public override AbilityEventPreview InitializePreview() => new AbilityEventPreview_MaterialPropertyCurve(this);
        public override bool PreviewExist() => true;
#endif
    }

    public sealed class AbilityEventEffect_MaterialPropertyCurve : AbilityEventEffect
    {
        private MaterialCurveBinding binding;
        private AbilityEventObj_MaterialPropertyCurve Config => (AbilityEventObj_MaterialPropertyCurve)_EventObj;
        public AbilityEventEffect_MaterialPropertyCurve(AbilityEventObj obj) : base(obj) { }
        public override void StartEffect()
        {
            base.StartEffect();
            binding = new MaterialCurveBinding(_combatController, Config);
            binding.Apply(0f);
        }
        public override void EffectRunning(float time) => binding?.Apply(
            Mathf.InverseLerp(eve.GetEventStartTime(), eve.GetEventEndTime(), time));
        public override void EndEffect()
        {
            binding?.Dispose(); binding = null;
            base.EndEffect();
        }
    }

#if UNITY_EDITOR
    public sealed class AbilityEventPreview_MaterialPropertyCurve : AbilityEventPreview
    {
        private MaterialCurveBinding binding;
        private AbilityEventObj_MaterialPropertyCurve Config => (AbilityEventObj_MaterialPropertyCurve)_EventObj;
        public AbilityEventPreview_MaterialPropertyCurve(AbilityEventObj obj) : base(obj) { }
        public override void PreviewRunning(float time)
        {
            if (Application.isPlaying || eve == null || !eve.Previewable || !Config.IsActive ||
                time < StartTimePercentage || time >= EndTimePercentage)
            { Release(); return; }
            if (binding == null) binding = new MaterialCurveBinding(_combatController, Config);
            binding.Apply(Mathf.InverseLerp(StartTimePercentage, EndTimePercentage, time));
            UnityEditor.SceneView.RepaintAll();
        }
        public override void BackToStart() => Release();
        public void RefreshProperties(float time)
        {
            Release();
            PreviewRunning(time);
        }
        public override void DestroyPreview() => Release();
        private void Release() { binding?.Dispose(); binding = null; }
    }
#endif

    // Snapshot each material slot once. Recompose active tracks from that baseline,
    // so overlapping tracks can end in any order without leaving a stale override.
    internal sealed class MaterialCurveBinding : IDisposable
    {
        private sealed class Slot
        {
            public Renderer Renderer;
            public Material Material;
            public int Index;
            public readonly MaterialPropertyBlock Original = new();
            public readonly MaterialPropertyBlock Working = new();
            public readonly List<MaterialCurveBinding> Bindings = new();
        }
        private static readonly Dictionary<(Renderer, int), Slot> Active = new();
        private readonly List<Slot> slots = new();
        private readonly AbilityEventObj_MaterialPropertyCurve config;
        private float time;
        private static int sceneRendererFrame = -1;
        private static Renderer[] sceneRenderers;

        public MaterialCurveBinding(CombatController controller, AbilityEventObj_MaterialPropertyCurve settings)
        {
            config = settings;
            if (config.UsesSceneMaterial)
            {
                if (config.TargetMaterial == null)
                    Debug.LogWarning("Material Property Curve：场景材质模式需要指定目标材质。", settings);
                else
                    BindRenderers(GetSceneRenderers());
                return;
            }
            if (controller == null) return;
            Transform node = null;
            if (config.TargetNode == CharacterNode.NodeType.Animator) node = controller.GetNodeTranform(config.TargetNode);
            else
                foreach (CharacterNode entry in controller.Nodes)
                    if (entry != null && entry.type == config.TargetNode && entry.NodeTrans != null)
                    { node = entry.NodeTrans; break; }
            // Missing weapon nodes must not fall back to modifying the whole character.
            if (node == null)
            {
                Debug.LogWarning("Material Property Curve：请在 CombatController 中配置目标挂点。", controller);
                return;
            }
            Renderer[] renderers = config.IncludeChildren
                ? node.GetComponentsInChildren<Renderer>(true) : node.GetComponents<Renderer>();
            BindRenderers(renderers);
            if (slots.Count == 0)
                Debug.LogWarning("Material Property Curve：目标挂点下没有匹配材质属性，请检查材质、Shader 属性名和 Float/Color 类型。", controller);
        }

        private static Renderer[] GetSceneRenderers()
        {
            if (!Application.isPlaying || sceneRenderers == null || sceneRendererFrame != Time.frameCount)
            {
                sceneRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
                sceneRendererFrame = Time.frameCount;
            }
            return sceneRenderers;
        }

        private void BindRenderers(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded) continue;
#if UNITY_EDITOR
                if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(renderer.gameObject)) continue;
#endif
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || (config.TargetMaterial != null && material != config.TargetMaterial) ||
                        config.Properties == null || !config.Properties.Exists(p => Supports(material, p))) continue;
                    var key = (renderer, i);
                    if (!Active.TryGetValue(key, out Slot slot))
                    {
                        slot = new Slot { Renderer = renderer, Material = material, Index = i };
                        renderer.GetPropertyBlock(slot.Original, i);
                        Active.Add(key, slot);
                    }
                    if (!slots.Contains(slot))
                    {
                        slot.Bindings.Add(this);
                        slots.Add(slot);
                    }
                }
            }
        }

        private static bool Supports(Material material, MaterialPropertyCurve property)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.PropertyName) ||
                !material.HasProperty(property.PropertyName)) return false;
            int index = material.shader.FindPropertyIndex(property.PropertyName);
            if (index < 0) return false;
            var type = material.shader.GetPropertyType(index);
            return property.ValueType == MaterialCurveValueType.Color
                ? type == UnityEngine.Rendering.ShaderPropertyType.Color
                : type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range;
        }
        public void Apply(float normalizedTime)
        {
            time = Mathf.Clamp01(normalizedTime);
            // Also bind effects/objects spawned after this track started.
            if (config.UsesSceneMaterial && config.TargetMaterial != null)
                BindRenderers(GetSceneRenderers());
            foreach (Slot slot in slots) UpdateSlot(slot);
        }
        private static void UpdateSlot(Slot slot)
        {
            if (slot.Renderer == null || slot.Material == null) return;
            slot.Renderer.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
            slot.Renderer.GetPropertyBlock(slot.Working, slot.Index);
            if (slot.Working.isEmpty) slot.Renderer.GetPropertyBlock(slot.Working);
            foreach (MaterialCurveBinding binding in slot.Bindings)
            {
                if (binding.config.Properties == null) continue;
                foreach (MaterialPropertyCurve property in binding.config.Properties)
                {
                    if (!Supports(slot.Material, property)) continue;
                    int id = Shader.PropertyToID(property.PropertyName);
                    float value = property.Evaluate(binding.time);
                    if (property.ValueType == MaterialCurveValueType.Float)
                    {
                        float original = slot.Working.HasFloat(id) ? slot.Working.GetFloat(id) : slot.Material.GetFloat(id);
                        slot.Working.SetFloat(id, property.Operation == MaterialCurveOperation.Override ? value : original * value);
                    }
                    else
                    {
                        Color original = slot.Working.HasColor(id) ? slot.Working.GetColor(id) : slot.Material.GetColor(id);
                        Color color = property.Color * value;
                        if (property.Operation == MaterialCurveOperation.MultiplyOriginal) color *= original;
                        if (!property.AffectAlpha) color.a = original.a;
                        slot.Working.SetColor(id, color);
                    }
                }
            }
            slot.Renderer.SetPropertyBlock(slot.Working, slot.Index);
        }
        public void Dispose()
        {
            foreach (Slot slot in slots)
            {
                slot.Bindings.Remove(this);
                if (slot.Bindings.Count > 0) UpdateSlot(slot);
                else
                {
                    if (slot.Renderer != null)
                        slot.Renderer.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
                    Active.Remove((slot.Renderer, slot.Index));
                }
            }
            slots.Clear();
        }
    }
}
