using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CombatEditor
{
    internal static class CallMethodEditorUtility
    {
        private static readonly string[] EmptyMethodOptions = new[] { "<No Public Methods Found>" };
        private static readonly string[] EmptyScriptOptions = new[] { "<No Scripts Found>" };

        public static MonoBehaviour[] GetCharacterComponents()
        {
            CombatEditor editor = CombatEditorUtility.GetCurrentEditor();
            if (editor == null || editor.SelectedController == null)
            {
                return Array.Empty<MonoBehaviour>();
            }

            return editor.SelectedController.GetComponentsInChildren<MonoBehaviour>(true);
        }

        public static string[] GetScriptOptions()
        {
            MonoBehaviour[] components = GetCharacterComponents();
            if (components.Length == 0)
            {
                return EmptyScriptOptions;
            }

            HashSet<string> scriptNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                scriptNames.Add(component.GetType().Name);
            }

            if (scriptNames.Count == 0)
            {
                return EmptyScriptOptions;
            }

            List<string> list = new List<string>(scriptNames);
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }

        public static string[] GetMethodOptions(string scriptName)
        {
            MonoBehaviour[] components = GetCharacterComponents();
            if (components.Length == 0 || string.IsNullOrWhiteSpace(scriptName))
            {
                return EmptyMethodOptions;
            }

            HashSet<string> methodNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (!string.Equals(type.Name, scriptName, StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo method = methods[j];
                    if (method.ReturnType != typeof(void))
                    {
                        continue;
                    }

                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length == 0 || (ps.Length == 1 && ps[0].ParameterType == typeof(float)))
                    {
                        methodNames.Add(method.Name);
                    }
                }
            }

            if (methodNames.Count == 0)
            {
                return EmptyMethodOptions;
            }

            List<string> list = new List<string>(methodNames);
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }

    [CustomEditor(typeof(AbilityEventObj_Method), true)]
    public class AbilityEventObj_MethodEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawScriptAndMethodPicker("ScriptTypeName", "MethodName");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LogMissingMethod"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();
        }

        private void DrawScriptAndMethodPicker(string scriptPropertyName, string methodPropertyName)
        {
            SerializedProperty scriptNameSp = serializedObject.FindProperty(scriptPropertyName);
            SerializedProperty methodNameSp = serializedObject.FindProperty(methodPropertyName);

            string[] scriptOptions = CallMethodEditorUtility.GetScriptOptions();
            int scriptSelectedIndex = Array.IndexOf(scriptOptions, scriptNameSp.stringValue);
            int scriptPopupIndex = EditorGUILayout.Popup(new GUIContent("Script"), Mathf.Max(0, scriptSelectedIndex), scriptOptions);
            if (scriptOptions.Length > 0 && scriptOptions[0] != "<No Scripts Found>")
            {
                scriptNameSp.stringValue = scriptOptions[scriptPopupIndex];
            }

            string[] methodOptions = CallMethodEditorUtility.GetMethodOptions(scriptNameSp.stringValue);
            int selectedIndex = Array.IndexOf(methodOptions, methodNameSp.stringValue);
            int popupIndex = EditorGUILayout.Popup(new GUIContent("Method"), Mathf.Max(0, selectedIndex), methodOptions);
            if (methodOptions.Length > 0 && methodOptions[0] != "<No Public Methods Found>")
            {
                string oldName = methodNameSp.stringValue;
                methodNameSp.stringValue = methodOptions[popupIndex];
                if (!string.Equals(oldName, methodNameSp.stringValue, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(methodNameSp.stringValue))
                {
                    target.name = methodNameSp.stringValue;
                    EditorUtility.SetDirty(target);
                }
            }

            EditorGUILayout.HelpBox("Pick script first, then method. Data comes from current selected character.", MessageType.Info);
        }
    }

    [CustomEditor(typeof(AbilityEventObj_MethodContinuous), true)]
    public class AbilityEventObj_MethodContinuousEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawScriptAndMethodPicker("ScriptTypeName", "MethodName");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LogMissingMethod"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUI.EndDisabledGroup();
        }

        private void DrawScriptAndMethodPicker(string scriptPropertyName, string methodPropertyName)
        {
            SerializedProperty scriptNameSp = serializedObject.FindProperty(scriptPropertyName);
            SerializedProperty methodNameSp = serializedObject.FindProperty(methodPropertyName);

            string[] scriptOptions = CallMethodEditorUtility.GetScriptOptions();
            int scriptSelectedIndex = Array.IndexOf(scriptOptions, scriptNameSp.stringValue);
            int scriptPopupIndex = EditorGUILayout.Popup(new GUIContent("Script"), Mathf.Max(0, scriptSelectedIndex), scriptOptions);
            if (scriptOptions.Length > 0 && scriptOptions[0] != "<No Scripts Found>")
            {
                scriptNameSp.stringValue = scriptOptions[scriptPopupIndex];
            }

            string[] methodOptions = CallMethodEditorUtility.GetMethodOptions(scriptNameSp.stringValue);
            int selectedIndex = Array.IndexOf(methodOptions, methodNameSp.stringValue);
            int popupIndex = EditorGUILayout.Popup(new GUIContent("Method"), Mathf.Max(0, selectedIndex), methodOptions);
            if (methodOptions.Length > 0 && methodOptions[0] != "<No Public Methods Found>")
            {
                string oldName = methodNameSp.stringValue;
                methodNameSp.stringValue = methodOptions[popupIndex];
                if (!string.Equals(oldName, methodNameSp.stringValue, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(methodNameSp.stringValue))
                {
                    target.name = methodNameSp.stringValue;
                    EditorUtility.SetDirty(target);
                }
            }

            EditorGUILayout.HelpBox("Duration is controlled by track length. Pick script first, then method.", MessageType.Info);
        }
    }
}
