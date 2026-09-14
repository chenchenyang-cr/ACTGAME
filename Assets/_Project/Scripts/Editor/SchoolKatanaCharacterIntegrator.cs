using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CombatEditor;
using UnityEditor.Animations;
using ComponentUtility = UnityEditorInternal.ComponentUtility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Requests are explicit, project-local files; importing this tool alone never edits a scene.
[InitializeOnLoad]
public static class SchoolKatanaCharacterIntegrator
{
    public const string PrefabPath = "Assets/ThirdParty/CombatGirlsCharacterPack/School_Katana_Girl/Prefab/School_Katana_FullBody-Magica cloth2.prefab";
    public const string ScenePath = "Assets/_Project/Scenes/Main_Nodachi.unity";
    const string Request = "Library/SchoolKatanaIntegration/request.txt";
    static SchoolKatanaCharacterIntegrator() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (File.Exists("Library/SchoolKatanaIntegration/error.txt")) File.Delete("Library/SchoolKatanaIntegration/error.txt");
        try { if (command == "audit") Audit(); else if (command == "integrate") Integrate(); else if (command == "verify") Verify(); }
        catch (Exception e) { File.WriteAllText("Library/SchoolKatanaIntegration/error.txt", e.ToString()); Debug.LogException(e); }
    }

    const string Output = "Assets/_Project/Art/Characters/SchoolKatana";
    const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Nodachi.controller";
    const string OldWeaponPath = "root/pelvis/spine_01/spine_02/spine_03/spine_04/spine_05/clavicle_r/upperarm_r/lowerarm_r/hand_r/Nodachi_Weapon_R";

    static Quaternion GripRotation(Animator oldAnimator, Animator newAnimator)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var rotations = new List<Quaternion>();
        try
        {
            foreach (var source in new[] { oldAnimator, newAnimator })
            {
                var go = UnityEngine.Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(go, preview);
                go.SetActive(true);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var a = go.GetComponent<Animator>();
                a.runtimeAnimatorController = null;
                a.Rebind();
                var pose = new HumanPose { bodyPosition = Vector3.up, bodyRotation = Quaternion.identity, muscles = new float[HumanTrait.MuscleCount] };
                using (var handler = new HumanPoseHandler(a.avatar, a.transform)) handler.SetHumanPose(ref pose);
                rotations.Add(a.GetBoneTransform(HumanBodyBones.RightHand).rotation);
            }
            return Quaternion.Inverse(rotations[1]) * rotations[0];
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [MenuItem("Tools/Player/Verify School Katana Replacement")]
    public static void Verify()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player_Nodachi");
        var animator = player.transform.Find("SchoolKatanaVisual").GetComponent<Animator>();
        CheckAvatar(animator);
        if (player.GetComponentsInChildren<Animator>().Length != 1) throw new Exception("Expected exactly one active Animator.");
        var report = new StringBuilder();
        var combat = player.GetComponent<CombatController>();
        var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
        var samplingScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var samplingObject = UnityEngine.Object.Instantiate(animator.gameObject);
            SceneManager.MoveGameObjectToScene(samplingObject, samplingScene);
            samplingObject.GetComponent<Animator>().runtimeAnimatorController = null;
            SampleAll(samplingObject.GetComponent<Animator>(), clips, report);
        }
        finally { EditorSceneManager.ClosePreviewScene(samplingScene); }
        foreach (var ability in combat.CombatDatas.SelectMany(g => g.CombatObjs))
            if (ability && !clips.Contains(ability.Clip)) throw new Exception("Ability clip identity differs from controller: " + ability.name);
        foreach (var node in combat.Nodes)
            if (!node.NodeTrans || (node.type != CharacterNode.NodeType.BottomCenter && !node.NodeTrans.IsChildOf(animator.transform))) throw new Exception("Invalid combat node: " + node.type);
        foreach (var component in player.GetComponentsInChildren<Component>())
        {
            if (!component) throw new Exception("Active missing component.");
            if (component is Transform) continue; // The player intentionally owns the disabled backup hierarchy.
            var p = new SerializedObject(component).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                var referenced = p.objectReferenceValue as Component;
                if (referenced && referenced.transform.IsChildOf(player.transform.Find("NodachiVisual_Disabled")))
                    throw new Exception("Active component still references old model: " + component.GetType().Name + "." + p.propertyPath);
            }
        }
        report.AppendLine("One active Humanoid Animator; all ability clip identities, combat nodes and active component references validated.");
        foreach (string name in new[] { "Idle_Combat", "Combo_Attack_01_01", "Run_Fast_Loop", "Original_Idle_Combat", "Original_Combo_Attack_01_01" })
        {
            bool original = name.StartsWith("Original_");
            var renderAnimator = original ? player.transform.Find("NodachiVisual_Disabled").GetComponent<Animator>() : animator;
            string clipName = original ? name.Substring("Original_".Length) : name;
            var renderClips = renderAnimator.runtimeAnimatorController.animationClips;
            var clip = renderClips.FirstOrDefault(c => c.name == clipName) ?? renderClips.First(c => c.name.Contains(clipName));
            var preview = new PreviewRenderUtility();
            PlayableGraph graph = default;
            try
            {
                var copy = UnityEngine.Object.Instantiate(renderAnimator.gameObject);
                copy.SetActive(true);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                preview.AddSingleGO(copy);
                var a = copy.GetComponent<Animator>();
                a.runtimeAnimatorController = null;
                a.applyRootMotion = false;
                graph = PlayableGraph.Create("SchoolKatanaVisualCheck");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", a).SetSourcePlayable(playable);
                graph.Play();
                playable.SetTime(clip.length * 0.65);
                graph.Evaluate(0.016f);
                graph.Evaluate(0.016f);
                Bounds bounds = new Bounds(a.GetBoneTransform(HumanBodyBones.Hips).position, Vector3.zero);
                foreach (var renderer in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (renderer.bones.Any(b => !b)) throw new Exception("Missing skinning bone: " + renderer.name);
                    var mesh = new Mesh();
                    renderer.BakeMesh(mesh);
                    if (mesh.vertexCount == 0 || float.IsNaN(mesh.bounds.size.sqrMagnitude) || mesh.bounds.size.magnitude > 6)
                        throw new Exception("Invalid baked mesh: " + renderer.name);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    bounds.Encapsulate(renderer.bounds);
                }
                preview.camera.clearFlags = CameraClearFlags.Color;
                preview.camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;
                preview.camera.fieldOfView = 35;
                preview.camera.transform.position = bounds.center + new Vector3(2.4f, 0.6f, 3.5f);
                preview.camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
                preview.lights[1].intensity = 0.8f;
                preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0, 0, 800, 800));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes("Library/SchoolKatanaIntegration/" + name + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                report.AppendLine("Skinned mesh and render checked: " + clip.name);
            }
            finally { if (graph.IsValid()) graph.Destroy(); preview.Cleanup(); }
        }
        File.WriteAllText("Library/SchoolKatanaIntegration/verification.txt", report.ToString());
    }

    static void CheckAvatar(Animator animator)
    {
        if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("A valid Humanoid Avatar is required.");
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            if (HumanTrait.RequiredBone(i) && !animator.GetBoneTransform((HumanBodyBones)i))
                throw new InvalidOperationException("Missing required bone: " + (HumanBodyBones)i);
    }

    static GameObject Prepare(Scene scene, Animator oldAnimator)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        // Keep the vendor prefab intact; missing optional cloth components are removed only from this instance.
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "SchoolKatanaVisual";
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            t.gameObject.layer = oldAnimator.gameObject.layer;
        }
        var animator = go.GetComponent<Animator>();
        CheckAvatar(animator);
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = oldAnimator.applyRootMotion;
        animator.updateMode = oldAnimator.updateMode;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // The existing Nodachi weapon and its hit markers retain their authored axes and dimensions.
        var weapon = oldAnimator.transform.Find(OldWeaponPath);
        if (!weapon) throw new InvalidOperationException("Original animated weapon was not found.");
        var alignment = new GameObject("NodachiGripAlignment").transform;
        alignment.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
        alignment.localRotation = GripRotation(oldAnimator, animator);
        var copiedWeapon = UnityEngine.Object.Instantiate(weapon.gameObject, alignment);
        copiedWeapon.name = weapon.name;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            if (!renderer.transform.IsChildOf(copiedWeapon.transform) && renderer.name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0)
                renderer.gameObject.SetActive(false);
        return go;
    }

    static AnimationClip Retarget(AnimationClip source, Animator target)
    {
        if (!source.humanMotion) throw new InvalidOperationException("Non-Humanoid clip: " + source.name);
        var result = UnityEngine.Object.Instantiate(source);
        result.name = source.name;
        string weaponPath = AnimationUtility.CalculateTransformPath(target.GetBoneTransform(HumanBodyBones.RightHand), target.transform) + "/NodachiGripAlignment/Nodachi_Weapon_R";
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type == typeof(Animator) && string.IsNullOrEmpty(binding.path)) continue;
            if (binding.path != OldWeaponPath) throw new InvalidOperationException("Unsupported direct animation binding: " + source.name + ": " + binding.path);
            var replacement = binding;
            replacement.path = weaponPath;
            AnimationUtility.SetEditorCurve(result, binding, null);
            AnimationUtility.SetEditorCurve(result, replacement, AnimationUtility.GetEditorCurve(source, binding));
        }
        if (AnimationUtility.GetObjectReferenceCurveBindings(source).Length != 0)
            throw new InvalidOperationException("Object animation bindings need explicit mapping: " + source.name);
        return result;
    }

    static void SampleAll(Animator animator, IEnumerable<AnimationClip> clips, StringBuilder report)
    {
        var transforms = animator.GetComponentsInChildren<Transform>(true);
        var positions = transforms.Select(t => t.localPosition).ToArray();
        var rotations = transforms.Select(t => t.localRotation).ToArray();
        var scales = transforms.Select(t => t.localScale).ToArray();
        var bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => animator.GetBoneTransform((HumanBodyBones)i)).Where(t => t).ToArray();
        int samples = 0;
        try
        {
            foreach (var clip in clips)
            {
                if (!clip.humanMotion) throw new Exception("Retargeted clip lost Humanoid data: " + clip.name);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.type != typeof(Animator)))
                    if (!animator.transform.Find(binding.path)) throw new Exception("Unresolved binding: " + binding.path);
                var graph = PlayableGraph.Create("SchoolKatanaCompatibility");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                    graph.Play();
                    for (int frame = 0; frame <= 8; frame++)
                    {
                        playable.SetTime(clip.length * frame / 8.0);
                        graph.Evaluate(0);
                        foreach (var bone in bones)
                        {
                            Vector3 p = animator.transform.InverseTransformPoint(bone.position);
                            if (float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude) || p.magnitude > 5f)
                                throw new Exception("Invalid sampled skeleton: " + clip.name + "/" + bone.name);
                        }
                        samples++;
                    }
                }
                finally { graph.Destroy(); }
            }
            report.AppendLine("Passed " + samples + " sampled Humanoid poses; all direct weapon bindings resolve.");
        }
        finally
        {
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                transforms[i].localScale = scales[i];
            }
        }
    }

    static void ReplaceReferences(UnityEngine.Object target, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        var serialized = new SerializedObject(target);
        var p = serialized.GetIterator();
        bool changed = false;
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || !p.objectReferenceValue || !map.TryGetValue(p.objectReferenceValue, out var replacement)) continue;
            p.objectReferenceValue = replacement;
            changed = true;
        }
        if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
    }

    static void CopyAssetWithSubassets(string source, string destination, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
    {
        if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Cannot copy " + source);
        var originals = AssetDatabase.LoadAllAssetsAtPath(source);
        var copies = AssetDatabase.LoadAllAssetsAtPath(destination);
        foreach (var original in originals)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string _, out long sourceId);
            var copy = copies.Single(c => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string _, out long id); return id == sourceId; });
            map[original] = copy;
        }
    }

    [MenuItem("Tools/Player/Replace Nodachi With Validated School Katana")]
    public static void Integrate()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var player = scene.GetRootGameObjects().Single(g => g.name == "Player_Nodachi");
        if (player.transform.Find("SchoolKatanaVisual")) throw new InvalidOperationException("School Katana is already installed.");
        var oldAnimator = player.transform.Find("NodachiVisual").GetComponent<Animator>();
        CheckAvatar(oldAnimator);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var abilityPaths = AssetDatabase.FindAssets("t:AbilityScriptableObject", new[] { "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Nodachi" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
        var abilities = abilityPaths.Select(AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>).ToArray();
        var sourceClips = controller.animationClips.Concat(abilities.Select(a => a.Clip)).Concat(abilities.SelectMany(a => a.AdditionalClips)).Where(c => c).Distinct().ToArray();
        var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        var report = new StringBuilder("School Katana integration\n");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var test = Prepare(preview, oldAnimator);
            foreach (var clip in sourceClips) map.Add(clip, Retarget(clip, test.GetComponent<Animator>()));
            SampleAll(test.GetComponent<Animator>(), map.Values.Cast<AnimationClip>(), report);
        }
        catch { foreach (var clip in map.Values) UnityEngine.Object.DestroyImmediate(clip); throw; }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        // No player or persistent animation asset is changed until the complete preflight passes.
        Directory.CreateDirectory(Output + "/Animations");
        Directory.CreateDirectory(Output + "/Abilities");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var source in sourceClips)
            AssetDatabase.CreateAsset(map[source], Output + "/Animations/" + source.name + ".anim");
        CopyAssetWithSubassets(ControllerPath, Output + "/Player_SchoolKatana.controller", map);
        foreach (string path in abilityPaths) CopyAssetWithSubassets(path, Output + "/Abilities/" + Path.GetFileName(path), map);
        foreach (var asset in map.Values.Where(a => !(a is AnimationClip)).ToArray()) ReplaceReferences(asset, map);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, "Library/SchoolKatanaIntegration/BeforeReplacement.unity", true);
        var visual = Prepare(scene, oldAnimator);
        visual.transform.SetParent(player.transform, false);
        visual.transform.SetLocalPositionAndRotation(oldAnimator.transform.localPosition, oldAnimator.transform.localRotation);
        visual.transform.localScale = oldAnimator.transform.localScale;
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = (RuntimeAnimatorController)map[controller];
        map[oldAnimator] = animator;
        map[oldAnimator.transform] = animator.transform;
        map[oldAnimator.gameObject] = animator.gameObject;
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var oldBone = oldAnimator.GetBoneTransform((HumanBodyBones)i);
            var newBone = animator.GetBoneTransform((HumanBodyBones)i);
            if (oldBone && newBone) { map[oldBone] = newBone; map[oldBone.gameObject] = newBone.gameObject; }
        }
        var oldWeapon = oldAnimator.transform.Find(OldWeaponPath);
        var newWeapon = animator.GetBoneTransform(HumanBodyBones.RightHand).Find("NodachiGripAlignment/Nodachi_Weapon_R");
        foreach (var t in oldWeapon.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, oldWeapon);
            var replacement = path.Length == 0 ? newWeapon : newWeapon.Find(path);
            if (replacement) { map[t] = replacement; map[t.gameObject] = replacement.gameObject; }
        }
        foreach (var component in oldAnimator.GetComponents<Component>().Where(c => !(c is Transform) && !(c is Animator)))
        {
            ComponentUtility.CopyComponent(component);
            if (!ComponentUtility.PasteComponentAsNew(visual)) throw new Exception("Cannot copy " + component.GetType().Name);
            map[component] = visual.GetComponents(component.GetType()).Last();
        }
        foreach (var component in player.GetComponentsInChildren<Component>(true))
            if (component && !component.transform.IsChildOf(oldAnimator.transform) && component.gameObject.name != "LegacyCharacter_Disabled") ReplaceReferences(component, map);
        oldAnimator.gameObject.name = "NodachiVisual_Disabled";
        oldAnimator.gameObject.SetActive(false);
        var combat = player.GetComponent<CombatController>();
        if (combat._animator != animator || combat.Nodes.Any(n => !n.NodeTrans || n.NodeTrans.IsChildOf(oldAnimator.transform)))
            throw new Exception("Combat references were not completely remapped.");
        if (!visual.GetComponent<RootMotionReceiver>()) throw new Exception("RootMotionReceiver is missing.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene could not be saved.");
        report.AppendLine("Saved " + ScenePath);
        report.AppendLine("Copied and retargeted clips: " + sourceClips.Length + "; copied ability assets: " + abilities.Length);
        report.AppendLine("Avatar: " + AssetDatabase.GetAssetPath(animator.avatar));
        report.AppendLine("Original Nodachi weapon, hit markers and root-motion components retained; all combat nodes rebound.");
        report.AppendLine("19 missing optional Magica Cloth components removed from scene instance. Cloth simulation unavailable.");
        File.WriteAllText("Library/SchoolKatanaIntegration/result.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Player/Audit School Katana Compatibility")]
    public static void Audit()
    {
        var report = new StringBuilder();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            SceneManager.MoveGameObjectToScene(go, preview);
            var animator = go.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid) throw new Exception("Target Avatar is not valid Humanoid.");
            report.AppendLine("Avatar valid Humanoid: " + AssetDatabase.GetAssetPath(animator.avatar));
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (HumanTrait.RequiredBone(i) && !bone) throw new Exception("Missing required bone: " + (HumanBodyBones)i);
                if (bone) report.AppendLine((HumanBodyBones)i + " = " + AnimationUtility.CalculateTransformPath(bone, go.transform));
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0) report.AppendLine("Missing scripts: " + t.name + " x" + missing);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) report.AppendLine("Renderer: " + r.name + " active=" + r.gameObject.activeInHierarchy + " materials=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "NULL")));
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Controllers/Player_Nodachi.controller");
            var clips = controller.animationClips.Distinct().ToArray();
            report.AppendLine("Controller clips: " + clips.Length);
            foreach (var clip in clips) if (!clip.humanMotion) throw new Exception("Non-Humanoid clip: " + clip.name);
            foreach (var path in clips.SelectMany(AnimationUtility.GetCurveBindings).Where(b => b.type != typeof(Animator)).Select(b => b.path).Distinct()) report.AppendLine("Non-humanoid binding: " + path);
            report.AppendLine("AUDIT PASSED");
            Directory.CreateDirectory("Library/SchoolKatanaIntegration");
            File.WriteAllText("Library/SchoolKatanaIntegration/audit.txt", report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
