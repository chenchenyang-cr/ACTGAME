#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    public sealed class AbilityEventPreview_TargetAssist : AbilityEventPreview
    {
        private TargetAssistRangePreviewHandle rangeHandle;

        public AbilityEventPreview_TargetAssist(AbilityEventObj obj) : base(obj) { }

        public override void InitPreview()
        {
            base.InitPreview();
            if (previewGroup == null)
            {
                return;
            }

            rangeHandle = previewGroup.AddComponent<TargetAssistRangePreviewHandle>();
            rangeHandle._combatController = _combatController;
            rangeHandle._preview = this;
            rangeHandle.Init();
        }

        public override void DestroyPreview()
        {
            if (rangeHandle != null)
            {
                rangeHandle.SelfDestroy();
                Object.DestroyImmediate(rangeHandle);
                rangeHandle = null;
            }
        }
    }

    public sealed class TargetAssistRangePreviewHandle : PreviewerOnObject
    {
        private static readonly Color FillColor = new(0.1f, 0.75f, 1f, 0.08f);
        private static readonly Color OutlineColor = new(0.1f, 0.8f, 1f, 0.95f);

        public override void PaintHandle()
        {
            if (_preview is not AbilityEventPreview_TargetAssist ||
                _preview._EventObj is not AbilityEventObj_TargetAssistWindow window ||
                !_preview.PreviewInRange(CombatGlobalEditorValue.Percentage))
            {
                return;
            }

            float radius = Mathf.Max(0f, window.AcquireRadius);
            Vector3 center = _combatController.transform.position + Vector3.up * 0.03f;

            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            Handles.color = FillColor;
            Handles.DrawSolidDisc(center, Vector3.up, radius);
            Handles.color = OutlineColor;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.Label(center + Vector3.forward * radius, $"吸附范围  {radius:0.##}m");
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }
    }
}
#endif
