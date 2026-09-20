using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit request marker lets the open editor perform the migration using Unity's asset APIs.
[InitializeOnLoad]
public static class UnagiPlayerIntegrator
{
    private const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    private const string ScenePath = "Assets/_Project/Scenes/Main_Nodachi.unity";
    private const string Request = "Library/UnagiPlayer.request";
    private const string Report = "Library/UnagiPlayerReport.txt";
    private static readonly string[] Clips = {
        "02_Idle/Unagi_Idle", "03_Run/Unagi_Run_Start", "03_Run/Unagi_Run",
        "03_Run/Unagi_Run_End", "04_Walk/Unagi_Walk"
    };
    private static bool running;

    static UnagiPlayerIntegrator() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists("Library/UnagiDrift.request"))
        {
            File.Delete("Library/UnagiDrift.request");
            ProbeDrift();
        }
        if (File.Exists("Library/UnagiRootFix.request"))
        {
            File.Delete("Library/UnagiRootFix.request");
            foreach (string relative in Clips)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Root + relative + ".fbx");
                var serializedImporter = new SerializedObject(importer);
                serializedImporter.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue = "Bip001";
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
            ProbeDrift();
        }
        if (File.Exists("Library/UnagiCleanup.request"))
        {
            File.Delete("Library/UnagiCleanup.request");
            CleanupDuplicateVisuals();
        }
        if (!File.Exists(Request) && File.Exists("Library/UnagiValidate.request") && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete("Library/UnagiValidate.request");
            ValidateMotion();
        }
        if (running || !File.Exists(Request) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Integrate();
    }

    [MenuItem("Tools/Unagi/Replace Player With Main Locomotion")]
    public static void Integrate()
    {
        running = true;
        var report = new List<string>();
        try
        {
            var clips = new List<AnimationClip>();
            foreach (string relative in Clips)
            {
                string path = Root + relative + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // Main exports contain metre-valued vertices but centimetre FBX units.
                importer.globalScale = 100f;
                importer.motionNodeName = "Root";
                // Root is the authored motion track; Bip001 is the moving skeleton
                // root used by the Generic importer to keep the mesh on the actor.
                var serializedImporter = new SerializedObject(importer);
                serializedImporter.FindProperty("m_HumanDescription.m_RootMotionBoneName").stringValue = "Bip001";
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                var definitions = importer.clipAnimations;
                foreach (var definition in definitions)
                {
                    definition.loopTime = relative.EndsWith("/Unagi_Idle") || relative.EndsWith("/Unagi_Run");
                    definition.lockRootRotation = false;
                    definition.lockRootPositionXZ = false;
                    definition.lockRootHeightY = true;
                }
                importer.clipAnimations = definitions;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Single(c => !c.name.StartsWith("__preview__"));
                clips.Add(clip);
                report.Add(clip.name + ": length=" + clip.length + ", loop=" + clip.isLooping);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // Preserve the controller GUID already used by the player.
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (asset != controller) Object.DestroyImmediate(asset, true);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (string parameter in new[] { "MoveX", "MoveY", "MoveSpeed", "CombatWeight",
                "StartX", "StartY", "StopX", "StopY", "StopSpeed", "TurnDirection" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddLayer("Base Layer");
            var layer = controller.layers[0];
            layer.defaultWeight = 1;
            controller.layers = new[] { layer };
            var machine = layer.stateMachine;
            var idle = machine.AddState("Idle", new Vector3(200, 60));
            idle.motion = clips[0];
            var locomotion = machine.AddStateMachine("NormalLocomotion", new Vector3(450, 60));
            var start = State(locomotion, "Start", clips[1], "LocomotionStart", 100);
            var loop = State(locomotion, "Loop", clips[2], "LocomotionLoop", 350);
            var end = State(locomotion, "End", clips[3], "LocomotionEnd", 600);
            var turn = State(locomotion, "Turn180", clips[4], "LocomotionTurn180", 850);
            machine.defaultState = idle;
            locomotion.defaultState = start;
            Transition(idle, start, true);
            Transition(start, end, false);
            Transition(start, loop, true, 0.85f);
            Transition(loop, end, false);
            Transition(end, start, true);
            Transition(end, idle, false, 0.85f);
            // Finish the authored rotation even if input is released midway.
            Transition(turn, loop, true, 0.95f);
            Transition(turn, end, false, 0.95f);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameObject player = scene.GetRootGameObjects().Single(o => o.name == "Player_Swordman");
            Animator oldAnimator = player.GetComponentInChildren<Animator>(true);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + Clips[0] + ".fbx");
            // Reuse the controlled Unagi instance when the migration is run again.
            var boundAnimator = new SerializedObject(player.GetComponent<PlayerStateMachine>())
                .FindProperty("animator").objectReferenceValue as Animator;
            GameObject visual = boundAnimator != null && boundAnimator.gameObject.name == "UnagiVisual"
                ? boundAnimator.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(model, player.transform);
            if (visual == boundAnimator?.gameObject) oldAnimator = boundAnimator;
            visual.name = "UnagiVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var receiver = visual.GetComponent<RootMotionReceiver>();
            if (receiver == null) receiver = visual.AddComponent<RootMotionReceiver>();
            ApplyMaterials(visual);

            // Redirect serialized Animator/receiver references before removing the old skeleton.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object reference = property.objectReferenceValue;
                    if (reference == oldAnimator) property.objectReferenceValue = animator;
                    else if (reference is RootMotionReceiver oldReceiver &&
                        oldReceiver.gameObject == oldAnimator.gameObject) property.objectReferenceValue = receiver;
                    else if (oldAnimator != animator && reference is Component component &&
                        component.transform.IsChildOf(oldAnimator.transform))
                        property.objectReferenceValue = null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            SetBool(player.GetComponent<PlayerStateMachine>(), "locomotionOnly", true);
            SetBool(player.GetComponent<PlayerMovement>(), "turn180RequiresFastMovement", false);
            var combat = player.GetComponent<CombatController>();
            if (combat != null)
            {
                combat.enabled = false;
                var serialized = new SerializedObject(combat);
                serialized.FindProperty("CombatDatas").ClearArray();
                serialized.FindProperty("Nodes").ClearArray();
                serialized.FindProperty("SelectedAbility").objectReferenceValue = null;
                serialized.FindProperty("clip").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var adapter = player.GetComponent<PlayerCombatAdapter>();
            if (adapter != null)
            {
                adapter.enabled = false;
                var serialized = new SerializedObject(adapter);
                foreach (string field in new[] { "firstLightAttack", "dodgeAbility", "parryStartAbility", "parryEndAbility" })
                    serialized.FindProperty(field).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour is PlayerDamageReceiver || behaviour is PlayerParryFeedback ||
                    behaviour is PlayerCommandInputAdapter) behaviour.enabled = false;
            if (oldAnimator != animator) Object.DestroyImmediate(oldAnimator.gameObject);
            RemoveDuplicateVisuals(player, animator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            report.Add("Model: " + AssetDatabase.GetAssetPath(model));
            report.Add("Avatar valid: " + (animator.avatar != null && animator.avatar.isValid));
            report.Add("Renderers: " + visual.GetComponentsInChildren<Renderer>(true).Length);
            foreach (var clip in clips)
                report.Add("Motion " + clip.name + ": averageSpeed=" + clip.averageSpeed +
                    ", angularSpeed=" + clip.averageAngularSpeed + ", rootCurves=" + clip.hasRootCurves);
            if (animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException("Unagi Generic avatar is invalid.");
            if (controller.animationClips.Length != 5)
                throw new InvalidOperationException("Unexpected controller clip count.");
            if (controller.animationClips.Any(c => !AssetDatabase.GetAssetPath(c).StartsWith(Root)))
                throw new InvalidOperationException("Controller contains non-Main animations.");
            if (clips[4].isLooping) throw new InvalidOperationException("Turn180 must not loop.");
            report.Add("SUCCESS");
        }
        catch (Exception exception)
        {
            report.Add(exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            File.WriteAllLines(Report, report);
            running = false;
        }
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name,
        AnimationClip clip, string tag, float x)
    {
        var state = machine.AddState(name, new Vector3(x, 150));
        state.motion = clip;
        state.tag = tag;
        state.writeDefaultValues = false;
        return state;
    }

    [MenuItem("Tools/Unagi/Remove Duplicate Player Visuals")]
    public static void CleanupDuplicateVisuals()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var player = scene.GetRootGameObjects().Single(o => o.name == "Player_Swordman");
        var animator = new SerializedObject(player.GetComponent<PlayerStateMachine>())
            .FindProperty("animator").objectReferenceValue as Animator;
        if (animator == null || !animator.transform.IsChildOf(player.transform))
            throw new InvalidOperationException("Cannot identify the controlled player visual.");
        int removed = RemoveDuplicateVisuals(player, animator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/UnagiCleanupReport.txt",
            "Removed=" + removed + ", Animators=" + player.GetComponentsInChildren<Animator>(true).Length +
            ", Renderers=" + player.GetComponentsInChildren<Renderer>(true).Length);
    }

    private static int RemoveDuplicateVisuals(GameObject player, Animator animator)
    {
        var duplicates = player.transform.Cast<Transform>()
            .Where(t => t.name == "UnagiVisual" && t != animator.transform).ToArray();
        foreach (var duplicate in duplicates) Object.DestroyImmediate(duplicate.gameObject);
        foreach (var component in new Component[] { player.GetComponent<PlayerMovement>(),
            player.GetComponent<PlayerStateMachine>(), player.GetComponent<RootMotionParentApplier>() })
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(component is RootMotionParentApplier ? "sourceAnimator" : "animator");
            property.objectReferenceValue = animator;
            if (component is RootMotionParentApplier)
                serialized.FindProperty("receiver").objectReferenceValue = animator.GetComponent<RootMotionReceiver>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        if (player.GetComponentsInChildren<Animator>(true).Length != 1)
            throw new InvalidOperationException("Player must have exactly one Animator.");
        return duplicates.Length;
    }

    private static void Transition(AnimatorState from, AnimatorState to, bool moving, float exit = -1)
    {
        var transition = from.AddTransition(to);
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "IsMoving");
        transition.hasExitTime = exit >= 0;
        transition.exitTime = Mathf.Max(0, exit);
        transition.hasFixedDuration = true;
        transition.duration = 0.12f;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
    }

    private static void SetBool(Object target, string name, bool value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Unagi/Validate Main Locomotion")]
    public static void ValidateMotion()
    {
        var report = new List<string>();
        GameObject instance = null;
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + Clips[0] + ".fbx");
            instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var skeleton = instance.GetComponentsInChildren<Transform>().Single(t => t.name == "Bip001");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            report.Add("Model bounds: " + bounds + ", initialized=" + animator.isInitialized);
            if (bounds.size.y < 1f || bounds.size.y > 3f)
                throw new InvalidOperationException("Model has incorrect world scale.");
            foreach (string state in new[] { "Idle", "NormalLocomotion.Start", "NormalLocomotion.Loop",
                "NormalLocomotion.End", "NormalLocomotion.Turn180" })
            {
                animator.SetBool("IsMoving", state != "Idle" && !state.EndsWith(".End"));
                animator.Play("Base Layer." + state, 0, 0);
                animator.Update(0);
                Vector3 delta = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                float duration = animator.GetCurrentAnimatorStateInfo(0).length;
                int frames = Mathf.Max(1, Mathf.FloorToInt(duration * 60 * 0.9f));
                for (int i = 0; i < frames; i++)
                {
                    animator.Update(1f / 60f);
                    delta += animator.deltaPosition;
                    rotation *= animator.deltaRotation;
                    Vector3 localSkeleton = instance.transform.InverseTransformPoint(skeleton.position);
                    if (new Vector2(localSkeleton.x, localSkeleton.z).magnitude > 0.65f)
                        throw new InvalidOperationException("Skeleton drifts away from the motion root in " + state);
                }
                report.Add(state + ": duration=" + duration + ", delta=" + delta +
                    ", rotation=" + rotation.eulerAngles);
                if (state.EndsWith(".Loop") && delta.magnitude < 1f)
                    throw new InvalidOperationException("Running has no usable root displacement.");
                if (state.EndsWith(".Turn180") && Quaternion.Angle(Quaternion.identity, rotation) < 170f)
                    throw new InvalidOperationException("Turn animation did not rotate 180 degrees.");
            }
            animator.SetBool("IsMoving", false);
            animator.Play("Base Layer.Idle", 0, 0);
            animator.Update(0);
            animator.SetBool("IsMoving", true);
            Advance(animator, 2f);
            ExpectState(animator, "Base Layer.NormalLocomotion.Loop");
            animator.SetBool("IsMoving", false);
            Advance(animator, 4f);
            ExpectState(animator, "Base Layer.Idle");
            animator.SetBool("IsMoving", true);
            Advance(animator, 0.3f);
            animator.SetBool("IsMoving", false);
            Advance(animator, 0.2f);
            animator.SetBool("IsMoving", true);
            Advance(animator, 2f);
            ExpectState(animator, "Base Layer.NormalLocomotion.Loop");
            animator.Play("Base Layer.NormalLocomotion.Turn180", 0, 0);
            animator.Update(0);
            animator.SetBool("IsMoving", false);
            Advance(animator, 5f);
            ExpectState(animator, "Base Layer.Idle");
            report.Add("PASS: start/run/stop, short tap/restart, release during Turn180.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var player = scene.GetRootGameObjects().Single(o => o.name == "Player_Swordman");
            var cameraObject = new GameObject("Unagi Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = player.transform.position + new Vector3(2.3f, 1.5f, 3.2f);
            camera.transform.LookAt(player.transform.position + Vector3.up * 0.9f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.fieldOfView = 35;
            var target = new RenderTexture(768, 768, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
            image.Apply();
            File.WriteAllBytes("Library/UnagiPlayerPreview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            report.Add("SUCCESS");
        }
        catch (Exception exception) { report.Add(exception.ToString()); }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            File.WriteAllLines("Library/UnagiMotionReport.txt", report);
        }
    }

    private static void Advance(Animator animator, float seconds)
    {
        for (int frame = 0; frame < Mathf.CeilToInt(seconds * 60); frame++)
            animator.Update(1f / 60f);
    }

    public static void ProbeDrift()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + Clips[0] + ".fbx");
        var instance = Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        var report = new List<string>();
        try
        {
            var animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("IsMoving", true);
            animator.Play("Base Layer.NormalLocomotion.Loop", 0, 0);
            animator.Update(0);
            var bones = instance.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "Root" || t.name == "Bip001" || t.name.Contains("Avatar_Female")).ToArray();
            foreach (var bone in bones)
                report.Add("Bone=" + AnimationUtility.CalculateTransformPath(bone, instance.transform));
            for (int frame = 0; frame <= 180; frame++)
            {
                if (frame % 30 == 0)
                {
                    report.Add("frame=" + frame + " model=" + instance.transform.position.ToString("F3"));
                    foreach (var bone in bones)
                        report.Add(bone.name + " local=" + bone.localPosition.ToString("F3") +
                            " relative=" + instance.transform.InverseTransformPoint(bone.position).ToString("F3"));
                }
                animator.Update(1f / 60f);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            File.WriteAllLines("Library/UnagiDriftReport.txt", report);
        }
    }

    private static void ExpectState(Animator animator, string path)
    {
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(path))
            throw new InvalidOperationException("Animator did not reach " + path);
    }

    private static void ApplyMaterials(GameObject visual)
    {
        const string folder = "Assets/_Project/Art/Characters/Unagi/Materials";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            string part = renderer.name.Contains("Hair") ? "Hair" :
                renderer.name.Contains("Face") ? "Face" :
                renderer.name.Contains("Weapon") ? "Weapon" : "Body";
            string path = folder + "/Unagi_" + part + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ThirdParty/UnagiAnimationPack/Textures/Unagi_" + part + "_D.tga"));
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        }
    }
}
