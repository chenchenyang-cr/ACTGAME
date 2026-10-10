using System.Globalization;
using TMPro;
using UnityEngine;

namespace UnityLearning.UI
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        private RectTransform root;
        private CanvasGroup group;
        private DamageNumberProfile profile;
        private Transform target;
        private Vector3 lastTargetPosition;
        private Vector3 anchor;
        private Vector2 initialOffset;
        private DamageNumberSample sample;
        private float elapsed;
        private float styleScale;
        private int seed;
        private Material ownedMaterial;
        private TMP_FontAsset fallbackFont;
        private Color baseFaceColor;
        private float baseFaceDilate;
        private static readonly int FaceGlowId = Shader.PropertyToID("_FaceGlowStrength");
        private static readonly int SoftGlowId = Shader.PropertyToID("_SoftGlowStrength");

        public DamageNumberProfile Profile => profile;
        public float Elapsed => elapsed;
        public Vector3 WorldAnchor => anchor;

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (root == null) root = (RectTransform)transform;
            if (group == null) group = GetComponent<CanvasGroup>();
            if (text == null) text = GetComponentInChildren<TextMeshProUGUI>(true);
            if (fallbackFont == null && text != null)
                fallbackFont = text.font != null ? text.font : TMP_Settings.defaultFontAsset;
        }

        public void Play(DamageNumberProfile settings, float damage, bool critical,
            Vector3 worldAnchor, Transform followTarget, Vector2 offset, int randomSeed)
        {
            CacheComponents();
            profile = settings;
            anchor = worldAnchor;
            target = settings.FollowTarget ? followTarget : null;
            lastTargetPosition = target != null ? target.position : Vector3.zero;
            initialOffset = offset;
            seed = randomSeed;
            elapsed = 0f;
            styleScale = critical ? Mathf.Max(0f, settings.CriticalScale) : 1f;
            TMP_FontAsset font = settings.Font != null ? settings.Font : fallbackFont;
            if (font != null && text.font != font)
            {
                ReleaseMaterial();
                text.font = font;
            }
            Shader shader = text.font != null ? text.font.material.shader : null;
            if (settings.EnableGlow && text.font != null)
            {
                // Retains the whole-glyph shader and its build variants, preserving the font atlas.
                var template = Resources.Load<Material>("DamageNumberGlow");
                if (template != null) shader = template.shader;
            }
            if (ownedMaterial != null && ownedMaterial.shader != shader) ReleaseMaterial();
            if (ownedMaterial == null && text.font != null)
            {
                ownedMaterial = new Material(text.font.material);
                if (shader != null) ownedMaterial.shader = shader;
                baseFaceColor = text.font.material.GetColor(ShaderUtilities.ID_FaceColor);
                baseFaceDilate = text.font.material.GetFloat(ShaderUtilities.ID_FaceDilate);
                text.fontSharedMaterial = ownedMaterial;
            }
            if (ownedMaterial != null)
            {
                ownedMaterial.EnableKeyword("OUTLINE_ON");
                ownedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, settings.OutlineColor);
                ownedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, settings.OutlineWidth);
                ownedMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, Mathf.Clamp(baseFaceDilate + settings.FontWeight, -1f, 1f));
                if (ownedMaterial.HasProperty(ShaderUtilities.ID_GlowColor))
                {
                    if (settings.EnableGlow) ownedMaterial.EnableKeyword("GLOW_ON");
                    else ownedMaterial.DisableKeyword("GLOW_ON");
                    ownedMaterial.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
                    ownedMaterial.SetFloat(ShaderUtilities.ID_GlowInner, .05f);
                    ownedMaterial.SetFloat(ShaderUtilities.ID_GlowOuter, Mathf.Clamp01(settings.GlowOuter));
                    ownedMaterial.SetFloat(ShaderUtilities.ID_GlowPower, .75f);
                }
                text.UpdateMeshPadding();
            }
            text.fontSize = Mathf.Max(1f, settings.FontSize);
            Color bottomColor = critical ? settings.CriticalBottomColor : settings.BottomColor;
            Color topColor = critical ? settings.CriticalTopColor : settings.TopColor;
            text.enableVertexGradient = settings.EnableColorGradient;
            // White tint prevents TMP from multiplying the lower color into every corner.
            text.color = settings.EnableColorGradient ? Color.white : critical ? settings.CriticalColor : settings.Color;
            text.colorGradientPreset = null;
            text.colorGradient = new VertexGradient(topColor, topColor, bottomColor, bottomColor);
            int decimals = Mathf.Clamp(settings.DecimalPlaces, 0, 3);
            float displayed = decimals == 0 ? Mathf.Ceil(damage) : damage;
            text.text = displayed.ToString("F" + decimals, CultureInfo.InvariantCulture);
            text.raycastTarget = false;
            group.blocksRaycasts = group.interactable = false;
            root.localScale = Vector3.one;
            gameObject.SetActive(true);
            SetTime(0f);
        }

        public bool Advance(float deltaTime)
        {
            if (profile == null) return false;
            SetTime(elapsed + Mathf.Max(0f, deltaTime));
            return elapsed < profile.Lifetime;
        }

        public void SetTime(float seconds)
        {
            if (profile == null) return;
            elapsed = Mathf.Clamp(seconds, 0f, profile.Lifetime);
            sample = profile.Evaluate(elapsed, seed);
            // Only the text scales; the root owns position and shake in reference pixels.
            text.rectTransform.localScale = Vector3.one * (sample.Scale * styleScale);
            group.alpha = sample.Alpha;
            if (ownedMaterial != null)
            {
                ownedMaterial.SetColor(ShaderUtilities.ID_FaceColor, baseFaceColor);
                if (ownedMaterial.HasProperty(FaceGlowId))
                {
                    ownedMaterial.SetFloat(FaceGlowId, sample.Glow * Mathf.Max(0f, profile.FaceGlowIntensity));
                    ownedMaterial.SetFloat(SoftGlowId, sample.Glow * Mathf.Max(0f, profile.SoftGlowIntensity));
                }
                if (ownedMaterial.HasProperty(ShaderUtilities.ID_GlowColor))
                {
                    ownedMaterial.SetColor(ShaderUtilities.ID_GlowColor, profile.GlowColor);
                }
            }
        }

        public void Project(Camera camera, RectTransform canvasRect)
        {
            if (target != null)
            {
                anchor += target.position - lastTargetPosition;
                lastTargetPosition = target.position;
            }
            if (camera == null || canvasRect == null)
            {
                group.alpha = 0f;
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(anchor);
            bool visible = screen.z >= camera.nearClipPlane &&
                           screen.z <= camera.farClipPlane && camera.pixelRect.Contains(screen);
            group.alpha = visible ? sample.Alpha : 0f;
            Canvas ownerCanvas = canvasRect.GetComponent<Canvas>();
            Camera uiCamera = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? ownerCanvas.worldCamera : null;
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, uiCamera, out Vector2 local))
                PlaceInCanvas(local);
        }

        public void PlaceInCanvas(Vector2 anchorPosition)
        {
            CacheComponents();
            root.anchoredPosition = anchorPosition + initialOffset + sample.Offset;
        }

        public void ResetForPool()
        {
            profile = null;
            target = null;
            elapsed = 0f;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => ReleaseMaterial();

        private void ReleaseMaterial()
        {
            if (ownedMaterial == null) return;
            if (Application.isPlaying) Destroy(ownedMaterial);
            else DestroyImmediate(ownedMaterial);
            ownedMaterial = null;
        }
    }
}
