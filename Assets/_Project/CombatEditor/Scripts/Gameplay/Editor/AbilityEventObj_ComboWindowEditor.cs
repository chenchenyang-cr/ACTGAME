using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace CombatEditor
{
    [CustomEditor(typeof(AbilityEventObj_ComboWindow))]
    public sealed class AbilityEventObj_ComboWindowEditor : Editor
    {
        private SerializedProperty priorityProperty;
        private SerializedProperty commandIdProperty;
        private SerializedProperty nextAbilityProperty;
        private SerializedProperty consumeBufferedInputProperty;

        private void OnEnable()
        {
            priorityProperty = serializedObject.FindProperty("Priority");
            commandIdProperty = serializedObject.FindProperty("CommandId");
            nextAbilityProperty = serializedObject.FindProperty("NextAbility");
            consumeBufferedInputProperty = serializedObject.FindProperty("ConsumeBufferedInput");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(priorityProperty);
            EditorGUILayout.PropertyField(commandIdProperty);
            DrawNextAbilitySelector();
            EditorGUILayout.PropertyField(consumeBufferedInputProperty);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNextAbilitySelector()
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect contentRect = EditorGUI.PrefixLabel(row, new GUIContent("Next Ability"));
            const float searchButtonWidth = 28f;
            const float gap = 2f;
            Rect dropdownRect = new Rect(
                contentRect.x,
                contentRect.y,
                contentRect.width - searchButtonWidth - gap,
                contentRect.height);
            Rect searchRect = new Rect(
                dropdownRect.xMax + gap,
                contentRect.y,
                searchButtonWidth,
                contentRect.height);

            AbilityScriptableObject current =
                nextAbilityProperty.objectReferenceValue as AbilityScriptableObject;
            string currentName = current != null ? current.name : "None";

            if (EditorGUI.DropdownButton(
                    dropdownRect,
                    new GUIContent(currentName, current != null ? AssetDatabase.GetAssetPath(current) : ""),
                    FocusType.Keyboard))
            {
                ShowDropdownMenu(current);
            }

            GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
            searchContent.tooltip = "Search abilities";
            if (GUI.Button(searchRect, searchContent))
            {
                OpenSearchWindow(searchRect);
            }
        }

        private void ShowDropdownMenu(AbilityScriptableObject current)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), current == null, () => AssignNextAbility(null));
            menu.AddSeparator("");

            IReadOnlyList<AbilityAssetEntry> abilities = AbilityAssetEntry.FindAll();
            foreach (AbilityAssetEntry entry in abilities)
            {
                AbilityScriptableObject ability = entry.Ability;
                menu.AddItem(
                    new GUIContent(entry.MenuPath),
                    ability == current,
                    () => AssignNextAbility(ability));
            }

            if (abilities.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Ability Assets Found"));
            }

            menu.ShowAsContext();
        }

        private void OpenSearchWindow(Rect buttonRect)
        {
            AbilitySearchProvider provider = CreateInstance<AbilitySearchProvider>();
            provider.hideFlags = HideFlags.HideAndDontSave;
            provider.Initialize(AbilityAssetEntry.FindAll(), AssignNextAbility);

            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(
                new Vector2(buttonRect.xMax, buttonRect.yMax));
            SearchWindow.Open(new SearchWindowContext(screenPosition, 420f, 480f), provider);
        }

        private void AssignNextAbility(AbilityScriptableObject ability)
        {
            AbilityEventObj_ComboWindow comboWindow = target as AbilityEventObj_ComboWindow;
            if (comboWindow == null || comboWindow.NextAbility == ability)
            {
                return;
            }

            Undo.RecordObject(comboWindow, "Set Next Combo Ability");
            comboWindow.NextAbility = ability;
            EditorUtility.SetDirty(comboWindow);
            serializedObject.Update();
            Repaint();
        }
    }

    internal readonly struct AbilityAssetEntry
    {
        private const string AbilityRoot = "Assets/ScriptableObjects/Abilities/";

        public AbilityScriptableObject Ability { get; }
        public string AssetPath { get; }
        public string Folder { get; }
        public string MenuPath { get; }

        private AbilityAssetEntry(AbilityScriptableObject ability, string assetPath)
        {
            Ability = ability;
            AssetPath = assetPath;

            string relativePath = assetPath.StartsWith(AbilityRoot, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(AbilityRoot.Length)
                : assetPath.Substring("Assets/".Length);
            string withoutExtension = Path.ChangeExtension(relativePath, null).Replace('\\', '/');
            MenuPath = withoutExtension;
            Folder = Path.GetDirectoryName(withoutExtension)?.Replace('\\', '/') ?? "Other";
            if (string.IsNullOrEmpty(Folder))
            {
                Folder = "Other";
            }
        }

        public static IReadOnlyList<AbilityAssetEntry> FindAll()
        {
            return AssetDatabase.FindAssets("t:AbilityScriptableObject")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(path => new AbilityAssetEntry(
                    AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path),
                    path))
                .Where(entry => entry.Ability != null)
                .OrderBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    internal sealed class AbilitySearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private IReadOnlyList<AbilityAssetEntry> abilities;
        private Action<AbilityScriptableObject> onSelected;

        public void Initialize(
            IReadOnlyList<AbilityAssetEntry> entries,
            Action<AbilityScriptableObject> selectionCallback)
        {
            abilities = entries;
            onSelected = selectionCallback;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Select Next Ability"), 0),
                new SearchTreeEntry(new GUIContent("None"))
                {
                    level = 1,
                    userData = null
                }
            };

            foreach (IGrouping<string, AbilityAssetEntry> folderGroup in
                     abilities.GroupBy(entry => entry.Folder)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(folderGroup.Key), 1));
                foreach (AbilityAssetEntry entry in
                         folderGroup.OrderBy(item => item.Ability.name, StringComparer.OrdinalIgnoreCase))
                {
                    GUIContent content = new GUIContent(
                        entry.Ability.name,
                        EditorGUIUtility.ObjectContent(entry.Ability, typeof(AbilityScriptableObject)).image,
                        entry.AssetPath);
                    tree.Add(new SearchTreeEntry(content)
                    {
                        level = 2,
                        userData = entry.Ability
                    });
                }
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            onSelected?.Invoke(entry.userData as AbilityScriptableObject);
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    DestroyImmediate(this);
                }
            };
            return true;
        }
    }
}
