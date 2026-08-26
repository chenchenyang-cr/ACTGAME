#if UNITY_EDITOR
using UnityEditor;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_PostFxTrack), true)]
    public sealed class AbilityEventObj_CombatPostFxEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed && CombatEditorUtility.EditorExist())
                CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
        }
    }
}
#endif
