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
public static class UnagiAllClipMigration
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi";
    const string ModelPath = Root + "/02_Idle/Unagi_Idle.fbx";
    const string Request = "Library/UnagiBatchImport/all_unagi_migration.request";
    const string Report = "Library/UnagiBatchImport/all_unagi_migration_report.csv";
    const string CleanupRequest = "Library/UnagiBatchImport/all_unagi_cleanup_validate.request";
    const string CleanupReport = "Library/UnagiBatchImport/all_unagi_cleanup_validation.txt";

    static UnagiAllClipMigration() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) { File.Delete(Request); Migrate(); }
        if (File.Exists(CleanupRequest)) { File.Delete(CleanupRequest); ValidateCleanup(); }
    }

    [MenuItem("Tools/Unagi/Migrate All Missing Animation Clips")]
    public static void Migrate()
    {
        var report = new StringBuilder();
        report.AppendLine("source,output,status,fps,lastFrame,bindings,events,detail");
        try
        {
            var paths = Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length != 70)
                throw new InvalidOperationException("Expected 70 Unagi FBXs, found " + paths.Length);
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (modelImporter == null || modelImporter.globalScale != 100f ||
                modelImporter.motionNodeName != "Root" ||
                modelImporter.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("Reference model import settings changed.");
            int migrated = 0, existing = 0, failed = 0;
            foreach (var sourcePath in paths)
            {
                var fixedPath = sourcePath.Substring(0, sourcePath.Length - 4) + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath) != null)
                {
                    existing++;
                    continue;
                }
                EditorUtility.DisplayProgressBar("Unagi animation migration",
                    Path.GetFileName(sourcePath), (migrated + failed) / 49f);
                var preview = EditorSceneManager.NewPreviewScene();
                AnimationClip created = null;
                try
                {
                    var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                        .Single(c => !c.name.StartsWith("__preview__"));
                    if (source.frameRate <= 0f || source.length <= 0f)
                        throw new InvalidOperationException("Invalid source timing.");
                    created = UnityEngine.Object.Instantiate(source);
                    created.name = Path.GetFileNameWithoutExtension(sourcePath);
                    AssetDatabase.CreateAsset(created, fixedPath);
                    EditorUtility.SetDirty(created);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(fixedPath, ImportAssetOptions.ForceUpdate);
                    var independent = AssetDatabase.LoadAssetAtPath<AnimationClip>(fixedPath);
                    int bindings = Verify(source, independent, preview);
                    int lastFrame = Mathf.RoundToInt(source.length * source.frameRate);
                    int events = AnimationUtility.GetAnimationEvents(source).Length;
                    report.AppendLine($"{sourcePath},{fixedPath},PASS,{source.frameRate},{lastFrame},{bindings},{events},same curves and half-frame poses");
                    migrated++;
                }
                catch (Exception e)
                {
                    failed++;
                    report.AppendLine($"{sourcePath},{fixedPath},FAIL,,,,,{e.GetType().Name}: {e.Message.Replace(',', ';')}");
                    Debug.LogException(e);
                    if (created != null) AssetDatabase.DeleteAsset(fixedPath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                    File.WriteAllText(Report, report.ToString());
                }
            }
            report.AppendLine($"SUMMARY,70 FBXs,{migrated} migrated,{existing} existing,{failed} failed,,,");
        }
        catch (Exception e)
        {
            report.AppendLine("SUMMARY,,,FAIL,,,," + e);
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            File.WriteAllText(Report, report.ToString());
        }
    }

    static int Verify(AnimationClip source, AnimationClip independent,
        UnityEngine.SceneManagement.Scene preview)
    {
        if (independent == null ||
            Mathf.Abs(source.frameRate - independent.frameRate) > 0.001f ||
            Mathf.Abs(source.length - independent.length) > 0.001f ||
            source.wrapMode != independent.wrapMode)
            throw new InvalidOperationException("Imported timing or wrap mode changed.");
        var oldBindings = AnimationUtility.GetCurveBindings(source);
        var newBindings = AnimationUtility.GetCurveBindings(independent);
        if (oldBindings.Length != newBindings.Length)
            throw new InvalidOperationException("Curve binding count changed.");
        var newSet = new HashSet<EditorCurveBinding>(newBindings);
        foreach (var binding in oldBindings)
        {
            if (!newSet.Contains(binding))
                throw new InvalidOperationException("Curve binding missing: " + binding.path + "/" + binding.propertyName);
            var oldKeys = AnimationUtility.GetEditorCurve(source, binding).keys;
            var newKeys = AnimationUtility.GetEditorCurve(independent, binding).keys;
            if (oldKeys.Length != newKeys.Length)
                throw new InvalidOperationException("Curve key count changed: " + binding.path);
            for (int key = 0; key < oldKeys.Length; key++)
                if (Mathf.Abs(oldKeys[key].time - newKeys[key].time) > 0.000001f ||
                    Mathf.Abs(oldKeys[key].value - newKeys[key].value) > 0.000001f ||
                    Mathf.Abs(oldKeys[key].inTangent - newKeys[key].inTangent) > 0.0001f ||
                    Mathf.Abs(oldKeys[key].outTangent - newKeys[key].outTangent) > 0.0001f)
                    throw new InvalidOperationException("Curve changed: " + binding.path + "/" + binding.propertyName);
        }
        var oldEvents = AnimationUtility.GetAnimationEvents(source);
        var newEvents = AnimationUtility.GetAnimationEvents(independent);
        if (oldEvents.Length != newEvents.Length)
            throw new InvalidOperationException("Animation event count changed.");
        for (int i = 0; i < oldEvents.Length; i++)
            if (Mathf.Abs(oldEvents[i].time - newEvents[i].time) > 0.000001f ||
                oldEvents[i].functionName != newEvents[i].functionName ||
                oldEvents[i].stringParameter != newEvents[i].stringParameter ||
                oldEvents[i].intParameter != newEvents[i].intParameter ||
                Mathf.Abs(oldEvents[i].floatParameter - newEvents[i].floatParameter) > 0.000001f)
                throw new InvalidOperationException("Animation event changed.");
        var oldReferences = AnimationUtility.GetObjectReferenceCurveBindings(source);
        var newReferences = AnimationUtility.GetObjectReferenceCurveBindings(independent);
        if (oldReferences.Length != newReferences.Length)
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
            foreach (var binding in oldBindings)
                if (instance.transform.Find(binding.path) == null)
                    throw new InvalidOperationException("Model path missing: " + binding.path);
            var positions = new Vector3[transforms.Length];
            var rotations = new Quaternion[transforms.Length];
            var scales = new Vector3[transforms.Length];
            int steps = Mathf.RoundToInt(source.length * source.frameRate * 2f);
            for (int step = 0; step <= steps; step++)
            {
                float time = step / (source.frameRate * 2f);
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
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        return oldBindings.Length;
    }

    static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states) yield return child.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in States(child.stateMachine)) yield return state;
    }

    [MenuItem("Tools/Unagi/Validate Animation Cleanup")]
    public static void ValidateCleanup()
    {
        var report = new StringBuilder();
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var fbx = Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories);
            var anim = Directory.GetFiles(Root, "*.anim", SearchOption.AllDirectories);
            if (fbx.Length != 1 || fbx[0].Replace('\\', '/') != ModelPath || anim.Length != 70)
                throw new InvalidOperationException($"Expected one model FBX and 70 .anim clips, found {fbx.Length} and {anim.Length}.");
            foreach (var path in anim)
            {
                string assetPath = path.Replace('\\', '/');
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) == null)
                    throw new InvalidOperationException("Animation missing after refresh: " + assetPath);
            }
            int oldGuids = 0;
            var stalePaths = new List<string>();
            const string Backup = "Library/UnagiBatchImport/RemovedFbxBackup";
            foreach (var meta in Directory.GetFiles(Backup, "*.fbx.meta", SearchOption.AllDirectories))
            {
                string guidLine = File.ReadLines(meta).First(line => line.StartsWith("guid: "));
                string stalePath = AssetDatabase.GUIDToAssetPath(guidLine.Substring(6).Trim());
                if (!string.IsNullOrEmpty(stalePath))
                {
                    oldGuids++;
                    if (stalePaths.Count < 5) stalePaths.Add(stalePath + " exists=" + File.Exists(stalePath));
                    if (File.Exists(stalePath) || AssetDatabase.LoadMainAssetAtPath(stalePath) != null)
                        throw new InvalidOperationException("Deleted FBX still loads: " + stalePath);
                }
            }
            var importedFbx = AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (importedFbx.Length != 1 || importedFbx[0] != ModelPath)
                throw new InvalidOperationException($"Unity still indexes {importedFbx.Length} FBXs: " +
                    string.Join("; ", importedFbx.Take(5)));
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelImporter.globalScale != 100f || modelImporter.motionNodeName != "Root" ||
                modelImporter.animationType != ModelImporterAnimationType.Generic || model == null)
                throw new InvalidOperationException("Reference model import settings changed.");
            var animator = model.GetComponent<Animator>();
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || animator.avatar == null || renderers.Length == 0 ||
                renderers.Any(r => r.sharedMesh == null || r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("Model Avatar, mesh or material is missing.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/_Project/Animations/Controllers/Player_Swordman.controller");
            int checkedStates = 0;
            foreach (var state in controller.layers.SelectMany(layer => States(layer.stateMachine)))
            {
                if (state.motion is AnimationClip clip && clip.name.StartsWith("Unagi_"))
                {
                    if (!AssetDatabase.GetAssetPath(clip).EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Animator state still uses FBX: " + state.name);
                    checkedStates++;
                }
            }
            int checkedAbilities = 0;
            const string AbilityRoot = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities";
            foreach (var path in Directory.GetFiles(AbilityRoot, "*.asset", SearchOption.AllDirectories))
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path.Replace('\\', '/'));
                if (ability != null && ability.Clip != null && ability.Clip.name.StartsWith("Unagi_"))
                {
                    if (!AssetDatabase.GetAssetPath(ability.Clip).EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Ability still uses FBX: " + path);
                    checkedAbilities++;
                }
            }
            report.AppendLine($"PASS: {anim.Length} .anim clips, only {Path.GetFileName(fbx[0])} remains as model FBX.");
            report.AppendLine($"PASS: Unity imports only the model FBX; {oldGuids} old GUID path records cannot load deleted assets.");
            report.AppendLine($"PASS: {checkedStates} Unagi Animator states and {checkedAbilities} Abilities use .anim.");
            report.AppendLine("PASS: model scale 100, Generic rig, Root motion node, Avatar, mesh and materials intact.");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        finally { File.WriteAllText(CleanupReport, report.ToString()); }
    }
}
