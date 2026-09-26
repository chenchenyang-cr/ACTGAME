using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiAnimationPreviewModel
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi";
    const string ModelPath = Root + "/02_Idle/Unagi_Idle.fbx";
    const string ReportPath = "Library/UnagiBatchImport/unagi_preview_model_validation.txt";
    const string RequestPath = "Library/UnagiBatchImport/unagi_preview_model_validation.request";

    static readonly Type PreviewSelectionType = typeof(Editor).Assembly.GetType("UnityEditor.AvatarPreviewSelection");
    static readonly MethodInfo GetPreviewMethod = PreviewSelectionType?.GetMethod(
        "GetPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    static readonly MethodInfo SetPreviewMethod = PreviewSelectionType?.GetMethod(
        "SetPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    static UnagiAnimationPreviewModel()
    {
        Selection.selectionChanged += BindForSelection;
        EditorApplication.delayCall += BindForSelection;
        EditorApplication.delayCall += RunRequestedValidation;
    }

    static void RunRequestedValidation()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedValidation;
            return;
        }
        File.Delete(RequestPath);
        BindAndValidate();
    }

    static void BindForSelection()
    {
        var clip = Selection.activeObject as AnimationClip;
        if (clip == null) return;
        var path = AssetDatabase.GetAssetPath(clip);
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal) ||
            !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            if (BindModel())
                EditorApplication.delayCall += () => ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
        catch (Exception exception)
        {
            Debug.LogError("Unagi animation preview model setup failed: " + exception);
        }
    }

    // Unity keeps the chosen preview model per rig type, not in each .anim asset.
    static bool BindModel()
    {
        if (GetPreviewMethod == null || SetPreviewMethod == null)
            throw new MissingMethodException("UnityEditor.AvatarPreviewSelection.GetPreview/SetPreview");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null || !model.activeSelf ||
            model.GetComponent<Animator>()?.avatar == null ||
            model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
            throw new InvalidOperationException("Unagi preview model, Avatar or mesh is missing.");

        var rigType = ModelImporterAnimationType.Generic;
        var current = GetPreviewMethod.Invoke(null, new object[] { rigType }) as GameObject;
        if (current == model) return false;
        SetPreviewMethod.Invoke(null, new object[] { rigType, model });
        if ((GetPreviewMethod.Invoke(null, new object[] { rigType }) as GameObject) != model)
            throw new InvalidOperationException("Unity did not retain the Unagi preview model.");
        return true;
    }

    [MenuItem("Tools/Unagi/Bind Preview Model and Validate Clips")]
    public static void BindAndValidate()
    {
        try
        {
            var paths = Directory.GetFiles(Root, "*.anim", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/')).ToArray();
            if (paths.Length != 70)
                throw new InvalidOperationException("Expected 70 Unagi clips, found " + paths.Length + ".");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var modelImporter = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (model == null || modelImporter == null ||
                modelImporter.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Unagi reference model is not a Generic rig.");

            var modelPaths = new HashSet<string>(model.GetComponentsInChildren<Transform>(true)
                .Select(bone => AnimationUtility.CalculateTransformPath(bone, model.transform)));
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || clip.legacy || clip.humanMotion)
                    throw new InvalidOperationException("Clip is missing or has a different rig type: " + path);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (!modelPaths.Contains(binding.path))
                        throw new InvalidOperationException("Model bone is missing: " + path + " / " + binding.path);
            }

            BindModel();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath,
                "PASS: 70 Unagi .anim clips use the Generic rig and all animated bone paths exist on Unagi_Idle.fbx.\n" +
                "PASS: the Unity Generic animation preview model is Unagi_Idle.fbx.\n");
            Debug.Log("Unagi: preview model bound; all 70 clips validated. Report: " + ReportPath);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }
}
