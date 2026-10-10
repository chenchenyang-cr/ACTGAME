using System;
using System.Collections.Generic;
using CombatEditor;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct CharacterEchoOutlineGroup
{
    public readonly IReadOnlyList<Renderer> Renderers;
    public readonly Color Color;
    public readonly float WidthPixels;
    public readonly float Opacity;

    public CharacterEchoOutlineGroup(IReadOnlyList<Renderer> renderers, Color color, float widthPixels, float opacity)
    {
        Renderers = renderers;
        Color = color;
        WidthPixels = widthPixels;
        Opacity = opacity;
    }
}

/// <summary>Owns an outline track or one point-triggered pose afterimage.</summary>
public sealed class CharacterEchoVisualSession : IDisposable
{
    private sealed class Source
    {
        public Renderer Original;
        public SkinnedMeshRenderer Skin;
        public Mesh Mesh;
    }

    private sealed class GhostPiece
    {
        public GameObject Object;
        public Mesh Mesh;
        public bool OwnsMesh;
        public Renderer Body;
    }

    private sealed class Ghost
    {
        public readonly List<GhostPiece> Pieces = new();
        public float Age;
        public float Lifetime;
    }

    private static readonly List<CharacterEchoVisualSession> ActiveSessions = new();
    private static readonly int EffectAlpha = Shader.PropertyToID("_EffectAlpha");
    private static readonly int UseDissolve = Shader.PropertyToID("_use_disslove");
    private static readonly int DissolveSwitch = Shader.PropertyToID("_DissSC");
    private static readonly int DissolvePower = Shader.PropertyToID("_Disspower");
    private static readonly int DissolveChannel = Shader.PropertyToID("_DissovleA_R");
    private readonly List<Source> sources = new();
    private readonly List<Renderer> outlineRenderers = new();
    private readonly List<Ghost> ghosts = new();
    private readonly Stack<Ghost> pool = new();
    private readonly MaterialPropertyBlock block = new();
    private readonly Material material;
    private readonly bool preview;
    private bool stopped;
    private bool outlineActive;
    private Color outlineColor;
    private float outlineWidth;
    private float outlineAlpha;

    public bool IsStopped => stopped;
    public bool HasGhosts => ghosts.Count > 0;
    public static IReadOnlyList<CharacterEchoVisualSession> Sessions => ActiveSessions;

    public CharacterEchoVisualSession(Transform root, bool includeChildren, Material material, bool preview)
        : this(new[] { root }, includeChildren, material, preview) { }

