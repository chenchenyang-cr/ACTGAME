using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SwordmanAttackSetup
{
    const string Folder = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Swordman";
    const string Request = "Library/SwordmanAttackSetup.request";
    const string Report = "Library/SwordmanAttackSetupReport.txt";
    static SwordmanAttackSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Unagi/Configure Swordman Attacks 1-3")]
    public static void Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Main_Nodachi.unity");
        if (!scene.isLoaded) throw new InvalidOperationException("Open Main_Nodachi before configuring Swordman attacks.");
        var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerStateMachine>(true))
            .Single(p => p.name == "Player_Swordman");
        var combat = player.GetComponent<CombatController>() ?? player.GetComponentInChildren<CombatController>();
        var adapter = player.GetComponent<PlayerCombatAdapter>();
        var animator = player.GetComponentInChildren<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (combat == null || adapter == null || controller == null) throw new InvalidOperationException("Missing Swordman components.");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/_Project/CombatEditor/ScriptableObjects/Abilities", "Swordman");
        var attacks = new AbilityScriptableObject[3];
        var ends = new AbilityScriptableObject[3];
        var report = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            attacks[i] = GetAbility($"Swordman_Attack_{i + 1:00}", $"Unagi_Attack_{i + 1:00}", controller, report);
            ends[i] = GetAbility($"Swordman_Attack_{i + 1:00}_End", $"Unagi_Attack_{i + 1:00}_End", controller, report);
        }
        for (int i = 0; i < 3; i++)
        {
            // Seed new assets only; rerunning setup preserves hand-tuned event tracks.
            if (attacks[i].events.Count == 0)
            {
                Add<AbilityEventObj_RotationWindow>(attacks[i], 0f, 1f);
                if (i < 2)
                {
                    var combo = Add<AbilityEventObj_ComboWindow>(attacks[i], 0.6f, 0.95f);
                    combo.NextAbility = attacks[i + 1];
                    combo.CommandId = "LightAttack";
                    combo.ConsumeBufferedInput = true;
                    EditorUtility.SetDirty(combo);
                }
                var cancel = Add<AbilityEventObj_InterruptWindow>(attacks[i], 0.75f, 1f);
                cancel.AllowMovement = true;
                cancel.AllowedCommandIds = "";
                EditorUtility.SetDirty(cancel);
            }
            if (ends[i].events.Count == 0)
            {
                Add<AbilityEventObj_RotationWindow>(ends[i], 0f, 1f);
                var cancel = Add<AbilityEventObj_InterruptWindow>(ends[i], 0.5f, 1f);
                cancel.AllowMovement = true;
                cancel.AllowedCommandIds = "LightAttack";
                EditorUtility.SetDirty(cancel);
            }
            EditorUtility.SetDirty(attacks[i]);
            EditorUtility.SetDirty(ends[i]);
        }
        Undo.RecordObject(adapter, "Configure Swordman attacks");
        adapter.enabled = true;
        var commandInput = player.GetComponent<PlayerCommandInputAdapter>();
        if (commandInput == null) commandInput = Undo.AddComponent<PlayerCommandInputAdapter>(player.gameObject);
        Undo.RecordObject(commandInput, "Enable Swordman command input");
        commandInput.enabled = true;
        EditorUtility.SetDirty(commandInput);
        var so = new SerializedObject(adapter);
        so.FindProperty("firstLightAttack").objectReferenceValue = attacks[0];
        var recoveries = so.FindProperty("attackRecoveries");
        recoveries.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var entry = recoveries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Attack").objectReferenceValue = attacks[i];
            entry.FindPropertyRelative("End").objectReferenceValue = ends[i];
            entry.FindPropertyRelative("StartTime").floatValue = 0.95f;
            entry.FindPropertyRelative("BlendDuration").floatValue = 0.08f;
        }
        so.ApplyModifiedProperties();
        var state = new SerializedObject(player);
        state.FindProperty("locomotionOnly").boolValue = false;
        state.ApplyModifiedProperties();
        Undo.RecordObject(combat, "Register Swordman attacks");
        combat.enabled = true;
        var group = combat.CombatDatas.FirstOrDefault(g => g.Label == "Swordman Attacks");
        if (group == null) { group = new CombatGroup { Label = "Swordman Attacks", CombatObjs = new List<AbilityScriptableObject>() }; combat.CombatDatas.Add(group); }
        foreach (var ability in attacks.Concat(ends))
            if (!combat.CombatDatas.Any(g => g.CombatObjs != null && g.CombatObjs.Contains(ability))) group.CombatObjs.Add(ability);
        EditorUtility.SetDirty(combat);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        report.Add("PASS: 3 attacks, 3 End abilities, combo 1 -> 2 -> 3; recovery 0.95 / blend 0.08; locomotionOnly off.");
        File.WriteAllLines(Report, report);
    }

    static AbilityScriptableObject GetAbility(string name, string clipName, AnimatorController controller, List<string> report)
    {
        var state = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.name == clipName);
        var clip = state.motion as AnimationClip;
        if (clip == null || clip.name != clipName || clip.isLooping) throw new InvalidOperationException("Invalid attack clip: " + clipName);
        // Use the same unit scale and motion root as the controlled Main Unagi model.
        // Original attack imports otherwise shrink the skeleton relative to locomotion.
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;
        if (importer != null)
        {
            var source = (ModelImporter)AssetImporter.GetAtPath("Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/02_Idle/Unagi_Idle.fbx");
            var sourceSettings = new SerializedObject(source);
            var settings = new SerializedObject(importer);
            string root = sourceSettings.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue;
            if (importer.globalScale != source.globalScale || importer.motionNodeName != source.motionNodeName ||
                settings.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue != root)
            {
                importer.globalScale = source.globalScale;
                importer.motionNodeName = source.motionNodeName;
                settings.Update();
                settings.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue = root;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var definitions = importer.clipAnimations;
                foreach (var definition in definitions)
                {
                    definition.loopTime = false;
                    definition.lockRootRotation = false;
                    definition.lockRootPositionXZ = false;
                    definition.lockRootHeightY = true;
                }
                importer.clipAnimations = definitions;
                importer.SaveAndReimport();
            }
        }
        string path = Folder + "/" + name + ".asset";
        var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilityScriptableObject>();
            ability.name = name;
            ability.Clip = clip;
            ability.AbilityType = AbilityScriptableObject.AbilityTypes.OneShot;
            AssetDatabase.CreateAsset(ability, path);
        }
        if (ability.Clip != clip) throw new InvalidOperationException("Unexpected configured clip: " + name);
        report.Add(name + " -> " + clip.name + " (" + clip.length + " seconds)");
        return ability;
    }

    static T Add<T>(AbilityScriptableObject ability, float start, float end) where T : AbilityEventObj_GameplayWindow
    {
        var window = ScriptableObject.CreateInstance<T>();
        window.name = typeof(T).Name.Replace("AbilityEventObj_", "");
        window.IsActive = true;
        AssetDatabase.AddObjectToAsset(window, ability);
        ability.events.Add(new AbilityEvent { Obj = window, EventRange = new Vector2(start, end) });
        return window;
    }
}
