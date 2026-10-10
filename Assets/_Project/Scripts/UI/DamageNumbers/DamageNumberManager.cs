using System.Collections.Generic;
using CombatEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace UnityLearning.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class DamageNumberManager : MonoBehaviour
    {
        [InspectorName("默认跳字预设")][SerializeField] private DamageNumberProfile defaultProfile;
        [InspectorName("跳字 Prefab")][SerializeField] private DamageNumberView viewPrefab;
        [InspectorName("场景相机")][Tooltip("留空时使用 Main Camera。")]
        [SerializeField] private Camera sceneCamera;
        [InspectorName("预热数量")][SerializeField, Min(0)] private int prewarmCount = 16;
        [InspectorName("最大同时显示数量")][SerializeField, Min(1)] private int maximumActive = 128;

        private readonly List<DamageNumberView> active = new List<DamageNumberView>();
        private readonly Stack<DamageNumberView> pool = new Stack<DamageNumberView>();
        private RectTransform canvasRect;
        public int ActiveCount => active.Count;
        public int PooledCount => pool.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            EnsureSceneManager();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureSceneManager();
        private static void OnSceneUnloaded(Scene scene)
        {
            if (SceneManager.sceneCount > 0) EnsureSceneManager();
        }

        private static void EnsureSceneManager()
        {
            if (!Application.isPlaying) return;
            var existing = FindObjectOfType<DamageNumberManager>();
            if (existing != null)
            {
                existing.Subscribe();
                return;
            }
            var prefab = Resources.Load<DamageNumberManager>("DamageNumberCanvas");
            if (prefab != null) Instantiate(prefab);
        }

        private void Awake()
        {
            canvasRect = (RectTransform)transform;
            if (viewPrefab == null) return;
            for (int i = 0; i < Mathf.Min(prewarmCount, Mathf.Max(1, maximumActive)); i++)
                pool.Push(CreateView());
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            // Also restores subscriptions when Enter Play Mode disables domain/scene reload.
            CombatHitEventBus.HitConfirmed -= OnHitConfirmed;
            Canvas.willRenderCanvases -= ProjectNumbers;
            CombatHitEventBus.HitConfirmed += OnHitConfirmed;
            Canvas.willRenderCanvases += ProjectNumbers;
        }

        private void OnDisable()
        {
            CombatHitEventBus.HitConfirmed -= OnHitConfirmed;
            Canvas.willRenderCanvases -= ProjectNumbers;
            for (int i = active.Count - 1; i >= 0; i--) Recycle(i);
        }

        private void OnHitConfirmed(CombatHitConfirmedEvent hit)
        {
            if (hit.Target == null || hit.Damage <= 0f ||
                (hit.ResultType != CombatHitResultType.Normal && hit.ResultType != CombatHitResultType.Critical)) return;
            ICombatDamageReceiver receiver = hit.Target.GetComponent<ICombatDamageReceiver>();
            if (receiver == null || receiver.Team != CombatTeam.Enemy) return;
            CombatHitProfile hitProfile = hit.SourceHitBoxEvent != null ? hit.SourceHitBoxEvent.HitProfile : null;
            if (hitProfile != null && hitProfile.SuppressDamageNumber) return;
            DamageNumberProfile profile = hitProfile != null && hitProfile.DamageNumberOverride != null
                ? hitProfile.DamageNumberOverride : defaultProfile;
            if (profile == null || viewPrefab == null) return;
            Vector3 anchor = profile.Anchor == DamageNumberAnchor.TargetPosition
                ? hit.Target.transform.position : hit.HitPoint;
            Spawn(profile, hit.Damage, hit.ResultType == CombatHitResultType.Critical,
                anchor + profile.WorldOffset, hit.Target.transform);
        }

        private void Spawn(DamageNumberProfile profile, float damage, bool critical,
            Vector3 anchor, Transform target)
        {
            if (active.Count >= Mathf.Max(1, maximumActive)) Recycle(0);
            DamageNumberView view = pool.Count > 0 ? pool.Pop() : CreateView();
            Vector2 range = new Vector2(Mathf.Max(0f, profile.RandomOffset.x), Mathf.Max(0f, profile.RandomOffset.y));
            Vector2 offset = profile.InitialOffset + new Vector2(Random.Range(-range.x, range.x), Random.Range(-range.y, range.y));
            view.Play(profile, damage, critical, anchor, target, offset, Random.Range(1, int.MaxValue));
            view.transform.SetAsLastSibling();
            active.Add(view);
        }

        private DamageNumberView CreateView()
        {
            DamageNumberView view = Instantiate(viewPrefab, transform, false);
            view.ResetForPool();
            return view;
        }

        private void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                DamageNumberProfile profile = active[i].Profile;
                float delta = profile != null && profile.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (!active[i].Advance(delta)) Recycle(i);
            }
        }

        // Canvas rendering runs after Cinemachine has updated the actual output camera.
        private void ProjectNumbers()
        {
            if (canvasRect == null) canvasRect = (RectTransform)transform;
            if (sceneCamera == null) sceneCamera = Camera.main;
            for (int i = 0; i < active.Count; i++) active[i].Project(sceneCamera, canvasRect);
        }

        private void Recycle(int index)
        {
            DamageNumberView view = active[index];
            active.RemoveAt(index);
            view.ResetForPool();
            if (pool.Count < Mathf.Max(1, maximumActive)) pool.Push(view);
            else Destroy(view.gameObject);
        }
    }
}
