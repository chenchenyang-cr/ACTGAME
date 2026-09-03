using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    [CustomPropertyDrawer(typeof(MyAnimationCurveAttribute))]
    public sealed class MyAnimationCurveDrawer : PropertyDrawer
    {
        private const float CurveHeight = 36f;

        public override float GetPropertyHeight(SerializedProperty property,
            GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.AnimationCurve
                ? CurveHeight
                : EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.AnimationCurve)
            {
                EditorGUI.BeginChangeCheck();
                AnimationCurve curve = EditorGUI.CurveField(position, label,
                    property.animationCurveValue, Color.green,
                    new Rect(0f, -1f, 1f, 2f));
                if (EditorGUI.EndChangeCheck())
                    property.animationCurveValue = curve;

                if (Event.current.type == EventType.Repaint)
                {
                    float markerX = position.x + position.width *
                        Mathf.Clamp01(CombatGlobalEditorValue.Percentage);
                    EditorGUI.DrawRect(new Rect(markerX, position.y, 1f,
                        position.height), Color.white);
                }
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }

            EditorGUI.EndProperty();
        }
    }
}
