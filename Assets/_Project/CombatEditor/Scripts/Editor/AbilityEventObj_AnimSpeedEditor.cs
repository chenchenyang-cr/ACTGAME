using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_AnimSpeed))]
    public class AbilityEventObj_AnimSpeedEditor : Editor
    {
        SerializedProperty modeProperty;
        SerializedProperty speedProperty;
        SerializedProperty speedCurveProperty;

        void OnEnable()
        {
            modeProperty = serializedObject.FindProperty("Mode");
            speedProperty = serializedObject.FindProperty("Speed");
            speedCurveProperty = serializedObject.FindProperty("SpeedCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(modeProperty);

            EditorGUILayout.PropertyField(speedProperty, new GUIContent("Speed"));

            AnimSpeedMode mode = (AnimSpeedMode)modeProperty.enumValueIndex;
            if (mode == AnimSpeedMode.Curve)
            {
                EditorGUILayout.PropertyField(speedCurveProperty, new GUIContent("Speed Curve"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