    public CharacterEchoVisualSession(IEnumerable<Transform> roots, bool includeChildren, Material material, bool preview)
    {
        this.material = material;
        this.preview = preview;
        var seen = new HashSet<Renderer>();
        foreach (Transform root in roots)
        {
            if (root == null) continue;
            Renderer[] renderers = includeChildren
                ? root.GetComponentsInChildren<Renderer>(true)
                : root.GetComponents<Renderer>();
            foreach (Renderer original in renderers)
            {
                if (original == null || !seen.Add(original) || !original.enabled ||
                    !original.gameObject.activeInHierarchy ||
                    original.gameObject.name.StartsWith("[Character Echo]", StringComparison.Ordinal)) continue;
                var skin = original as SkinnedMeshRenderer;
                Mesh mesh = skin != null ? skin.sharedMesh : original.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !(original is MeshRenderer || skin != null)) continue;
                sources.Add(new Source { Original = original, Skin = skin, Mesh = mesh });
                outlineRenderers.Add(original);
            }
        }
        ActiveSessions.Add(this);
    }

    public void CollectOutlineGroups(List<CharacterEchoOutlineGroup> output)
    {
        if (outlineActive && !stopped && outlineRenderers.Count > 0 &&
            outlineWidth > 0f && outlineAlpha > 0f)
        {
            float visibility = 1f;
            foreach (Renderer renderer in outlineRenderers)
                visibility = Mathf.Min(visibility, CharacterFadeBinding.GetVisibility(renderer));
            float visibleAlpha = outlineAlpha * visibility;
            if (visibleAlpha > 0f)
                output.Add(new CharacterEchoOutlineGroup(outlineRenderers, outlineColor,
                    outlineWidth, visibleAlpha));
        }
    }

    public void ApplyOutline(AbilityEventObj_CharacterEcho config, float progress)
    {
        if (stopped || config == null) return;
        outlineActive = config.EnableOutline;
        outlineColor = config.OutlineTint;
        outlineWidth = Mathf.Max(0f, config.OutlineWidthPixels);
        outlineAlpha = outlineActive ? Mathf.Clamp01(AbilityEventObj_CharacterEcho.Evaluate(
            config.OutlineOpacity, progress)) : 0f;
    }

    public void CaptureGhost(float lifetime, float initialAge)
    {
        lifetime = Mathf.Max(0.02f, lifetime);
        if (material == null || sources.Count == 0 || initialAge >= lifetime) return;
        Ghost ghost = pool.Count > 0 ? pool.Pop() : new Ghost();
        ghost.Age = Mathf.Max(0f, initialAge);
        ghost.Lifetime = lifetime;
        for (int i = 0; i < sources.Count; i++)
        {
            Source source = sources[i];
            if (source.Original == null) continue;
            GhostPiece piece;
            if (i < ghost.Pieces.Count) piece = ghost.Pieces[i];
            else
            {
                var go = new GameObject("[Character Echo] Snapshot");
                if (preview) go.hideFlags = HideFlags.HideAndDontSave;
                piece = new GhostPiece { Object = go };
                if (source.Skin != null)
                {
                    piece.Mesh = new Mesh { name = "Character Echo Snapshot Mesh" };
                    piece.OwnsMesh = true;
                }
                else piece.Mesh = source.Mesh;
                piece.Body = CreateStaticRenderer(go.transform, piece.Mesh,
                    source.Mesh.subMeshCount);
                ghost.Pieces.Add(piece);
            }
            if (source.Skin != null) source.Skin.BakeMesh(piece.Mesh);
            piece.Object.transform.SetPositionAndRotation(source.Original.transform.position,
                source.Original.transform.rotation);
            piece.Object.transform.localScale = source.Original.transform.lossyScale;
            piece.Object.SetActive(true);
        }
        ghosts.Add(ghost);
        UpdateGhost(ghost);
    }

    public void Advance(float deltaTime)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = ghosts[i];
            ghost.Age += Mathf.Max(0f, deltaTime);
            if (ghost.Age >= ghost.Lifetime)
            {
                ghosts.RemoveAt(i);
                HideGhost(ghost);
                pool.Push(ghost);
            }
            else UpdateGhost(ghost);
        }
    }

    public void ClearGhosts()
    {
        foreach (Ghost ghost in ghosts) { HideGhost(ghost); pool.Push(ghost); }
        ghosts.Clear();
    }

    public void Stop()
    {
        stopped = true;
        outlineActive = false;
    }

    public void Dispose()
    {
        Stop();
        ClearGhosts();
        sources.Clear();
        outlineRenderers.Clear();
        ActiveSessions.Remove(this);
        while (pool.Count > 0)
        {
            Ghost ghost = pool.Pop();
            foreach (GhostPiece piece in ghost.Pieces)
            {
                if (piece.OwnsMesh) DestroyObject(piece.Mesh);
                DestroyObject(piece.Object);
            }
            ghost.Pieces.Clear();
        }
    }

    private Renderer CreateStaticRenderer(Transform parent, Mesh mesh, int subMeshCount)
    {
        var go = new GameObject("[Character Echo] Body");
        if (preview) go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var materials = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    private void UpdateGhost(Ghost ghost)
    {
        float progress = Mathf.Clamp01(ghost.Age / ghost.Lifetime);
        float fade = 1f - progress;
        block.Clear();
            block.SetFloat(EffectAlpha, fade);
            block.SetFloat(UseDissolve, 1f);
            block.SetFloat(DissolveSwitch, 0f);
            block.SetFloat(DissolvePower, progress);
            block.SetFloat(DissolveChannel, 0f);
        foreach (GhostPiece piece in ghost.Pieces)
        {
            if (piece.Body == null) continue;
            piece.Body.enabled = fade > 0.001f;
            piece.Body.SetPropertyBlock(block);
        }
    }

    private static void HideGhost(Ghost ghost)
    {
        foreach (GhostPiece piece in ghost.Pieces)
            if (piece.Object != null) piece.Object.SetActive(false);
    }

    private void DestroyObject(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (preview && !Application.isPlaying) UnityEngine.Object.DestroyImmediate(obj);
        else UnityEngine.Object.Destroy(obj);
    }
}
