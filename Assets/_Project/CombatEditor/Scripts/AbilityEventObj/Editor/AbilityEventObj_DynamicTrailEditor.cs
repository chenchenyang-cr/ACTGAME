using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

 namespace CombatEditor
{	
	[CustomEditor(typeof(AbilityEventObj_DynamicTrail))]
	public class AbilityEventObj_DynamicTrailEditor :Editor
	{
	    public override void OnInspectorGUI()
	    {
	        serializedObject.Update();
	        EditorGUI.BeginChangeCheck();
	        EditorGUI.BeginDisabledGroup(true);
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
	        EditorGUI.EndDisabledGroup();
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("BaseNode"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TipNode"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TrailMat"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("MaxFrame"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TrailSubs"));
	        //EditorGUILayout.PropertyField(serializedObject.FindProperty("uvMethod"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("StopMultiplier"));
	        EditorGUILayout.Space(6f);
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TrailColor"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("Brightness"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TrailTexture"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TextureTiling"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TextureScrollSpeed"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("TailFade"));
	        EditorGUILayout.PropertyField(serializedObject.FindProperty("Opacity"), new GUIContent("Opacity（透明度）"));

            if (EditorGUI.EndChangeCheck())
            {
                if (CombatEditorUtility.EditorExist())
                {
                    CombatEditorUtility.GetCurrentEditor().RequirePreviewReload();
                }
            }
            serializedObject.ApplyModifiedProperties();
	    }
	}
}
