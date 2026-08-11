using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_CreateObjWithHandle), true)]
    public class AbilityEventObj_CreateObjWithHandleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            DrawTransformEditButtons();
        }

        protected void DrawTransformEditButtons()
        {
            global::CombatEditor.CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
            if (editor == null)
            {
                return;
            }

            AbilityEventObj currentObj = target as AbilityEventObj;
            if (currentObj == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Edit", EditorStyles.boldLabel);

            Color defaultColor = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = editor.IsEditingTransform(currentObj, CombatEditorTransformEditState.EditMode.Position) ? Color.green : defaultColor;
            if (GUILayout.Button("Pos"))
            {
                editor.ToggleTransformEditing(currentObj, CombatEditorTransformEditState.EditMode.Position);
            }

            GUI.backgroundColor = editor.IsEditingTransform(currentObj, CombatEditorTransformEditState.EditMode.Rotation) ? Color.green : defaultColor;
            if (GUILayout.Button("Rot"))
            {
                editor.ToggleTransformEditing(currentObj, CombatEditorTransformEditState.EditMode.Rotation);
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = defaultColor;
            EditorGUILayout.HelpBox("Use Pos or Rot to edit this event on the current preview frame.", MessageType.Info);
        }
    }
}
