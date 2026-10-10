using TMPro;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityLearning.UI.Editor
{
    [CustomEditor(typeof(DamageNumberProfile))]
    public sealed class DamageNumberProfileEditor : UnityEditor.Editor
    {
        private PreviewRenderUtility preview;
        private DamageNumberView view;
        private GameObject canvasObject;
        private float previewDamage = 123f;
        private bool critical;
        private bool playing;
        private bool loop = true;
        private float time;
        private double lastTick;
        private int seed = 19356;
        private bool dirty = true;

        private void OnEnable()
        {
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (preview != null) preview.Cleanup();
            preview = null;
            view = null;
            canvasObject = null;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing)
            {
                var profile = (DamageNumberProfile)target;
                time += (float)(now - lastTick);
                if (time >= profile.Lifetime)
                {
                    if (loop) time %= profile.Lifetime;
                    else { time = profile.Lifetime; playing = false; }
                }
                Repaint();
            }
            lastTick = now;
        }

        public override bool HasPreviewGUI() => true;

        public override void OnPreviewSettings()
        {
            if (GUILayout.Button(playing ? "暂停" : "播放", EditorStyles.toolbarButton))
            {
                if (time >= ((DamageNumberProfile)target).Lifetime) time = 0f;
                playing = !playing;
                lastTick = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("重播", EditorStyles.toolbarButton)) { time = 0f; playing = true; }
            if (GUILayout.Button("随机", EditorStyles.toolbarButton)) { seed++; dirty = true; }
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            Rect slider = new Rect(rect.x + 12f, rect.yMax - 24f, rect.width - 24f, 16f);
            Rect image = new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, rect.height - 30f));
            DrawNumberPreview(image);
            EditorGUI.BeginChangeCheck();
            time = GUI.HorizontalSlider(slider, time, 0f, ((DamageNumberProfile)target).Lifetime);
            if (EditorGUI.EndChangeCheck()) { playing = false; Repaint(); }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            SerializedProperty property = serializedObject.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                children = false;
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                {
                    var field = typeof(DamageNumberProfile).GetField(property.name);
                    var label = field != null ? field.GetCustomAttribute<InspectorNameAttribute>() : null;
                    EditorGUILayout.PropertyField(property,
                        new GUIContent(label != null ? label.displayName : property.displayName, property.tooltip), true);
                }
            }
            if (EditorGUI.EndChangeCheck()) dirty = true;
            serializedObject.ApplyModifiedProperties();
            var profile = (DamageNumberProfile)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("跳字预览", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            previewDamage = Mathf.Max(.01f, EditorGUILayout.FloatField("模拟伤害", previewDamage));
            critical = EditorGUILayout.Toggle("模拟暴击", critical);
            if (EditorGUI.EndChangeCheck()) dirty = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "暂停" : "播放"))
                {
                    if (time >= profile.Lifetime) time = 0f;
                    playing = !playing;
                    lastTick = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("重播")) { time = 0f; playing = true; }
                if (GUILayout.Button("换一个随机种子")) { seed++; dirty = true; }
                loop = GUILayout.Toggle(loop, "循环", GUILayout.Width(60f));
            }
            EditorGUI.BeginChangeCheck();
            time = EditorGUILayout.Slider("预览时间（秒）", time, 0f, profile.Lifetime);
            if (EditorGUI.EndChangeCheck()) playing = false;
            EditorGUILayout.HelpBox("预览使用实际 TMP 字体和运行时采样。缩放、抖动曲线分别使用自身阶段进度 0～1；位移和发光衰减使用总生命周期进度。独立阶段超过总时长时随跳字回收一起结束。", MessageType.Info);
            if (profile.FadeDuration > profile.Lifetime)
                EditorGUILayout.HelpBox("淡出时长超过总时长，运行时按整个生命周期淡出。", MessageType.Info);
        }

        private void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 1.6f;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100f;
            preview.camera.transform.position = new Vector3(0f, 0f, -10f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.08f, .09f, .12f);
            canvasObject = new GameObject("Damage Number Inspector Preview", typeof(RectTransform), typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = preview.camera;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(800f, 320f);
            canvasRect.localScale = Vector3.one * .01f;
            var root = new GameObject("Number", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvasRect, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(400f, 140f);
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            view = root.AddComponent<DamageNumberView>();
            preview.AddSingleGO(canvasObject);
            dirty = true;
        }

        private void DrawNumberPreview(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsurePreview();
            var profile = (DamageNumberProfile)target;
            if (dirty)
            {
                // Fixed random offsets make scrubbing and tuning repeatable.
                uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
                Vector2 random = new Vector2((hash & 0xffffu) / 32767.5f - 1f,
                    ((hash >> 16) & 0xffffu) / 32767.5f - 1f);
                Vector2 range = new Vector2(Mathf.Max(0f, profile.RandomOffset.x), Mathf.Max(0f, profile.RandomOffset.y));
                view.Play(profile, previewDamage, critical, Vector3.zero, null,
                    profile.InitialOffset + Vector2.Scale(random, range), seed);
                dirty = false;
            }
            view.SetTime(time);
            view.PlaceInCanvas(new Vector2(0f, -60f));
            view.GetComponentInChildren<TextMeshProUGUI>().ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true);
            Texture texture = preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
    }
}
