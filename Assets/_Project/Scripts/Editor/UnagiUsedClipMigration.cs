using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiUsedClipMigration
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string ModelPath = Root + "02_Idle/Unagi_Idle.fbx";
    const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    const string AbilityRoot = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/";
    const string Request = "Library/UnagiBatchImport/used_clip_migration.request";
    const string Report = "Library/UnagiBatchImport/used_clip_migration_report.txt";
    const string ValidateRequest = "Library/UnagiBatchImport/used_clip_validate.request";
    const string ValidateReport = "Library/UnagiBatchImport/used_clip_validation_report.txt";

    static readonly string[] RelativePaths = {
        "01_Attack/Unagi_Attack_02_End", "01_Attack/Unagi_Attack_01_End",
        "01_Attack/Unagi_Attack_05_End", "03_Run/Unagi_Run",
        "03_Run/Unagi_Run_Start", "04_Walk/Unagi_Walk",
        "02_Idle/Unagi_Idle", "01_Attack/Unagi_Attack_03_End",
        "01_Attack/Unagi_Attack_03", "01_Attack/Unagi_Attack_06_End",
        "03_Run/Unagi_Run_End"
    };
    static readonly string[] StateNames = {
        "Unagi_Attack_02_End", "Unagi_Attack_01_End", "Unagi_Attack_05_End",
        "Loop", "Start", "Turn180", "Idle", "Unagi_Attack_03_End",
        "Unagi_Attack_03", "Unagi_Attack_06_End", "End"
    };
    static readonly string[] SourceGuids = {
        "3593bee0805750441bb0c03bf480a47d", "1eac3f4cd7749d1408c3819b3469acf2",
        "8bb320fee8f2bfe41ba48b4a44b934f0", "f2acf95bae9e2f04baf007791646b30f",
        "3fc5adcf291b62849895072085e4c509", "087794caf20e339458a8060339eae6e0",
        "bd2868add8424964c8b8bb98ecc06f78", "60837c75444ec2047a8c432ce3177925",
        "443f56ff3eef5ef499d98e9de553b249", "2725d13ca3e1bf242a2807b8db66bc9c",
        "af5134077e88e1b4ba4f1b7574c9f3eb"
    };
    static readonly int[] LastFrames = { 105, 100, 80, 15, 45, 35, 121, 97, 33, 116, 111 };
    static readonly string[] AbilityPaths = {
        "Swordman/Unagi_Attack_02_End.asset", "Swordman/Unagi_Attack_01_End.asset",
        "Sandbox/Unagi_Attack_05_End.asset", null, null, null, null,
        "Swordman/Unagi_Attack_03_End.asset", "Swordman/Unagi_Attack_03.asset",
        "Sandbox/Unagi_Attack_06_End.asset", null
    };

    static UnagiUsedClipMigration() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) { File.Delete(Request); Migrate(); }
        if (File.Exists(ValidateRequest)) { File.Delete(ValidateRequest); Validate(); }
    }

    static string SourcePath(int index) => Root + RelativePaths[index] + ".fbx";
    static string FixedPath(int index) => Root + RelativePaths[index] + ".anim";

    static AnimationClip SourceClip(int index) => AssetDatabase.LoadAllAssetsAtPath(SourcePath(index))
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));

    static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states) yield return child.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in States(child.stateMachine)) yield return state;
    }

    static AnimatorState State(AnimatorController controller, int index) =>
        controller.layers.SelectMany(layer => States(layer.stateMachine))
            .Single(s => s.name == StateNames[index]);

    static AbilityScriptableObject Ability(int index) => AbilityPaths[index] == null ? null :
        AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityRoot + AbilityPaths[index]);

    [MenuItem("Tools/Unagi/Migrate Used FBX Clips")]
    public static void Migrate()
    {
        var report = new StringBuilder();
        bool allPassed = true;
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new InvalidOperationException("Player_Swordman controller missing.");
            for (int i = 0; i < RelativePaths.Length; i++)
            {
                if (AssetDatabase.AssetPathToGUID(SourcePath(i)) != SourceGuids[i])
                    throw new InvalidOperationException("Source FBX GUID changed: " + RelativePaths[i]);
                var source = SourceClip(i);
                var state = State(controller, i);
                var ability = Ability(i);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(i));
                if (state.motion != source && (existing == null || state.motion != existing))
                    throw new InvalidOperationException("Animator state changed: " + StateNames[i]);
                if (AbilityPaths[i] != null && (ability == null ||
                    (ability.Clip != source && (existing == null || ability.Clip != existing))))
                    throw new InvalidOperationException("Ability clip changed: " + RelativePaths[i]);
                if (Mathf.Abs(source.frameRate - 30f) > 0.001f ||
                    Mathf.Abs(source.length - LastFrames[i] / 30f) > 0.001f)
                    throw new InvalidOperationException("Source timing changed: " + RelativePaths[i]);
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer.globalScale != 100f || importer.motionNodeName != "Root" ||
                importer.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Reference model import settings changed.");

            for (int i = 0; i < RelativePaths.Length; i++)
            {
                var preview = EditorSceneManager.NewPreviewScene();
                AnimationClip created = null;
                try
                {
                    report.AppendLine("=== " + RelativePaths[i] + " ===");
                    var source = SourceClip(i);
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(i));
                    if (existing == null)
                    {
                        created = UnityEngine.Object.Instantiate(source);
                        created.name = Path.GetFileName(RelativePaths[i]);
                        AssetDatabase.CreateAsset(created, FixedPath(i));
                        EditorUtility.SetDirty(created);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(FixedPath(i), ImportAssetOptions.ForceUpdate);
                    }
                    var independent = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(i));
                    Check(i, source, independent, preview, report);
                    report.AppendLine("PASS: standalone .anim imported; FBX source kept.");
                }
                catch (Exception e)
                {
                    allPassed = false;
                    report.AppendLine("FAIL: " + e);
                    Debug.LogException(e);
                    if (created != null) AssetDatabase.DeleteAsset(FixedPath(i));
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }

            if (allPassed)
            {
                for (int i = 0; i < RelativePaths.Length; i++)
                {
                    var independent = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(i));
                    var state = State(controller, i);
                    state.motion = independent;
                    EditorUtility.SetDirty(state);
                    var ability = Ability(i);
                    if (ability != null) { ability.Clip = independent; EditorUtility.SetDirty(ability); }
                }
                var attack04Source = AssetDatabase.LoadAllAssetsAtPath(Root + "01_Attack/Unagi_Attack_04.fbx")
                    .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
                var attack04Fixed = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "01_Attack/Unagi_Attack_04.anim");
                var sandboxAbility = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                    AbilityRoot + "Sandbox/NewAbilityScriptableObject_5.asset");
                if (sandboxAbility == null || attack04Fixed == null ||
                    (sandboxAbility.Clip != attack04Source && sandboxAbility.Clip != attack04Fixed))
                    throw new InvalidOperationException("Sandbox Attack 04 ability reference changed.");
                sandboxAbility.Clip = attack04Fixed;
                EditorUtility.SetDirty(sandboxAbility);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: 11 Animator states, 6 paired Abilities, and the Attack 04 sandbox Ability use independent .anim clips.");
            }
        }
        catch (Exception e) { report.AppendLine("FAIL setup/link: " + e); Debug.LogException(e); }
        finally { File.WriteAllText(Report, report.ToString()); }
    }

    static void Check(int index, AnimationClip source, AnimationClip independent,
        UnityEngine.SceneManagement.Scene preview, StringBuilder report)
    {
        if (independent == null || independent.name != Path.GetFileName(RelativePaths[index]) ||
            Mathf.Abs(independent.frameRate - source.frameRate) > 0.001f ||
            Mathf.Abs(independent.length - source.length) > 0.001f ||
            independent.wrapMode != source.wrapMode)
            throw new InvalidOperationException($"Clip identity, timing or wrap mode changed: " +
                $"source={source.name}/{source.frameRate}/{source.length}/{source.wrapMode}, " +
                $"independent={independent?.name}/{independent?.frameRate}/{independent?.length}/{independent?.wrapMode}.");
        var beforeBindings = AnimationUtility.GetCurveBindings(source);
        var afterBindings = AnimationUtility.GetCurveBindings(independent);
        if (beforeBindings.Length != afterBindings.Length)
            throw new InvalidOperationException("Curve binding count changed.");
        for (int i = 0; i < beforeBindings.Length; i++)
        {
            var binding = beforeBindings[i];
            if (!afterBindings.Contains(binding))
                throw new InvalidOperationException("Curve binding missing: " + binding.path + "/" + binding.propertyName);
            var before = AnimationUtility.GetEditorCurve(source, binding).keys;
            var after = AnimationUtility.GetEditorCurve(independent, binding).keys;
            if (before.Length != after.Length)
                throw new InvalidOperationException("Curve key count changed: " + binding.path);
            for (int key = 0; key < before.Length; key++)
                if (Mathf.Abs(before[key].time - after[key].time) > 0.000001f ||
                    Mathf.Abs(before[key].value - after[key].value) > 0.000001f ||
                    Mathf.Abs(before[key].inTangent - after[key].inTangent) > 0.0001f ||
                    Mathf.Abs(before[key].outTangent - after[key].outTangent) > 0.0001f)
                    throw new InvalidOperationException("Curve key changed: " + binding.path + "/" + binding.propertyName);
        }
        var beforeEvents = AnimationUtility.GetAnimationEvents(source);
        var afterEvents = AnimationUtility.GetAnimationEvents(independent);
        if (beforeEvents.Length != afterEvents.Length)
            throw new InvalidOperationException("Animation event count changed.");
        for (int i = 0; i < beforeEvents.Length; i++)
            if (Mathf.Abs(beforeEvents[i].time - afterEvents[i].time) > 0.000001f ||
                beforeEvents[i].functionName != afterEvents[i].functionName ||
                beforeEvents[i].stringParameter != afterEvents[i].stringParameter ||
                beforeEvents[i].intParameter != afterEvents[i].intParameter ||
                Mathf.Abs(beforeEvents[i].floatParameter - afterEvents[i].floatParameter) > 0.000001f)
                throw new InvalidOperationException("Animation event changed.");
        var beforeReferences = AnimationUtility.GetObjectReferenceCurveBindings(source);
        var afterReferences = AnimationUtility.GetObjectReferenceCurveBindings(independent);
        if (beforeReferences.Length != afterReferences.Length)
            throw new InvalidOperationException("Object curve binding count changed.");

        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
        try
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
                throw new InvalidOperationException("Reference model Avatar missing.");
            animator.enabled = false;
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0 || renderers.Any(r => r.sharedMesh == null || r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("Reference model mesh or material missing.");
            foreach (var binding in beforeBindings)
                if (instance.transform.Find(binding.path) == null)
                    throw new InvalidOperationException("Model path missing: " + binding.path);
            var positions = new Vector3[transforms.Length];
            var rotations = new Quaternion[transforms.Length];
            var scales = new Vector3[transforms.Length];
            for (int step = 0; step <= LastFrames[index] * 2; step++)
            {
                float time = step / 60f;
                source.SampleAnimation(instance, time);
                for (int bone = 0; bone < transforms.Length; bone++)
                {
                    positions[bone] = transforms[bone].localPosition;
                    rotations[bone] = transforms[bone].localRotation;
                    scales[bone] = transforms[bone].localScale;
                }
                independent.SampleAnimation(instance, time);
                for (int bone = 0; bone < transforms.Length; bone++)
                    if (Vector3.Distance(positions[bone], transforms[bone].localPosition) > 0.00001f ||
                        Quaternion.Angle(rotations[bone], transforms[bone].localRotation) > 0.01f ||
                        Vector3.Distance(scales[bone], transforms[bone].localScale) > 0.00001f)
                        throw new InvalidOperationException($"Pose changed at frame {step / 2f:F1}, bone {transforms[bone].name}.");
            }
            if (instance.transform.localScale != Vector3.one)
                throw new InvalidOperationException("Reference model root scale changed.");
            report.AppendLine($"PASS: 30 FPS, frames 0-{LastFrames[index]}, {beforeBindings.Length} bindings, {beforeEvents.Length} events; every integer and half-frame pose matches source.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    [MenuItem("Tools/Unagi/Validate Migrated FBX Clips")]
    public static void Validate()
    {
        var report = new StringBuilder();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        for (int i = 0; i < RelativePaths.Length; i++)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                AssetDatabase.ImportAsset(FixedPath(i), ImportAssetOptions.ForceUpdate);
                var independent = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(i));
                Check(i, SourceClip(i), independent, preview, report);
                if (State(controller, i).motion != independent ||
                    (Ability(i) != null && Ability(i).Clip != independent))
                    throw new InvalidOperationException("Animator or Ability did not migrate.");
                report.AppendLine("PASS: references use " + FixedPath(i));
            }
            catch (Exception e) { report.AppendLine("FAIL " + RelativePaths[i] + ": " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        var attack04 = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "01_Attack/Unagi_Attack_04.anim");
        var sandboxAbility = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
            AbilityRoot + "Sandbox/NewAbilityScriptableObject_5.asset");
        report.AppendLine(sandboxAbility != null && sandboxAbility.Clip == attack04
            ? "PASS: Attack 04 sandbox Ability uses repaired .anim."
            : "FAIL: Attack 04 sandbox Ability still references FBX.");
        File.WriteAllText(ValidateReport, report.ToString());
    }
}
