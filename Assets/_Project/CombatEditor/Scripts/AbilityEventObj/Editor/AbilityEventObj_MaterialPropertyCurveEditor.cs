using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_MaterialPropertyCurve))]
    public sealed class AbilityEventObj_MaterialPropertyCurveEditor : Editor
    {
        private sealed class PropertyOption
        {
            public string Name;
            public string Label;
            public MaterialCurveValueType Type;
            public int MaterialCount;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetMode"), new GUIContent("作用范围"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetMaterial"), new GUIContent("目标材质（可选）"));
            var targetMode = (MaterialCurveTargetMode)serializedObject.FindProperty("TargetMode").enumValueIndex;
            bool sceneMaterial = targetMode == MaterialCurveTargetMode.SceneMaterial ||
                (targetMode == MaterialCurveTargetMode.Auto && serializedObject.FindProperty("TargetMaterial").objectReferenceValue != null);
            if (sceneMaterial)
            {
                EditorGUILayout.HelpBox("控制已加载场景中所有使用目标材质的对象，包括轨道播放期间生成的对象，无需关联角色挂点。轨道结束后恢复原值。", MessageType.Info);
                if (serializedObject.FindProperty("TargetMaterial").objectReferenceValue == null)
                    EditorGUILayout.HelpBox("场景材质模式必须指定目标材质。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetNode"), new GUIContent("目标挂点"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("IncludeChildren"), new GUIContent("包含子物体"));
            }

            List<Material> materials = ResolveMaterials();
            List<PropertyOption> options = DiscoverProperties(materials);
            if (materials.Count == 0)
                EditorGUILayout.HelpBox("指定目标材质后会自动列出属性；也可以在动作编辑器中选择角色，自动读取目标挂点上的材质。", MessageType.Info);
            else
            {
                string names = string.Join("、", materials.ConvertAll(material => material.name));
                EditorGUILayout.HelpBox($"已读取材质：{names}。下拉列表显示属性名称、Shader 属性名及类型；纹理和向量属性暂不支持。", MessageType.Info);
                if (options.Count == 0)
                    EditorGUILayout.HelpBox("这些材质没有支持的 Float、Range 或 Color 属性。", MessageType.Warning);
            }

            SerializedProperty properties = serializedObject.FindProperty("Properties");
            for (int i = 0; i < properties.arraySize; i++)
            {
                SerializedProperty entry = properties.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"属性 {i + 1}", EditorStyles.boldLabel);
                bool remove = GUILayout.Button("删除", GUILayout.Width(48));
                EditorGUILayout.EndHorizontal();
                DrawProperty(entry, options, materials.Count);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    properties.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            using (new EditorGUI.DisabledScope(options.Count == 0))
            {
                if (GUILayout.Button("添加材质属性曲线"))
                {
                    int index = properties.arraySize++;
                    SerializedProperty entry = properties.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("PropertyName").stringValue = options[0].Name;
                    entry.FindPropertyRelative("ValueType").enumValueIndex = (int)options[0].Type;
                    entry.FindPropertyRelative("Operation").enumValueIndex = (int)MaterialCurveOperation.MultiplyOriginal;
                    entry.FindPropertyRelative("Value").floatValue = 1f;
                    entry.FindPropertyRelative("Color").colorValue = Color.white;
                    entry.FindPropertyRelative("AffectAlpha").boolValue = false;
                    entry.FindPropertyRelative("Curve").animationCurveValue = AnimationCurve.Linear(0f, 1f, 1f, 1f);
                }
            }

            if (serializedObject.ApplyModifiedProperties() && CombatEditorUtility.EditorExist())
            {
                CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
                if (!EditorApplication.isPlaying && editor.SelectedAbilityObj != null &&
                    editor.SelectedAbilityObj.events.Exists(entry => entry != null && entry.Obj == target))
                    editor._previewer?.RefreshMaterialPropertyPreview((AbilityEventObj_MaterialPropertyCurve)target);
            }
        }

        private void DrawProperty(SerializedProperty entry, List<PropertyOption> options, int materialCount)
        {
            SerializedProperty name = entry.FindPropertyRelative("PropertyName");
            SerializedProperty type = entry.FindPropertyRelative("ValueType");
            int selected = options.FindIndex(option => option.Name == name.stringValue && (int)option.Type == type.enumValueIndex);
            if (selected < 0) selected = options.FindIndex(option => option.Name == name.stringValue);
            var labels = new List<string> { selected < 0 && !string.IsNullOrEmpty(name.stringValue)
                ? $"未匹配：{name.stringValue}（请选择属性）" : "请选择材质属性" };
            labels.AddRange(options.ConvertAll(option => option.Label));
            using (new EditorGUI.DisabledScope(options.Count == 0))
            {
                int choice = EditorGUILayout.Popup("材质属性", selected + 1, labels.ToArray());
                if (choice > 0)
                {
                    selected = choice - 1;
                    name.stringValue = options[selected].Name;
                    type.enumValueIndex = (int)options[selected].Type;
                }
            }
            if (selected < 0 && materialCount > 0)
                EditorGUILayout.HelpBox("当前材质不包含已配置属性，请从下拉列表重新选择。原曲线会保留。", MessageType.Warning);
            else if (selected >= 0 && options[selected].MaterialCount < materialCount)
                EditorGUILayout.LabelField($"该属性匹配 {options[selected].MaterialCount}/{materialCount} 个材质，不匹配的材质会跳过。");

            SerializedProperty operation = entry.FindPropertyRelative("Operation");
            operation.enumValueIndex = EditorGUILayout.Popup("计算方式", operation.enumValueIndex,
                new[] { "原值倍率", "直接覆盖" });
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Value"), new GUIContent("曲线强度"));
            if ((MaterialCurveValueType)type.enumValueIndex == MaterialCurveValueType.Color)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Color"), new GUIContent("颜色"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("AffectAlpha"), new GUIContent("同时影响透明度"));
            }
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("Curve"), new GUIContent("动画曲线"));
        }

        private List<Material> ResolveMaterials()
        {
            var materials = new List<Material>();
            Material explicitMaterial = serializedObject.FindProperty("TargetMaterial").objectReferenceValue as Material;
            if (explicitMaterial != null) { materials.Add(explicitMaterial); return materials; }
            if ((MaterialCurveTargetMode)serializedObject.FindProperty("TargetMode").enumValueIndex == MaterialCurveTargetMode.SceneMaterial)
                return materials;
            if (!CombatEditorUtility.EditorExist()) return materials;
            CombatController controller = CombatEditorUtility.GetCurrentEditor().SelectedController;
            if (controller == null) return materials;
            var nodeType = (CharacterNode.NodeType)serializedObject.FindProperty("TargetNode").enumValueIndex;
            Transform node = null;
            if (nodeType == CharacterNode.NodeType.Animator) node = controller.GetNodeTranform(nodeType);
            else
                foreach (CharacterNode entry in controller.Nodes)
                    if (entry != null && entry.type == nodeType && entry.NodeTrans != null)
                    { node = entry.NodeTrans; break; }
            if (node == null) return materials;
            Renderer[] renderers = serializedObject.FindProperty("IncludeChildren").boolValue
                ? node.GetComponentsInChildren<Renderer>(true) : node.GetComponents<Renderer>();
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && !materials.Contains(material)) materials.Add(material);
            return materials;
        }

        private static List<PropertyOption> DiscoverProperties(List<Material> materials)
        {
            var options = new List<PropertyOption>();
            foreach (Material material in materials)
            {
                Shader shader = material.shader;
                if (shader == null) continue;
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    ShaderPropertyType shaderType = shader.GetPropertyType(i);
                    if (shaderType != ShaderPropertyType.Float && shaderType != ShaderPropertyType.Range &&
                        shaderType != ShaderPropertyType.Color) continue;
                    string name = shader.GetPropertyName(i);
                    MaterialCurveValueType type = shaderType == ShaderPropertyType.Color
                        ? MaterialCurveValueType.Color : MaterialCurveValueType.Float;
                    PropertyOption option = options.Find(candidate => candidate.Name == name && candidate.Type == type);
                    if (option == null)
                    {
                        option = new PropertyOption { Name = name, Type = type,
                            Label = $"{shader.GetPropertyDescription(i)}  ({name})  [{type}]" };
                        options.Add(option);
                    }
                    option.MaterialCount++;
                }
            }
            return options;
        }
    }
}
