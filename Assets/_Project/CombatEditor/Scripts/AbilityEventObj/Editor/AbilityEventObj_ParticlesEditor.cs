using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_Particles))]
    public class AbilityEventObj_ParticlesEditor : AbilityEventObj_CreateObjWithHandleEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            DrawTransformEditButtons();

            if (changed && CombatEditorUtility.EditorExist())
            {
                CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
                editor.RequirePreviewReload();
                editor.HardResetPreviewToCurrentFrame();
            }
        }
    }
}
