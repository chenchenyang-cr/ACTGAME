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
public static class UnagiRemainingAttackRepair
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string ModelPath = Root + "02_Idle/Unagi_Idle.fbx";
    const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    const string HiltPath = "Bip001/Bn_katana_hilt";
    const string Request = "Library/UnagiBatchImport/remaining_repair.request";
    const string Report = "Library/UnagiBatchImport/remaining_repair_report.txt";
    const string ValidateRequest = "Library/UnagiBatchImport/remaining_validate.request";
    const string ValidateReport = "Library/UnagiBatchImport/remaining_validation_report.txt";
    static readonly string[] Names = {
        "Unagi_Attack_04_02", "Unagi_Attack_05", "Unagi_Attack_05_02",
        "Unagi_Attack_06", "Unagi_Attack_Branch_01_Attack_01",
        "Unagi_Attack_Branch_02_Attack_01", "Unagi_Attack_Rush"
    };
    static readonly string[] SourceGuids = {
        "4388c4c46f54b9f419521cf550de65fa", "c5aa3d9c951541b4c9ae91765c578057",
        "04c6b138548bf7444820cb3126bbc13b", "d9ff61daed1934a4999d532da7441052",
        "ae867bccba6850643996b51855d4cbc8", "f5da4d51019a169469b6ff1c34cead50",
        "41a252b5eee44964d95c11f6c44e33c1"
    };
    static readonly int[] LastFrames = { 23, 41, 62, 27, 14, 14, 27 };
    static readonly string[] Properties = {
        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
        "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w"
    };

    static UnagiRemainingAttackRepair() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) { File.Delete(Request); RepairAll(); }
        if (File.Exists(ValidateRequest)) { File.Delete(ValidateRequest); ValidateAll(); }
    }

    static string SourcePath(string name) => Root + "01_Attack/" + name + ".fbx";
    static string FixedPath(string name) => Root + "01_Attack/" + name + ".anim";

    static AnimationClip SourceClip(string name) => AssetDatabase.LoadAllAssetsAtPath(SourcePath(name))
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));

    static GameObject PreviewModel(UnityEngine.SceneManagement.Scene scene)
    {
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
        var animator = instance.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        return instance;
    }

    [MenuItem("Tools/Unagi/Repair Remaining Attacks")]
    public static void RepairAll()
    {
        var report = new StringBuilder();
        bool allPassed = true;
        for (int i = 0; i < Names.Length; i++)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            AnimationClip repaired = null;
            try
            {
                string name = Names[i];
                string sourcePath = SourcePath(name);
                string fixedPath = FixedPath(name);
                report.AppendLine("=== " + name + " ===");
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath);
                if (existing != null)
                {
                    var existingInstance = PreviewModel(preview);
                    try
                    {
                        var existingTransforms = existingInstance.GetComponentsInChildren<Transform>(true);
                        Check(name, SourceClip(name), existing, existingInstance,
                            existingTransforms.Single(t => t.name == "Bip001 R Hand"),
                            existingTransforms.Single(t => t.name == "Bn_katana_hilt"), report);
                        report.AppendLine("PASS: previously saved independent clip rechecked.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(existingInstance); }
                    continue;
                }
                if (AssetDatabase.AssetPathToGUID(sourcePath) != SourceGuids[i])
                    throw new InvalidOperationException("Source FBX GUID changed since backup.");
                var source = SourceClip(name);
                if (Mathf.Abs(source.frameRate - 30f) > 0.001f ||
                    Mathf.Abs(source.length - LastFrames[i] / 30f) > 0.001f)
                    throw new InvalidOperationException("Source timing changed since backup.");
                var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
                if (modelImporter.globalScale != 100f || modelImporter.motionNodeName != "Root" ||
                    modelImporter.animationType != ModelImporterAnimationType.Generic)
                    throw new InvalidOperationException("Reference model import settings changed.");
                var instance = PreviewModel(preview);
                try
                {
                    var transforms = instance.GetComponentsInChildren<Transform>(true);
                    var hand = transforms.Single(t => t.name == "Bip001 R Hand");
                    var hilt = transforms.Single(t => t.name == "Bn_katana_hilt");
                    foreach (var binding in AnimationUtility.GetCurveBindings(source))
                        if (instance.transform.Find(binding.path) == null)
                            throw new InvalidOperationException("Missing model path: " + binding.path);
                    var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (renderers.Length == 0 || renderers.Any(r => r.sharedMesh == null || r.sharedMaterials.Any(m => m == null)))
                        throw new InvalidOperationException("Reference model has missing mesh or material.");
                    source.SampleAnimation(instance, 0f);
                    report.AppendLine($"model height {renderers.Max(r => r.bounds.max.y) - renderers.Min(r => r.bounds.min.y):F6} m, feet Y {renderers.Min(r => r.bounds.min.y):F6} m, root scale {instance.transform.localScale}");

                    var grips = new Vector3[LastFrames[i] + 1];
                    var gripRotations = new Quaternion[grips.Length];
                    for (int frame = 0; frame < grips.Length; frame++)
                    {
                        source.SampleAnimation(instance, frame / source.frameRate);
                        grips[frame] = hand.InverseTransformPoint(hilt.position);
                        gripRotations[frame] = Quaternion.Inverse(hand.rotation) * hilt.rotation;
                    }
                    if (name == "Unagi_Attack_06")
                    {
                        if (grips[6].magnitude < 0.12f || grips[5].magnitude > 0.08f || grips[7].magnitude > 0.08f)
                            throw new InvalidOperationException("Expected isolated frame 6 anomaly was not found.");
                        report.AppendLine($"frame 6 grip corrected from {grips[6].magnitude:F6} m to {grips[7].magnitude:F6} m");
                        grips[6] = grips[7];
                        gripRotations[6] = gripRotations[7];
                    }

                    int samplesPerFrame = name == "Unagi_Attack_05_02" ? 16 : 4;
                    var positions = new Vector3[LastFrames[i] * samplesPerFrame + 1];
                    var rotations = new Quaternion[positions.Length];
                    for (int step = 0; step < positions.Length; step++)
                    {
                        int frame = Mathf.Min(LastFrames[i] - 1, step / samplesPerFrame);
                        float fraction = step / (float)samplesPerFrame - frame;
                        source.SampleAnimation(instance, step / (source.frameRate * samplesPerFrame));
                        Vector3 desiredGrip = Vector3.Lerp(grips[frame], grips[frame + 1], fraction);
                        Quaternion desiredRotation = Quaternion.Slerp(gripRotations[frame], gripRotations[frame + 1], fraction);
                        positions[step] = hilt.parent.InverseTransformPoint(hand.TransformPoint(desiredGrip));
                        rotations[step] = Quaternion.Inverse(hilt.parent.rotation) * hand.rotation * desiredRotation;
                        if (step > 0 && Quaternion.Dot(rotations[step - 1], rotations[step]) < 0f)
                            rotations[step] = new Quaternion(-rotations[step].x, -rotations[step].y,
                                -rotations[step].z, -rotations[step].w);
                    }

                    repaired = UnityEngine.Object.Instantiate(source);
                    repaired.name = name;
                    AssetDatabase.CreateAsset(repaired, fixedPath);
                    SetDenseCurve(repaired, Properties[0], positions.Select(v => v.x).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[1], positions.Select(v => v.y).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[2], positions.Select(v => v.z).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[3], rotations.Select(q => q.x).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[4], rotations.Select(q => q.y).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[5], rotations.Select(q => q.z).ToArray(), samplesPerFrame);
                    SetDenseCurve(repaired, Properties[6], rotations.Select(q => q.w).ToArray(), samplesPerFrame);
                    EditorUtility.SetDirty(repaired);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(fixedPath, ImportAssetOptions.ForceUpdate);
                    repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath);
                    Check(name, source, repaired, instance, hand, hilt, report);
                    report.AppendLine("PASS: independent .anim saved and reimported.");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            catch (Exception e)
            {
                allPassed = false;
                report.AppendLine("FAIL: " + e);
                Debug.LogException(e);
                if (repaired != null) AssetDatabase.DeleteAsset(FixedPath(Names[i]));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        if (allPassed)
        {
            try { LinkExistingReferences(report); }
            catch (Exception e) { report.AppendLine("FAIL linking references: " + e); Debug.LogException(e); }
        }
        File.WriteAllText(Report, report.ToString());
    }

    static void SetDenseCurve(AnimationClip clip, string property, float[] values, int samplesPerFrame)
    {
        var binding = EditorCurveBinding.FloatCurve(HiltPath, typeof(Transform), property);
        var original = AnimationUtility.GetEditorCurve(clip, binding);
        if (original == null) throw new InvalidOperationException("Missing hilt curve: " + property);
        var keys = new Keyframe[values.Length];
        float interval = 1f / (samplesPerFrame * clip.frameRate);
        for (int i = 0; i < values.Length; i++)
        {
            float incoming = i == 0 ? original.keys[0].inTangent : (values[i] - values[i - 1]) / interval;
            float outgoing = i == values.Length - 1 ? original.keys[original.length - 1].outTangent :
                (values[i + 1] - values[i]) / interval;
            keys[i] = new Keyframe(i * interval, values[i], incoming, outgoing);
        }
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }

    static void Check(string name, AnimationClip source, AnimationClip repaired, GameObject instance,
        Transform hand, Transform hilt, StringBuilder report)
    {
        int index = Array.IndexOf(Names, name);
        if (repaired == null || repaired.name != name ||
            Mathf.Abs(repaired.frameRate - 30f) > 0.001f ||
            Mathf.Abs(repaired.length - LastFrames[index] / 30f) > 0.001f)
            throw new InvalidOperationException("Clip name or timing changed.");
        var oldBindings = AnimationUtility.GetCurveBindings(source);
        var newBindings = AnimationUtility.GetCurveBindings(repaired);
        if (oldBindings.Length != newBindings.Length)
            throw new InvalidOperationException("Binding count changed.");
        foreach (var binding in oldBindings)
        {
            if (instance.transform.Find(binding.path) == null)
                throw new InvalidOperationException("Missing model path: " + binding.path);
            if (binding.path == HiltPath && Properties.Contains(binding.propertyName)) continue;
            var before = AnimationUtility.GetEditorCurve(source, binding).keys;
            var after = AnimationUtility.GetEditorCurve(repaired, binding).keys;
            if (before.Length != after.Length)
                throw new InvalidOperationException("Untouched curve key count changed: " + binding.path);
            for (int key = 0; key < before.Length; key++)
                if (Mathf.Abs(before[key].time - after[key].time) > 0.000001f ||
                    Mathf.Abs(before[key].value - after[key].value) > 0.000001f ||
                    Mathf.Abs(before[key].inTangent - after[key].inTangent) > 0.0001f ||
                    Mathf.Abs(before[key].outTangent - after[key].outTangent) > 0.0001f)
                    throw new InvalidOperationException("Untouched curve changed: " + binding.path + "/" + binding.propertyName);
        }
        for (int frame = 0; frame <= LastFrames[index]; frame++)
        {
            if (name == "Unagi_Attack_06" && frame == 6) continue;
            source.SampleAnimation(instance, frame / 30f);
            Vector3 oldPosition = hilt.localPosition;
            Quaternion oldRotation = hilt.localRotation;
            repaired.SampleAnimation(instance, frame / 30f);
            if (Vector3.Distance(oldPosition, hilt.localPosition) > 0.0001f ||
                Quaternion.Angle(oldRotation, hilt.localRotation) > 0.05f)
                throw new InvalidOperationException("Authored integer pose changed: frame " + frame);
        }
        float largestExcess = 0f;
        float worstFrame = 0f;
        for (int frame = 0; frame < LastFrames[index]; frame++)
        {
            repaired.SampleAnimation(instance, frame / 30f);
            float startGrip = Vector3.Distance(hand.position, hilt.position);
            repaired.SampleAnimation(instance, (frame + 1) / 30f);
            float endGrip = Vector3.Distance(hand.position, hilt.position);
            float baseline = Mathf.Max(startGrip, endGrip);
            for (int quarter = 1; quarter < 16; quarter++)
            {
                float at = frame + quarter / 16f;
                repaired.SampleAnimation(instance, at / 30f);
                float excess = Vector3.Distance(hand.position, hilt.position) - baseline;
                if (excess > largestExcess) { largestExcess = excess; worstFrame = at; }
            }
        }
        if (largestExcess > 0.015f)
            throw new InvalidOperationException($"Hilt still jumps between frames near {worstFrame:F4}: {largestExcess:F6} m excess.");
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer.globalScale != 100f || importer.motionNodeName != "Root" ||
            importer.animationType != ModelImporterAnimationType.Generic ||
            instance.transform.localScale != Vector3.one)
            throw new InvalidOperationException("Reference model scale or rig changed.");
        report.AppendLine($"PASS: {name}, 30 FPS, frames 0-{LastFrames[index]}, {newBindings.Length} paths; all untouched curves and integer poses preserved.");
        report.AppendLine($"PASS: max frame-interpolation grip excess {largestExcess:F6} m at frame {worstFrame:F4}; model scale and rig unchanged.");
    }

    static void LinkExistingReferences(StringBuilder report)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Player_Swordman controller missing.");
        string[] linked = { "Unagi_Attack_05", "Unagi_Attack_06", "Unagi_Attack_Branch_01_Attack_01" };
        var states = new AnimatorState[linked.Length];
        var abilities = new AbilityScriptableObject[linked.Length];
        var repaired = new AnimationClip[linked.Length];
        for (int i = 0; i < linked.Length; i++)
        {
            string name = linked[i];
            var source = SourceClip(name);
            repaired[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(name));
            states[i] = controller.layers[0].stateMachine.states.Select(s => s.state)
                .Single(s => s.name == name);
            abilities[i] = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Sandbox/" + name + ".asset");
            if (repaired[i] == null || abilities[i] == null || states[i].motion != source || abilities[i].Clip != source)
                throw new InvalidOperationException("Animator or Ability source reference changed: " + name);
        }
        for (int i = 0; i < linked.Length; i++)
        {
            states[i].motion = repaired[i];
            abilities[i].Clip = repaired[i];
            EditorUtility.SetDirty(states[i]);
            EditorUtility.SetDirty(abilities[i]);
            report.AppendLine("PASS: Animator and Ability now use " + linked[i] + ".anim.");
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Unagi/Validate Remaining Attacks")]
    public static void ValidateAll()
    {
        var report = new StringBuilder();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var name in Names)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                AssetDatabase.ImportAsset(FixedPath(name), ImportAssetOptions.ForceUpdate);
                var source = SourceClip(name);
                var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath(name));
                var instance = PreviewModel(preview);
                try
                {
                    var transforms = instance.GetComponentsInChildren<Transform>(true);
                    Check(name, source, repaired, instance,
                        transforms.Single(t => t.name == "Bip001 R Hand"),
                        transforms.Single(t => t.name == "Bn_katana_hilt"), report);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
                if (name == "Unagi_Attack_05" || name == "Unagi_Attack_06" ||
                    name == "Unagi_Attack_Branch_01_Attack_01")
                {
                    var state = controller.layers[0].stateMachine.states.Select(s => s.state)
                        .Single(s => s.name == name);
                    var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(
                        "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Sandbox/" + name + ".asset");
                    if (state.motion != repaired || ability.Clip != repaired)
                        throw new InvalidOperationException("Animator/Ability reference mismatch: " + name);
                    report.AppendLine("PASS: Animator and Ability reference " + name + ".anim.");
                }
            }
            catch (Exception e) { report.AppendLine("FAIL " + name + ": " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        File.WriteAllText(ValidateReport, report.ToString());
    }
}
