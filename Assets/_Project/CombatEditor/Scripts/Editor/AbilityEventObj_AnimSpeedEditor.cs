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
                // An unrestricted range keeps Unity's native curve-window range controls available.
                EditorGUILayout.CurveField(speedCurveProperty, Color.green,
                    Rect.zero,
                    new GUIContent("Speed Curve"), GUILayout.Height(36f));
                EditorGUILayout.HelpBox("最终速度 = Speed × 曲线值。曲线值可以大于 1，负速度按 0 处理。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
