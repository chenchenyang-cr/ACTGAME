using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CharacterEcho))]
    public sealed class AbilityEventObj_CharacterEchoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("角色外轮廓发光", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("区间事件：只控制角色最外沿的深度轮廓。", MessageType.Info);
            Draw("playOnPerfectDodge", "仅完美闪避成功时播放");

            Draw("TargetNode", "目标挂点");
            Draw("IncludeChildren", "包含子物体");

            EditorGUILayout.Space();
            Section("EnableOutline", "外轮廓发光", "OutlineTint", "OutlineWidthPixels",
                "OutlineOpacity");

            if (!serializedObject.ApplyModifiedProperties()) return;
            if (Application.isPlaying) return;
            CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
            if (editor == null || editor.SelectedAbilityObj == null ||
                !editor.SelectedAbilityObj.events.Exists(entry => entry != null && entry.Obj == target)) return;
            editor._previewer?.RefreshCharacterVisualPreview();
        }

        private void Section(string toggleName, string label, params string[] fields)
        {
            SerializedProperty toggle = serializedObject.FindProperty(toggleName);
            EditorGUILayout.PropertyField(toggle, new GUIContent(label));
            if (!toggle.boolValue) return;
            using (new EditorGUI.IndentLevelScope())
                foreach (string field in fields) Draw(field, Label(field));
        }

        private void Draw(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private static string Label(string field) => field switch
        {
            "OutlineTint" => "轮廓颜色与亮度",
            "OutlineWidthPixels" => "轮廓宽度（像素）",
            "OutlineOpacity" => "轮廓透明度曲线",
            _ => field
        };
    }

    [CustomEditor(typeof(AbilityEventObj_CharacterAfterimage))]
    public sealed class AbilityEventObj_CharacterAfterimageEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("点事件：播放到此帧时，在当前位置留下一个静止的 LTY 风格幻影，并按寿命淡出。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnPerfectDodge"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetNode"), new GUIContent("目标挂点"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeChildren"), new GUIContent("包含子物体"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeWeapon"), new GUIContent("同时包含武器"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("EffectMaterial"), new GUIContent("幻影材质（留空用模板）"));
            Material effectMaterial = serializedObject.FindProperty("EffectMaterial").objectReferenceValue as Material;
            if (effectMaterial != null && !effectMaterial.HasProperty("_EffectAlpha"))
                EditorGUILayout.HelpBox("此材质没有 _EffectAlpha 属性，无法按寿命淡出。请使用 CharacterEchoURP 或兼容材质。", MessageType.Warning);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Lifetime"), new GUIContent("幻影寿命（秒）"));
            if (serializedObject.ApplyModifiedProperties()) CharacterVisualPreviewRefresh.RefreshPreview(target);
        }
    }

    [CustomEditor(typeof(AbilityEventObj_CharacterFade))]
    public sealed class AbilityEventObj_CharacterFadeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("区间事件：曲线横轴是事件时间，纵轴 0 为完全消失、1 为完全可见。淡入和淡出可画在同一条曲线上。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playOnPerfectDodge"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetNode"), new GUIContent("目标挂点"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeChildren"), new GUIContent("包含子物体"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeWeapon"), new GUIContent("同时包含武器"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Visibility"), new GUIContent("人物可见度曲线"));
            if (serializedObject.ApplyModifiedProperties()) CharacterVisualPreviewRefresh.RefreshPreview(target);
        }
    }

    internal static class CharacterVisualPreviewRefresh
    {
        public static void RefreshPreview(Object target)
        {
            if (Application.isPlaying) return;
            CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
            if (editor == null || editor.SelectedAbilityObj == null ||
                !editor.SelectedAbilityObj.events.Exists(entry => entry != null && entry.Obj == target)) return;
            editor._previewer?.RefreshCharacterVisualPreview();
        }
    }

    /// <summary>Explains the legacy LTY controls on the URP echo material.</summary>
    public sealed class CharacterEchoShaderGUI : ShaderGUI
    {
        private static readonly bool[] Expanded = { true, true, true, false, false };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "建议先调：屏幕纹理颜色 → 边缘颜色 → 主透明度。此材质用于单次角色残影；残影淡出由点事件自动处理。",
                MessageType.Info);

            if (Section(0, "1  主体颜色与流动", "屏幕空间贴图决定主体花纹；它跟随屏幕坐标，不使用模型 UV。"))
            {
                Draw(materialEditor, properties, "_ScreenTex", "屏幕纹理", "主体花纹贴图。灰度噪声可产生明暗变化；模板使用 01dissolve。 ");
                Draw(materialEditor, properties, "_ScreenTexColor", "纹理颜色与亮度", "给屏幕纹理染色；HDR 亮度较高时可配合 Bloom 发光。");
                Draw(materialEditor, properties, "_ScreenTex_Scale_Offset", "纹理缩放与偏移", "X/Y 是水平、垂直缩放；Z/W 是水平、垂直偏移。");
                Draw(materialEditor, properties, "_ScreenTex_Panner", "纹理流动", "X/Y 是移动方向；Z 是速度；W 是纹理颜色强度。Z 为 0 时不流动。");
                Draw(materialEditor, properties, "_MainColorDiSE", "边缘替换色", "主体颜色靠近视角边缘时，逐渐由屏幕纹理颜色过渡到此颜色。");
                Draw(materialEditor, properties, "_MainColorMaskPower", "边缘替换范围", "值越大，替换色越集中在轮廓附近。");
                Draw(materialEditor, properties, "_MainColorMaskSacle", "边缘替换强度", "控制纹理色与边缘替换色的混合权重，也会影响边缘透明度。");
            }

            if (Section(1, "2  边缘发光", "沿模型视角边缘附加一层 HDR 颜色，可用局部坐标限制生效区域。"))
            {
                Draw(materialEditor, properties, "_FreColor", "边缘发光颜色", "附加在主体颜色上的 HDR 发光色。");
                Draw(materialEditor, properties, "_FrePower", "边缘集中程度", "值越大，发光越集中在视角轮廓附近。");
                Draw(materialEditor, properties, "_FreScale", "边缘发光强度", "发光色的亮度倍率；0 表示关闭这层发光。");
                Draw(materialEditor, properties, "_Offset_Direction", "区域遮罩轴", "选择模型局部坐标 X、Y 或 Z，决定哪一侧出现这层发光。");
                Draw(materialEditor, properties, "_FreVPmaskOffset", "区域位置偏移", "沿选定的局部坐标轴平移发光区域。");
                Draw(materialEditor, properties, "_MaskSmoothMin", "区域渐入起点", "局部坐标小于此值时不显示这层发光。");
                Draw(materialEditor, properties, "_MaskSmoothMax", "区域渐入终点", "局部坐标大于此值时完整显示；与起点的距离决定过渡宽度。");
            }

            if (Section(2, "3  透明与混合", "决定 LTY 表面的整体透视程度，以及颜色如何叠到场景上。"))
            {
                Draw(materialEditor, properties, "_Dst", "混合模式", "Additive 把颜色加到背景上，更亮；AlphaBlend 按透明度混合，更容易看出表面颜色。原 LTY 默认 Additive。");
                Draw(materialEditor, properties, "_Main_Alpha", "主透明度", "整个 LTY 表面的透明度倍率；0 不可见，1 保留下面的透明度计算。");
                Draw(materialEditor, properties, "_MainAlphaAdd", "透明度补偿", "给各处透明度增加固定值。提高后，身体正面也会更实。");
                Draw(materialEditor, properties, "_AlphaFrepower", "正面透明度范围", "值越大，正面透明度贡献越集中在朝向镜头的区域。");
                Draw(materialEditor, properties, "_AlphaFreScale", "正面透明度强度", "朝向镜头区域的透明度贡献；调低会更透。");
            }

            if (Section(3, "4  表面边缘线", "这是 LTY 主体表面内的视角边缘颜色，不是动作事件中的最外层轮廓发光。"))
            {
                Draw(materialEditor, properties, "_Color0", "线条颜色", "沿视角边缘增加的 HDR 颜色。");
                Draw(materialEditor, properties, "_SmoothMaxP", "线条强度", "线条颜色的亮度倍率；0 关闭这层线条。");
                Draw(materialEditor, properties, "_SmoothMin", "线条出现位置", "视角边缘程度超过此值时开始出现线条。");
                Draw(materialEditor, properties, "_SmoothMax", "线条完整位置", "视角边缘程度达到此值时完整显示线条；与起点的距离决定过渡宽度。");
            }

            if (Section(4, "5  可选溶解", "溶解纹理只影响 LTY 表面的透明度；残影寿命淡出由动作事件自动处理。"))
            {
                Draw(materialEditor, properties, "_use_disslove", "启用溶解", "关闭时下面的纹理与阈值不会影响画面。");
                if (FindProperty("_use_disslove", properties).floatValue > 0.5f)
                {
                    Draw(materialEditor, properties, "_DissovleTex", "溶解纹理", "用灰度或 Alpha 通道形成局部消失的形状。");
                    Draw(materialEditor, properties, "_DissovleA_R", "读取通道", "R 读取红色通道；A 读取 Alpha 通道。");
                    Draw(materialEditor, properties, "_DissovleTex_Scale_Offset", "溶解缩放与偏移", "X/Y 是屏幕纹理缩放；Z/W 是屏幕纹理偏移。");
                    Draw(materialEditor, properties, "_DissovleTex_Panner", "溶解纹理流动", "X/Y 是方向；Z 是速度；W 是纹理强度。");
                    Draw(materialEditor, properties, "_Disspower", "溶解强度", "值越大，透明消失的区域越多。");
                    Draw(materialEditor, properties, "_smooth", "溶解边缘柔和度", "0.5 接近硬切；越靠近 1，消失边缘越柔和。");
                    Draw(materialEditor, properties, "_DissSC", "忽略溶解强度", "On 时忽略上面的溶解强度值，保留原 LTY Shader 的开关行为。");
                }
            }
        }

        private static bool Section(int index, string title, string description)
        {
            Expanded[index] = EditorGUILayout.Foldout(Expanded[index], title, true, EditorStyles.foldoutHeader);
            if (!Expanded[index]) return false;
            EditorGUILayout.HelpBox(description, MessageType.None);
            return true;
        }

        private void Draw(MaterialEditor materialEditor, MaterialProperty[] properties,
            string propertyName, string label, string description)
        {
            materialEditor.ShaderProperty(FindProperty(propertyName, properties), label);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }
    }
}
