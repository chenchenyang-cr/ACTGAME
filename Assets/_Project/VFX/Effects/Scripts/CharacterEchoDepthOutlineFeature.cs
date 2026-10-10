using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class CharacterEchoDepthOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    private Material material;
    private OutlinePass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("Hidden/CharacterEcho/DepthOutline");
        if (shader != null && (material == null || material.shader != shader))
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
        pass ??= new OutlinePass();
        pass.SetMaterial(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) Create();
        if (material == null || CharacterEchoVisualSession.Sessions.Count == 0 ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay ||
            (renderingData.cameraData.cameraType != CameraType.Game &&
             renderingData.cameraData.cameraType != CameraType.SceneView)) return;
        renderer.EnqueuePass(pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (material != null && pass != null)
            pass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
    }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private static readonly int DepthId = Shader.PropertyToID("_CharacterEchoDepth");
        private static readonly int ColorId = Shader.PropertyToID("_CharacterEchoColor");
        private static readonly int WidthId = Shader.PropertyToID("_CharacterEchoWidth");
        private static readonly int OpacityId = Shader.PropertyToID("_CharacterEchoOpacity");
        private readonly List<CharacterEchoOutlineGroup> groups = new();
        private Material material;
        private RTHandle source;
        private RTHandle depthColor;
        private RTHandle outlineDepthBuffer;
        private RTHandle temporary;
        private bool warnedInvalidColorFormat;

        public OutlinePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            profilingSampler = new ProfilingSampler("Character Echo Depth Outline");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void SetMaterial(Material value) => material = value;
        public void SetSource(RTHandle value) => source = value;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
            descriptor.sRGB = false;
            RenderingUtils.ReAllocateIfNeeded(ref depthColor, descriptor, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_CharacterEchoDepthColor");
            if (depthColor.rt.graphicsFormat == GraphicsFormat.None && !warnedInvalidColorFormat)
            {
                Debug.LogError("Character Echo depth color target has no color format.");
                warnedInvalidColorFormat = true;
            }

            descriptor.depthBufferBits = 24;
            descriptor.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
            descriptor.graphicsFormat = GraphicsFormat.None;
            RenderingUtils.ReAllocateIfNeeded(ref outlineDepthBuffer, descriptor, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_CharacterEchoDepthAttachment");

            descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref temporary, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_CharacterEchoOutlineComposite");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null || depthColor == null ||
                outlineDepthBuffer == null || temporary == null) return;
            groups.Clear();
            foreach (CharacterEchoVisualSession session in CharacterEchoVisualSession.Sessions)
                session.CollectOutlineGroups(groups);
            if (groups.Count == 0) return;

            CommandBuffer cmd = CommandBufferPool.Get("Character Echo Depth Outline");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                foreach (CharacterEchoOutlineGroup group in groups)
                {
                    CoreUtils.SetRenderTarget(cmd, depthColor, outlineDepthBuffer, ClearFlag.All,
                        new Color(1000000f, 0f, 0f, 1f));
                    foreach (Renderer renderer in group.Renderers)
                    {
                        if (renderer == null) continue;
                        Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh :
                            renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh == null) continue;
                        for (int i = 0; i < mesh.subMeshCount; i++)
                            cmd.DrawRenderer(renderer, material, i, 0);
                    }
                    cmd.SetGlobalTexture(DepthId, depthColor.nameID);
                    cmd.SetGlobalColor(ColorId, group.Color);
                    cmd.SetGlobalFloat(WidthId, group.WidthPixels);
                    cmd.SetGlobalFloat(OpacityId, group.Opacity);
                    Blitter.BlitCameraTexture(cmd, source, temporary, material, 1);
                    Blitter.BlitCameraTexture(cmd, temporary, source);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            depthColor?.Release();
            outlineDepthBuffer?.Release();
            temporary?.Release();
        }
    }
}
