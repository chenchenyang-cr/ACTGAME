using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_AnimSpeed))]
    public class AbilityEventObj_AnimSpeedEditor : Editor
    {
        SerializedProperty modeProperty;
        SerializedProperty speedProperty;
        SerializedProperty speedAtCurve0Property;
        SerializedProperty speedAtCurve1Property;
        SerializedProperty speedCurveProperty;

        void OnEnable()
        {
            modeProperty = serializedObject.FindProperty("Mode");
            speedProperty = serializedObject.FindProperty("Speed");
            speedAtCurve0Property = serializedObject.FindProperty("SpeedAtCurve0");
            speedAtCurve1Property = serializedObject.FindProperty("SpeedAtCurve1");
            speedCurveProperty = serializedObject.FindProperty("SpeedCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(modeProperty);

            AnimSpeedMode mode = (AnimSpeedMode)modeProperty.enumValueIndex;
            if (mode == AnimSpeedMode.Constant)
            {
                EditorGUILayout.PropertyField(speedProperty, new GUIContent("Speed"));
            }
            else
            {
                EditorGUILayout.PropertyField(speedAtCurve0Property, new GUIContent("Speed At Curve 0"));
                EditorGUILayout.PropertyField(speedAtCurve1Property, new GUIContent("Speed At Curve 1"));
                EditorGUILayout.PropertyField(speedCurveProperty, new GUIContent("Speed Curve"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
