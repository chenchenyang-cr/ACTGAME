using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_InterruptWindow))]
    public sealed class AbilityEventObj_InterruptWindowEditor : Editor
    {
        private SerializedProperty allowedCommands;
        private ReorderableList commandList;

        private void OnEnable()
        {
            allowedCommands = serializedObject.FindProperty("AllowedCommands");
            commandList = new ReorderableList(serializedObject, allowedCommands, true, true, true, true);
            commandList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Allowed Commands (drag to reorder)");
            commandList.drawElementCallback = DrawCommand;
            commandList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            commandList.onAddDropdownCallback = (_, _) => ShowAddMenu();
            commandList.onCanAddCallback = _ => GetAvailableCommands(-1).Count > 0;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IsActive"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Priority"));
            commandList.DoLayoutList();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AllowMovement"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MinimumPriority"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AllowHitReaction"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCommand(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty element = allowedCommands.GetArrayElementAtIndex(index);
            List<PlayerActionCommand> choices = GetAvailableCommands(index);
            string[] labels = new string[choices.Count];
            int[] values = new int[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                labels[i] = choices[i].ToString();
                values[i] = (int)choices[i];
            }

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            element.intValue = EditorGUI.IntPopup(rect, element.intValue, labels, values);
        }

        private void ShowAddMenu()
        {
            GenericMenu menu = new GenericMenu();
            List<PlayerActionCommand> choices = GetAvailableCommands(-1);
            foreach (PlayerActionCommand command in choices)
            {
                PlayerActionCommand selected = command;
                menu.AddItem(new GUIContent(selected.ToString()), false, () =>
                {
                    serializedObject.Update();
                    int index = allowedCommands.arraySize;
                    allowedCommands.InsertArrayElementAtIndex(index);
                    allowedCommands.GetArrayElementAtIndex(index).intValue = (int)selected;
                    serializedObject.ApplyModifiedProperties();
                });
            }
            if (choices.Count == 0)
                menu.AddDisabledItem(new GUIContent("All commands selected"));
            menu.ShowAsContext();
        }

        private List<PlayerActionCommand> GetAvailableCommands(int currentIndex)
        {
            var choices = new List<PlayerActionCommand>();
            foreach (PlayerActionCommand command in Enum.GetValues(typeof(PlayerActionCommand)))
            {
                bool usedElsewhere = false;
                for (int i = 0; i < allowedCommands.arraySize; i++)
                {
                    if (i != currentIndex &&
                        allowedCommands.GetArrayElementAtIndex(i).intValue == (int)command)
                    {
                        usedElsewhere = true;
                        break;
                    }
                }
                if (!usedElsewhere) choices.Add(command);
            }
            return choices;
        }
    }
}
