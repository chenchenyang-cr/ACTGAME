using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CombatPostFX
{
    public sealed class CombatPostFxRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
        [SerializeField] private Shader shader;

        private CombatPostFxPass _pass;
        private Material _material;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/Combat/PostFX");

            if (shader != null && (_material == null || _material.shader != shader))
                _material = CoreUtils.CreateEngineMaterial(shader);

            _pass = new CombatPostFxPass(_material)
            {
                renderPassEvent = injectionPoint
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null)
                Create();

            if (!ShouldRender(in renderingData))
                return;

            renderer.EnqueuePass(_pass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (!ShouldRender(in renderingData))
                return;

            // URP creates the camera targets after AddRenderPasses. Accessing the handle any earlier
            // produces a lifecycle error and can reference a target from the previous camera.
            _pass.SetTarget(renderer.cameraColorTargetHandle);
        }

        private bool ShouldRender(in RenderingData renderingData)
        {
            return _material != null && _pass != null && CombatPostFxRuntime.Current.IsVisible &&
                   renderingData.cameraData.cameraType != CameraType.Preview &&
                   renderingData.cameraData.renderType != CameraRenderType.Overlay;
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
        }

        private sealed class CombatPostFxPass : ScriptableRenderPass
        {
            private static readonly int Lens0Id = Shader.PropertyToID("_CombatFxLens0");
            private static readonly int VignetteId = Shader.PropertyToID("_CombatFxVignette");
            private static readonly int StyleId = Shader.PropertyToID("_CombatFxStyle");
            private static readonly int GlitchId = Shader.PropertyToID("_CombatFxGlitch");
            private static readonly int GrainId = Shader.PropertyToID("_CombatFxGrain");
            private static readonly int GrainSpeedId = Shader.PropertyToID("_CombatFxGrainSpeed");
            private static readonly int FlashColorId = Shader.PropertyToID("_CombatFxFlashColor");
            private static readonly int VignetteColorId = Shader.PropertyToID("_CombatFxVignetteColor");
            private static readonly int TintColorId = Shader.PropertyToID("_CombatFxTintColor");
            private static readonly int CenterId = Shader.PropertyToID("_CombatFxCenter");

            private readonly Material _material;
            private RTHandle _source;
            private RTHandle _temporary;

            public CombatPostFxPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("Combat Post FX");
            }

            public void SetTarget(RTHandle source)
            {
                _source = source;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _temporary, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_CombatPostFxTexture");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _source == null)
                    return;

                CombatPostFxSettings fx = CombatPostFxRuntime.Current;
                _material.SetVector(Lens0Id, new Vector4(fx.radialBlur, fx.radialBlurDistance,
                    fx.chromaticAberration, fx.chromaticSpread));
                _material.SetVector(VignetteId, new Vector4(fx.vignette, fx.vignetteInner,
                    fx.vignetteOuter, 0f));
                _material.SetVector(StyleId, new Vector4(fx.flash, fx.desaturation,
                    fx.tintStrength, fx.glitch));
                _material.SetVector(GlitchId, new Vector4(fx.glitchSpeed, fx.glitchDensity,
                    fx.glitchDisplacement, fx.glitchChannelSplit));
                _material.SetVector(GrainId, new Vector4(fx.filmGrain, fx.filmGrainScale, 0f, 0f));
                _material.SetFloat(GrainSpeedId, fx.filmGrainSpeed);
                _material.SetColor(FlashColorId, fx.flashColor);
                _material.SetColor(VignetteColorId, fx.vignetteColor);
                _material.SetColor(TintColorId, fx.tintColor);
                _material.SetVector(CenterId, fx.center);

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, _source, _temporary, _material, 0);
                    Blitter.BlitCameraTexture(cmd, _temporary, _source);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _temporary?.Release();
            }
        }
    }
}
