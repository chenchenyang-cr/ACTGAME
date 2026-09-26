using System;
using System.IO;
using System.Linq;
using System.Text;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiAttack02Repair
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string ModelPath = Root + "02_Idle/Unagi_Idle.fbx";
    const string SourcePath = Root + "01_Attack/Unagi_Attack_02.fbx";
    const string FixedPath = Root + "01_Attack/Unagi_Attack_02.anim";
    const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    const string AbilityPath = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Swordman/Unagi_Attack_02.asset";
    const string HiltPath = "Bip001/Bn_katana_hilt";
    const string Request = "Library/UnagiBatchImport/attack02_unity_diagnose.request";
    const string Report = "Library/UnagiBatchImport/attack02_unity_diagnose.csv";
    const string RepairRequest = "Library/UnagiBatchImport/attack02_unity_repair.request";
    const string RepairReport = "Library/UnagiBatchImport/attack02_repair_report.txt";
    const string ValidateRequest = "Library/UnagiBatchImport/attack02_unity_validate.request";
    const string ValidateReport = "Library/UnagiBatchImport/attack02_validation_report.txt";

    static UnagiAttack02Repair() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) { File.Delete(Request); Diagnose(); }
        if (File.Exists(RepairRequest)) { File.Delete(RepairRequest); Repair(); }
        if (File.Exists(ValidateRequest)) { File.Delete(ValidateRequest); Validate(); }
    }

    [MenuItem("Tools/Unagi/Diagnose Attack 02")]
    public static void Diagnose()
    {
        var output = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var hand = transforms.Single(t => t.name == "Bip001 R Hand");
                var hilt = transforms.Single(t => t.name == "Bn_katana_hilt");
                var bones = transforms.Where(t => t.name.Contains("R Finger") || t.name.Contains("R Hand"))
                    .OrderBy(t => t.name).ToArray();
                output.AppendLine($"clip,{clip.name},fps,{clip.frameRate},length,{clip.length},lastFrame,{clip.length * clip.frameRate}");
                output.AppendLine($"modelGlobalScale,{((ModelImporter)AssetImporter.GetAtPath(ModelPath)).globalScale},rootScale,{instance.transform.localScale}");
                output.AppendLine("bones," + string.Join(";", bones.Select(t => t.name)));
                output.AppendLine("frame,hiltHandDistance,gripRotation,handScale,hiltScale,maxFingerStep,maxFingerBone");
                Quaternion[] previous = null;
                for (int step = 0; step <= 36; step++)
                {
                    clip.SampleAnimation(instance, step * 0.5f / clip.frameRate);
                    var rotations = bones.Select(t => t.localRotation).ToArray();
                    float max = 0f;
                    string maxBone = "";
                    if (previous != null)
                        for (int i = 0; i < bones.Length; i++)
                        {
                            float angle = Quaternion.Angle(previous[i], rotations[i]);
                            if (angle > max) { max = angle; maxBone = bones[i].name; }
                        }
                    output.AppendLine($"{step * 0.5f},{Vector3.Distance(hand.position, hilt.position):F6}," +
                        $"{Quaternion.Angle(hand.rotation, hilt.rotation):F3},{hand.lossyScale},{hilt.lossyScale},{max:F3},{maxBone}");
                    previous = rotations;
                }
                output.AppendLine("bindings,path,property,keys");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.path.Contains("R Hand") || binding.path.Contains("R Finger") ||
                        binding.path.Contains("katana_hilt"))
                        output.AppendLine($"binding,{binding.path},{binding.propertyName}," +
                            AnimationUtility.GetEditorCurve(clip, binding).length);
                output.AppendLine("detailFrame,bone,localPosition,localRotation,worldPosition,worldRotation");
                for (int step = 8; step <= 16; step++)
                {
                    clip.SampleAnimation(instance, step * 0.5f / clip.frameRate);
                    foreach (var bone in new[] { hand, hilt })
                        output.AppendLine($"{step * 0.5f},{bone.name},{bone.localPosition},{bone.localRotation}," +
                            $"{bone.position},{bone.rotation}");
                }
                output.AppendLine("curve,path,property,frame,value,inTangent,outTangent");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if ((binding.path.EndsWith("R Hand") || binding.path.EndsWith("katana_hilt")) &&
                        (binding.propertyName.Contains("Rotation") || binding.propertyName.Contains("Position")))
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        foreach (var key in curve.keys.Where(k => k.time * clip.frameRate >= 4f && k.time * clip.frameRate <= 8f))
                            output.AppendLine($"curve,{binding.path},{binding.propertyName}," +
                                $"{key.time * clip.frameRate:F3},{key.value:F9},{key.inTangent:F9},{key.outTangent:F9}");
                    }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e) { output.AppendLine("FAIL," + e); Debug.LogException(e); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllText(Report, output.ToString());
        }
    }

    [MenuItem("Tools/Unagi/Repair Attack 02 Grip Interpolation")]
    public static void Repair()
    {
        var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        AnimationClip fixedClip = null;
        bool linked = false;
        try
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath) != null)
                throw new InvalidOperationException("Attack 02 standalone clip already exists; refusing to overwrite it.");
            var source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            if (Mathf.Abs(source.frameRate - 30f) > 0.001f ||
                Mathf.Abs(source.length - 0.6f) > 0.001f)
                throw new InvalidOperationException("Source timing differs from the backed up 0-18 frame, 30 FPS clip.");
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (modelImporter.globalScale != 100f || modelImporter.motionNodeName != "Root" ||
                modelImporter.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Reference model import settings changed.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                var hand = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bip001 R Hand");
                var hilt = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bn_katana_hilt");
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0 || renderers.Any(r => r.sharedMesh == null || r.sharedMaterials.Any(m => m == null)))
                    throw new InvalidOperationException("Reference model has missing mesh or material.");
                source.SampleAnimation(instance, 0f);
                float modelBottom = renderers.Min(r => r.bounds.min.y);
                float modelTop = renderers.Max(r => r.bounds.max.y);
                report.AppendLine($"reference model height {modelTop - modelBottom:F6} m, feet Y {modelBottom:F6} m, " +
                    $"root scale {instance.transform.localScale}, avatar {animator?.avatar?.name}");
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                    if (instance.transform.Find(binding.path) == null)
                        throw new InvalidOperationException("Model is missing animation path: " + binding.path);

                Vector3[] gripPositions = new Vector3[3];
                Quaternion[] gripRotations = new Quaternion[3];
                for (int frame = 5; frame <= 7; frame++)
                {
                    source.SampleAnimation(instance, frame / source.frameRate);
                    gripPositions[frame - 5] = hand.InverseTransformPoint(hilt.position);
                    gripRotations[frame - 5] = Quaternion.Inverse(hand.rotation) * hilt.rotation;
                    report.AppendLine($"source frame {frame} grip distance {gripPositions[frame - 5].magnitude:F6} m");
                }

                const int samplesPerFrame = 4;
                Vector3[] positions = new Vector3[2 * samplesPerFrame + 1];
                Quaternion[] rotations = new Quaternion[positions.Length];
                for (int step = 0; step < positions.Length; step++)
                {
                    float frame = 5f + step / (float)samplesPerFrame;
                    int segment = Mathf.Min(1, Mathf.FloorToInt(frame) - 5);
                    float fraction = frame - (5 + segment);
                    source.SampleAnimation(instance, frame / source.frameRate);
                    Vector3 gripPosition = Vector3.Lerp(gripPositions[segment], gripPositions[segment + 1], fraction);
                    Quaternion gripRotation = Quaternion.Slerp(gripRotations[segment], gripRotations[segment + 1], fraction);
                    Vector3 worldPosition = hand.TransformPoint(gripPosition);
                    Quaternion worldRotation = hand.rotation * gripRotation;
                    positions[step] = hilt.parent.InverseTransformPoint(worldPosition);
                    rotations[step] = Quaternion.Inverse(hilt.parent.rotation) * worldRotation;
                    if (step > 0 && Quaternion.Dot(rotations[step - 1], rotations[step]) < 0f)
                        rotations[step] = new Quaternion(-rotations[step].x, -rotations[step].y,
                            -rotations[step].z, -rotations[step].w);
                }

                fixedClip = UnityEngine.Object.Instantiate(source);
                fixedClip.name = "Unagi_Attack_02";
                AssetDatabase.CreateAsset(fixedClip, FixedPath);
                SetDenseCurve(fixedClip, "m_LocalPosition.x", positions.Select(v => v.x).ToArray());
                SetDenseCurve(fixedClip, "m_LocalPosition.y", positions.Select(v => v.y).ToArray());
                SetDenseCurve(fixedClip, "m_LocalPosition.z", positions.Select(v => v.z).ToArray());
                SetDenseCurve(fixedClip, "m_LocalRotation.x", rotations.Select(q => q.x).ToArray());
                SetDenseCurve(fixedClip, "m_LocalRotation.y", rotations.Select(q => q.y).ToArray());
                SetDenseCurve(fixedClip, "m_LocalRotation.z", rotations.Select(q => q.z).ToArray());
                SetDenseCurve(fixedClip, "m_LocalRotation.w", rotations.Select(q => q.w).ToArray());
                EditorUtility.SetDirty(fixedClip);

                if (Mathf.Abs(fixedClip.frameRate - source.frameRate) > 0.001f ||
                    Mathf.Abs(fixedClip.length - source.length) > 0.001f)
                    throw new InvalidOperationException("Output clip timing changed.");
                if (AnimationUtility.GetCurveBindings(fixedClip).Length != AnimationUtility.GetCurveBindings(source).Length)
                    throw new InvalidOperationException("Output clip binding count changed.");
                float maximumGrip = 0f;
                for (int step = 0; step <= 32; step++)
                {
                    float frame = 5f + step * 0.0625f;
                    source.SampleAnimation(instance, frame / source.frameRate);
                    float before = Vector3.Distance(hand.position, hilt.position);
                    fixedClip.SampleAnimation(instance, frame / source.frameRate);
                    float after = Vector3.Distance(hand.position, hilt.position);
                    maximumGrip = Mathf.Max(maximumGrip, after);
                    report.AppendLine($"frame {frame:F2}: source {before:F6} m, repaired {after:F6} m");
                }
                if (maximumGrip > 0.08f)
                    throw new InvalidOperationException("Repair still separates hilt from hand (max " + maximumGrip + " m).");
                for (int frame = 0; frame <= 18; frame++)
                {
                    source.SampleAnimation(instance, frame / source.frameRate);
                    Vector3 oldPosition = hilt.localPosition;
                    Quaternion oldRotation = hilt.localRotation;
                    fixedClip.SampleAnimation(instance, frame / source.frameRate);
                    if (Vector3.Distance(oldPosition, hilt.localPosition) > 0.0001f ||
                        Quaternion.Angle(oldRotation, hilt.localRotation) > 0.05f)
                        throw new InvalidOperationException("Original pose changed at frame " + frame);
                }
                report.AppendLine("PASS: all integer frame hilt poses unchanged, including frames outside repair.");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                var state = controller.layers[0].stateMachine.states.Select(s => s.state)
                    .Single(s => s.name == "Unagi_Attack_02");
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityPath);
                if (state.motion != source || ability.Clip != source ||
                    AssetDatabase.AssetPathToGUID(SourcePath) != "7179a939ab1a6494fbef07138745312c")
                    throw new InvalidOperationException("Attack 02 source references changed since backup.");
                state.motion = fixedClip;
                ability.Clip = fixedClip;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(ability);
                AssetDatabase.SaveAssets();
                linked = true;
                report.AppendLine("PASS: Animator state and Ability use the same standalone clip.");
                report.AppendLine("PASS: reference model import scale 100, Generic rig, Root motion node unchanged.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e)
        {
            report.AppendLine("FAIL: " + e);
            Debug.LogException(e);
            if (!linked && fixedClip != null) AssetDatabase.DeleteAsset(FixedPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllText(RepairReport, report.ToString());
        }
    }

    static void SetDenseCurve(AnimationClip clip, string property, float[] values)
    {
        var binding = EditorCurveBinding.FloatCurve(HiltPath, typeof(Transform), property);
        var original = AnimationUtility.GetEditorCurve(clip, binding);
        if (original == null) throw new InvalidOperationException("Missing hilt curve " + property);
        float rate = clip.frameRate;
        var start = original.keys.Single(k => Mathf.Abs(k.time * rate - 5f) < 0.001f);
        var end = original.keys.Single(k => Mathf.Abs(k.time * rate - 7f) < 0.001f);
        var keys = original.keys.Where(k => k.time * rate < 5f - 0.001f).ToList();
        for (int i = 0; i < values.Length; i++)
        {
            float time = (5f + i * 0.25f) / rate;
            float incoming = i == 0 ? start.inTangent : (values[i] - values[i - 1]) / (0.25f / rate);
            float outgoing = i == values.Length - 1 ? end.outTangent :
                (values[i + 1] - values[i]) / (0.25f / rate);
            keys.Add(new Keyframe(time, values[i], incoming, outgoing));
        }
        keys.AddRange(original.keys.Where(k => k.time * rate > 7f + 0.001f));
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
    }

    [MenuItem("Tools/Unagi/Validate Attack 02 Import")]
    public static void Validate()
    {
        var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            AssetDatabase.ImportAsset(FixedPath, ImportAssetOptions.ForceUpdate);
            var source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath);
            if (repaired == null || repaired.name != "Unagi_Attack_02" ||
                Mathf.Abs(repaired.frameRate - 30f) > 0.001f ||
                Mathf.Abs(repaired.length - 0.6f) > 0.001f)
                throw new InvalidOperationException("Imported clip name or timing is wrong.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                var hand = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bip001 R Hand");
                var hilt = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bn_katana_hilt");
                var sourceBindings = AnimationUtility.GetCurveBindings(source);
                var repairedBindings = AnimationUtility.GetCurveBindings(repaired);
                if (sourceBindings.Length != repairedBindings.Length)
                    throw new InvalidOperationException("Animation binding count changed.");
                foreach (var binding in repairedBindings)
                    if (instance.transform.Find(binding.path) == null)
                        throw new InvalidOperationException("Missing model path: " + binding.path);
                foreach (var binding in sourceBindings)
                {
                    if (binding.path == HiltPath &&
                        (binding.propertyName.StartsWith("m_LocalPosition") ||
                         binding.propertyName.StartsWith("m_LocalRotation"))) continue;
                    var oldKeys = AnimationUtility.GetEditorCurve(source, binding).keys;
                    var newKeys = AnimationUtility.GetEditorCurve(repaired, binding).keys;
                    if (oldKeys.Length != newKeys.Length)
                        throw new InvalidOperationException("Untouched curve key count changed: " + binding.path);
                    for (int i = 0; i < oldKeys.Length; i++)
                        if (Mathf.Abs(oldKeys[i].time - newKeys[i].time) > 0.000001f ||
                            Mathf.Abs(oldKeys[i].value - newKeys[i].value) > 0.000001f ||
                            Mathf.Abs(oldKeys[i].inTangent - newKeys[i].inTangent) > 0.0001f ||
                            Mathf.Abs(oldKeys[i].outTangent - newKeys[i].outTangent) > 0.0001f)
                            throw new InvalidOperationException("Untouched curve changed: " + binding.path);
                }
                for (int frame = 0; frame <= 18; frame++)
                {
                    source.SampleAnimation(instance, frame / 30f);
                    Vector3 oldPosition = hilt.localPosition;
                    Quaternion oldRotation = hilt.localRotation;
                    repaired.SampleAnimation(instance, frame / 30f);
                    if (Vector3.Distance(oldPosition, hilt.localPosition) > 0.0001f ||
                        Quaternion.Angle(oldRotation, hilt.localRotation) > 0.05f)
                        throw new InvalidOperationException("Integer frame changed: " + frame);
                }
                float maxGrip = 0f;
                for (int step = 0; step <= 32; step++)
                {
                    float frame = 5f + step / 16f;
                    repaired.SampleAnimation(instance, frame / 30f);
                    float grip = Vector3.Distance(hand.position, hilt.position);
                    maxGrip = Mathf.Max(maxGrip, grip);
                }
                if (maxGrip > 0.08f)
                    throw new InvalidOperationException("Reserialized clip still separates weapon from hand.");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                var state = controller.layers[0].stateMachine.states.Select(s => s.state)
                    .Single(s => s.name == "Unagi_Attack_02");
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityPath);
                if (state.motion != repaired || ability.Clip != repaired)
                    throw new InvalidOperationException("Animator and Ability do not share the independent clip.");
                var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
                if (importer.globalScale != 100f || importer.motionNodeName != "Root" ||
                    importer.animationType != ModelImporterAnimationType.Generic ||
                    instance.transform.localScale != Vector3.one)
                    throw new InvalidOperationException("Reference model scale or rig changed.");
                report.AppendLine($"PASS: reimported {repaired.name} at 30 FPS, frames 0-18, {repairedBindings.Length} curve bindings.");
                report.AppendLine($"PASS: all bone paths resolve on Unagi_Idle, all untouched curves and integer poses match source.");
                report.AppendLine($"PASS: maximum grip distance over 5-7 at 1/16-frame intervals is {maxGrip:F6} m.");
                report.AppendLine("PASS: Animator and Ability reference the same .anim GUID; model scale, rig and root unchanged.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllText(ValidateReport, report.ToString());
        }
    }
}
