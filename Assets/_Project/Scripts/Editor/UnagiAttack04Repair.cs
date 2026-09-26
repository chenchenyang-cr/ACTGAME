using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiAttack04Repair
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string ModelPath = Root + "02_Idle/Unagi_Idle.fbx";
    const string SourcePath = Root + "01_Attack/Unagi_Attack_04.fbx";
    const string FixedPath = Root + "01_Attack/Unagi_Attack_04.anim";
    const string HiltPath = "Bip001/Bn_katana_hilt";
    const string Request = "Library/UnagiBatchImport/attack04_diagnose.request";
    const string Report = "Library/UnagiBatchImport/attack04_diagnose.csv";
    const string RepairRequest = "Library/UnagiBatchImport/attack04_repair.request";
    const string RepairReport = "Library/UnagiBatchImport/attack04_repair_report.txt";
    const string ValidateRequest = "Library/UnagiBatchImport/attack04_validate.request";
    const string ValidateReport = "Library/UnagiBatchImport/attack04_validation_report.txt";

    static UnagiAttack04Repair() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) { File.Delete(Request); Diagnose(); }
        if (File.Exists(RepairRequest)) { File.Delete(RepairRequest); Repair(); }
        if (File.Exists(ValidateRequest)) { File.Delete(ValidateRequest); Validate(); }
    }

    [MenuItem("Tools/Unagi/Diagnose Attack 04")]
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
                output.AppendLine($"clip,{clip.name},fps,{clip.frameRate},length,{clip.length},lastFrame,{clip.length * clip.frameRate}");
                output.AppendLine($"modelGlobalScale,{((ModelImporter)AssetImporter.GetAtPath(ModelPath)).globalScale},rootScale,{instance.transform.localScale}");
                output.AppendLine("frame,gripDistance,gripX,gripY,gripZ,gripRotation,hiltLocalX,hiltLocalY,hiltLocalZ");
                for (int step = 0; step <= 76; step++)
                {
                    float frame = step * 0.25f;
                    clip.SampleAnimation(instance, frame / clip.frameRate);
                    Vector3 grip = hand.InverseTransformPoint(hilt.position);
                    output.AppendLine($"{frame:F2},{grip.magnitude:F6},{grip.x:F6},{grip.y:F6},{grip.z:F6}," +
                        $"{Quaternion.Angle(hand.rotation, hilt.rotation):F3}," +
                        $"{hilt.localPosition.x:F6},{hilt.localPosition.y:F6},{hilt.localPosition.z:F6}");
                }
                output.AppendLine("hiltCurve,path,property,keys");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path == "Bip001/Bn_katana_hilt"))
                    output.AppendLine($"curve,{binding.path},{binding.propertyName},{AnimationUtility.GetEditorCurve(clip, binding).length}");
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

    static AnimationClip SourceClip() => AssetDatabase.LoadAllAssetsAtPath(SourcePath)
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));

    static GameObject PreviewModel(UnityEngine.SceneManagement.Scene scene)
    {
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
        var animator = instance.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        return instance;
    }

    [MenuItem("Tools/Unagi/Repair Attack 04 Grip")]
    public static void Repair()
    {
        var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        AnimationClip repaired = null;
        try
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath) != null)
                throw new InvalidOperationException("Standalone Attack 04 already exists; refusing to overwrite it.");
            var source = SourceClip();
            if (Mathf.Abs(source.frameRate - 30f) > 0.001f ||
                Mathf.Abs(source.length - 19f / 30f) > 0.001f ||
                AssetDatabase.AssetPathToGUID(SourcePath) != "707bb8d66b1d5d84dbefac4d187a9c2b")
                throw new InvalidOperationException("Source timing or GUID differs from backup.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer.globalScale != 100f || importer.motionNodeName != "Root" ||
                importer.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Reference model import settings changed.");
            var instance = PreviewModel(preview);
            try
            {
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var hand = transforms.Single(t => t.name == "Bip001 R Hand");
                var hilt = transforms.Single(t => t.name == "Bn_katana_hilt");
                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0 || renderers.Any(r => r.sharedMesh == null || r.sharedMaterials.Any(m => m == null)))
                    throw new InvalidOperationException("Reference model has a missing mesh or material.");
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                    if (instance.transform.Find(binding.path) == null)
                        throw new InvalidOperationException("Missing model path: " + binding.path);
                source.SampleAnimation(instance, 0f);
                report.AppendLine($"model height {renderers.Max(r => r.bounds.max.y) - renderers.Min(r => r.bounds.min.y):F6} m; feet Y {renderers.Min(r => r.bounds.min.y):F6} m; root scale {instance.transform.localScale}");

                Vector3[] grip = new Vector3[6];
                Quaternion[] gripRotation = new Quaternion[6];
                for (int frame = 4; frame <= 9; frame++)
                {
                    source.SampleAnimation(instance, frame / source.frameRate);
                    grip[frame - 4] = hand.InverseTransformPoint(hilt.position);
                    gripRotation[frame - 4] = Quaternion.Inverse(hand.rotation) * hilt.rotation;
                    report.AppendLine($"source frame {frame}: grip {grip[frame - 4].magnitude:F6} m");
                }
                if (grip[1].magnitude < 0.08f || grip[0].magnitude > 0.08f || grip[2].magnitude > 0.08f)
                    throw new InvalidOperationException("Expected isolated frame 5 grip anomaly was not found.");

                const int samplesPerFrame = 4;
                var positions = new Vector3[5 * samplesPerFrame + 1];
                var rotations = new Quaternion[positions.Length];
                for (int step = 0; step < positions.Length; step++)
                {
                    float frame = 4f + step / (float)samplesPerFrame;
                    source.SampleAnimation(instance, frame / source.frameRate);
                    // Frame 5 receives the stable frame 6 hand-relative grip.
                    int left = frame < 5f ? 0 : frame < 6f ? 2 : Mathf.Min(4, Mathf.FloorToInt(frame) - 4);
                    int right = frame < 5f ? 2 : frame < 6f ? 2 : Mathf.Min(5, left + 1);
                    float fraction = frame < 5f ? frame - 4f : frame < 6f ? 0f : frame - (left + 4f);
                    Vector3 desiredGrip = Vector3.Lerp(grip[left], grip[right], fraction);
                    Quaternion desiredRotation = Quaternion.Slerp(gripRotation[left], gripRotation[right], fraction);
                    positions[step] = hilt.parent.InverseTransformPoint(hand.TransformPoint(desiredGrip));
                    rotations[step] = Quaternion.Inverse(hilt.parent.rotation) * hand.rotation * desiredRotation;
                    if (step > 0 && Quaternion.Dot(rotations[step - 1], rotations[step]) < 0f)
                        rotations[step] = new Quaternion(-rotations[step].x, -rotations[step].y,
                            -rotations[step].z, -rotations[step].w);
                }

                repaired = UnityEngine.Object.Instantiate(source);
                repaired.name = "Unagi_Attack_04";
                AssetDatabase.CreateAsset(repaired, FixedPath);
                SetDenseCurve(repaired, "m_LocalPosition.x", positions.Select(v => v.x).ToArray());
                SetDenseCurve(repaired, "m_LocalPosition.y", positions.Select(v => v.y).ToArray());
                SetDenseCurve(repaired, "m_LocalPosition.z", positions.Select(v => v.z).ToArray());
                SetDenseCurve(repaired, "m_LocalRotation.x", rotations.Select(q => q.x).ToArray());
                SetDenseCurve(repaired, "m_LocalRotation.y", rotations.Select(q => q.y).ToArray());
                SetDenseCurve(repaired, "m_LocalRotation.z", rotations.Select(q => q.z).ToArray());
                SetDenseCurve(repaired, "m_LocalRotation.w", rotations.Select(q => q.w).ToArray());
                EditorUtility.SetDirty(repaired);
                AssetDatabase.SaveAssets();
                report.AppendLine("PASS: saved standalone .anim; source FBX and references unchanged.");
                Check(source, repaired, instance, hand, hilt, report);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e)
        {
            report.AppendLine("FAIL: " + e);
            Debug.LogException(e);
            if (repaired != null) AssetDatabase.DeleteAsset(FixedPath);
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
        var start = original.keys.Single(k => Mathf.Abs(k.time * rate - 4f) < 0.001f);
        var end = original.keys.Single(k => Mathf.Abs(k.time * rate - 9f) < 0.001f);
        var keys = original.keys.Where(k => k.time * rate < 4f - 0.001f).ToList();
        for (int i = 0; i < values.Length; i++)
        {
            float time = (4f + i * 0.25f) / rate;
            float incoming = i == 0 ? start.inTangent : (values[i] - values[i - 1]) * 4f * rate;
            float outgoing = i == values.Length - 1 ? end.outTangent :
                (values[i + 1] - values[i]) * 4f * rate;
            keys.Add(new Keyframe(time, values[i], incoming, outgoing));
        }
        keys.AddRange(original.keys.Where(k => k.time * rate > 9f + 0.001f));
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
    }

    static void Check(AnimationClip source, AnimationClip repaired, GameObject instance,
        Transform hand, Transform hilt, StringBuilder report)
    {
        if (repaired == null || repaired.name != "Unagi_Attack_04" ||
            Mathf.Abs(repaired.frameRate - source.frameRate) > 0.001f ||
            Mathf.Abs(repaired.length - source.length) > 0.001f)
            throw new InvalidOperationException("Clip name or timing changed.");
        var sourceBindings = AnimationUtility.GetCurveBindings(source);
        var repairedBindings = AnimationUtility.GetCurveBindings(repaired);
        if (sourceBindings.Length != repairedBindings.Length)
            throw new InvalidOperationException("Binding count changed.");
        foreach (var binding in sourceBindings)
        {
            if (instance.transform.Find(binding.path) == null)
                throw new InvalidOperationException("Missing model path: " + binding.path);
            if (binding.path == HiltPath &&
                (binding.propertyName.StartsWith("m_LocalPosition") ||
                 binding.propertyName.StartsWith("m_LocalRotation"))) continue;
            var before = AnimationUtility.GetEditorCurve(source, binding).keys;
            var after = AnimationUtility.GetEditorCurve(repaired, binding).keys;
            if (before.Length != after.Length)
                throw new InvalidOperationException("Untouched curve key count changed: " + binding.path);
            for (int i = 0; i < before.Length; i++)
                if (Mathf.Abs(before[i].time - after[i].time) > 0.000001f ||
                    Mathf.Abs(before[i].value - after[i].value) > 0.000001f ||
                    Mathf.Abs(before[i].inTangent - after[i].inTangent) > 0.0001f ||
                    Mathf.Abs(before[i].outTangent - after[i].outTangent) > 0.0001f)
                    throw new InvalidOperationException("Untouched curve changed: " + binding.path + "/" + binding.propertyName);
        }
        for (int frame = 0; frame <= 19; frame++)
        {
            if (frame == 5) continue;
            source.SampleAnimation(instance, frame / source.frameRate);
            Vector3 originalPosition = hilt.localPosition;
            Quaternion originalRotation = hilt.localRotation;
            repaired.SampleAnimation(instance, frame / source.frameRate);
            if (Vector3.Distance(originalPosition, hilt.localPosition) > 0.0001f ||
                Quaternion.Angle(originalRotation, hilt.localRotation) > 0.05f)
                throw new InvalidOperationException("Unchanged integer frame changed: " + frame);
        }
        float maximumGrip = 0f;
        for (int step = 0; step <= 80; step++)
        {
            float frame = 4f + step / 16f;
            repaired.SampleAnimation(instance, frame / source.frameRate);
            float grip = Vector3.Distance(hand.position, hilt.position);
            maximumGrip = Mathf.Max(maximumGrip, grip);
            report.AppendLine($"frame {frame:F4}: grip {grip:F6} m");
        }
        if (maximumGrip > 0.08f)
            throw new InvalidOperationException("Grip still separates over frames 4-9: " + maximumGrip);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer.globalScale != 100f || importer.motionNodeName != "Root" ||
            importer.animationType != ModelImporterAnimationType.Generic ||
            instance.transform.localScale != Vector3.one)
            throw new InvalidOperationException("Reference model scale or rig changed.");
        report.AppendLine($"PASS: 30 FPS, frames 0-19, {repairedBindings.Length} paths, all untouched curves and integer poses preserved.");
        report.AppendLine($"PASS: max grip distance on frames 4-9 at 1/16 frame intervals: {maximumGrip:F6} m.");
        report.AppendLine("PASS: model scale, rig and motion root unchanged.");
    }

    [MenuItem("Tools/Unagi/Validate Attack 04 Import")]
    public static void Validate()
    {
        var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            AssetDatabase.ImportAsset(FixedPath, ImportAssetOptions.ForceUpdate);
            var source = SourceClip();
            var repaired = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath);
            var instance = PreviewModel(preview);
            try
            {
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var hand = transforms.Single(t => t.name == "Bip001 R Hand");
                var hilt = transforms.Single(t => t.name == "Bn_katana_hilt");
                Check(source, repaired, instance, hand, hilt, report);
                report.AppendLine("PASS: independent .anim reimported and validated; no Animator or Ability connection needed for preview.");
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
