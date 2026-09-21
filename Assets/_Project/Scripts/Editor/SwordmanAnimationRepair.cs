using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SwordmanAnimationRepair
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string Idle = Root + "02_Idle/Unagi_Idle.fbx";
    const string Controller = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    const string Request = "Library/SwordmanAnimationRepair.request";
    const string Report = "Library/SwordmanAnimationRepairReport.txt";

    static SwordmanAnimationRepair() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Repair();
    }

    [MenuItem("Tools/Unagi/Repair Swordman Main Animations")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before repairing imports.");
        var lines = new List<string>();
        int changed = 0, passed = 0;
        try
        {
            string[] paths = Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            foreach (string path in paths)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                var clipsBefore = importer.clipAnimations;
                string clipsJson = JsonUtility.ToJson(new ClipSettings { clips = clipsBefore });
                var settings = new SerializedObject(importer);
                var rootBone = settings.FindProperty("m_HumanDescription.m_RootMotionBoneName");
                bool needsRepair = importer.globalScale != 100f || importer.motionNodeName != "Root" || rootBone.stringValue != "Bip001";
                if (needsRepair)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var names = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
                    if (!names.Contains("Root") || !names.Contains("Bip001")) throw new InvalidOperationException("Unexpected skeleton: " + path);
                    importer.globalScale = 100f;
                    importer.motionNodeName = "Root";
                    settings.Update();
                    settings.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue = "Bip001";
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                    changed++;
                }
                // Preserve each action's authored range, loop, vertical motion and events.
                if (AssetDatabase.AssetPathToGUID(path) != guid ||
                    clipsJson != JsonUtility.ToJson(new ClipSettings { clips = importer.clipAnimations }))
                    throw new InvalidOperationException("Clip settings or GUID changed: " + path);
                lines.Add((needsRepair ? "REPAIRED " : "UNCHANGED ") + path);
                File.WriteAllLines(Report, lines);
            }

            // Sample every action on the same correctly scaled model, rather than
            // sampling each action on its own matching (possibly wrong) skeleton.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Idle);
                var idleClip = LoadClip(Idle);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
                if (controller == null) throw new InvalidOperationException("Missing Swordman controller.");
                var clips = paths.Select(LoadClip).Concat(controller.animationClips).Distinct().ToArray();
                lines.Add($"Checking {controller.animationClips.Distinct().Count()} actual controller clips, including standalone clips and blend trees.");
                foreach (var clip in clips)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    var instance = UnityEngine.Object.Instantiate(source);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
                    try
                    {
                        var animator = instance.GetComponent<Animator>();
                        if (animator != null) animator.enabled = false;
                        idleClip.SampleAnimation(instance, 0f);
                        var bones = instance.GetComponentsInChildren<Transform>(true)
                            .Where(t => t.name.StartsWith("Bip001") && t.name != "Bip001" && t.localPosition.magnitude > 0.01f).ToArray();
                        var lengths = bones.Select(t => Vector3.Distance(t.position, t.parent.position)).ToArray();
                        float minRatio = float.MaxValue, maxRatio = 0f;
                        int frames = Mathf.Max(1, Mathf.CeilToInt(clip.length * clip.frameRate));
                        for (int frame = 0; frame <= frames; frame++)
                        {
                            clip.SampleAnimation(instance, clip.length * frame / frames);
                            var ratios = bones.Select((bone, i) => Vector3.Distance(bone.position, bone.parent.position) / lengths[i]).OrderBy(r => r).ToArray();
                            if (ratios.Length < 10 || ratios.Any(r => float.IsNaN(r) || float.IsInfinity(r)))
                                throw new InvalidOperationException("Invalid bone sample: " + path);
                            float median = ratios[ratios.Length / 2];
                            minRatio = Mathf.Min(minRatio, median); maxRatio = Mathf.Max(maxRatio, median);
                            if (median < 0.8f || median > 1.2f) throw new InvalidOperationException("Skeleton scale mismatch " + median + ": " + path);
                        }
                        passed++;
                        lines.Add($"PASS sampled {clip.name}: {frames + 1} frames, median world bone ratio {minRatio:F4}..{maxRatio:F4} ({path})");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            lines.Add($"PASS: {paths.Length} imports checked; {changed} repaired; {passed} distinct clips sampled every frame, including actual controller references. Clip settings and GUIDs preserved.");
        }
        catch (Exception e) { lines.Add("FAIL: " + e); Debug.LogException(e); }
        finally { File.WriteAllLines(Report, lines); }
    }

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
    [Serializable] sealed class ClipSettings { public ModelImporterClipAnimation[] clips; }
}
