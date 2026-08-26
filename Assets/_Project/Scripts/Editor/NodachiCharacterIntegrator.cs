using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates an isolated Nodachi player integration while preserving the original
/// Main scene, controller, character hierarchy and ability assets.
/// </summary>
[InitializeOnLoad]
public static class NodachiCharacterIntegrator
{
    private const string SourceScenePath = "Assets/_Project/Scenes/Main.unity";
    private const string TargetScenePath = "Assets/_Project/Scenes/Main_Nodachi.unity";
    private const string SourceControllerPath = "Assets/_Project/Animations/Controllers/9CG_Sword.controller";
    private const string TargetControllerPath = "Assets/_Project/Animations/Controllers/Player_Nodachi.controller";
    private const string AbilityTargetFolder = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Nodachi";
    private const string NewAnimationRoot = "Assets/ThirdParty/Nodachi Sword Animation Pack/Animations/Humanoid";
    private const string OldAnimationRoot = "Assets/ThirdParty/SwordAnimationPack";
    private const string NewPrefabPath = "Assets/ThirdParty/Nodachi Sword Animation Pack/PreFabs/9CG_Nodachi_Sword.prefab";
    private const string NewModelPath = "Assets/ThirdParty/Nodachi Sword Animation Pack/Model/9CG_Nodachi_Sword.fbx";
    private const string SessionCompleteKey = "UnityLearning.NodachiIntegrationComplete";
    private const string UrpMaterialFolder = "Assets/_Project/Art/Characters/Nodachi/Materials";
    private const string SessionMaterialKey = "UnityLearning.NodachiMaterialsConverted";

    private static readonly Dictionary<string, string> ExplicitClipMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Run_Fast_Combat_Lean_L_Loop"] = "Run_Fast_Lean_L_Loop",
            ["Run_Fast_Combat_Lean_R_Loop"] = "Run_Fast_Lean_R_Loop",
            ["Run_Fast_Combat_Loop"] = "Run_Fast_Loop",
            ["Run_Fast_Combat_Turn_L"] = "Run_Fast_Turn_L",
            ["Run_Fast_Combat_Turn_R"] = "Run_Fast_Turn_R",
            ["Dodge_B"] = "Dodge_B_180",
            ["Dodge_F"] = "Dodge_F_0",
            ["Dodge_L"] = "Dodge_L_90",
            ["Dodge_R"] = "Dodge_R_90",
            ["Dodge_Combat_B"] = "Dodge_Combat_B_180",
            ["Dodge_Combat_F"] = "Dodge_Combat_F_0",
            ["Dodge_Combat_L"] = "Dodge_Combat_L_90",
            ["Dodge_Combat_R"] = "Dodge_Combat_R_90",
            ["Dodge_Combat_R_L_45"] = "Dodge_Combat_F_L_45",
            ["Idle_Combat_To_Idle_ArmsOnly"] = "Idle_Combat_to_Idle",
            ["Idle_Combat_To_Idle"] = "Idle_Combat_to_Idle"
        };

    static NodachiCharacterIntegrator()
    {
        EditorApplication.delayCall += AutoIntegrateOnce;
        EditorApplication.delayCall += AutoConvertMaterialsOnce;
    }

    [MenuItem("Tools/Player/Create Nodachi Integration")]
    public static void CreateIntegrationFromMenu()
    {
        CreateIntegration();
    }

    [MenuItem("Tools/Player/Convert Nodachi Materials To URP")]
    public static void ConvertMaterialsFromMenu()
    {
        ConvertNodachiMaterialsToUrp();
    }

    private static void AutoConvertMaterialsOnce()
    {
        if (SessionState.GetBool(SessionMaterialKey, false) ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null)
        {
            return;
        }

        try
        {
            ConvertNodachiMaterialsToUrp();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void AutoIntegrateOnce()
    {
        if (SessionState.GetBool(SessionCompleteKey, false) ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            IntegrationSceneIsComplete())
        {
            return;
        }

        try
        {
            CreateIntegration();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static void CreateIntegration()
    {
        ValidateSourceAssets();
        EnsureFolder(AbilityTargetFolder);

        Dictionary<string, AnimationClip> nodachiClips = LoadNodachiClips();
        AnimatorController controller = CreateNodachiController(nodachiClips);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null &&
            !AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath))
        {
            throw new InvalidOperationException($"Could not copy {SourceScenePath} to {TargetScenePath}.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Scene integrationScene = SceneManager.GetSceneByPath(TargetScenePath);
        bool wasAlreadyLoaded = integrationScene.IsValid() && integrationScene.isLoaded;
        if (!wasAlreadyLoaded)
        {
            integrationScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        }
        try
        {
            GameObject player = integrationScene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Player" || root.name == "Player_Nodachi");
            if (player == null)
            {
                throw new InvalidOperationException("The copied Main scene does not contain a Player root object.");
            }

            Transform legacyRoot = player.transform.Find("LegacyCharacter_Disabled");
            Animator oldAnimator = legacyRoot != null
                ? legacyRoot.GetComponent<Animator>()
                : player.GetComponentInChildren<Animator>(true);
            if (oldAnimator == null)
            {
                throw new InvalidOperationException("The existing Player does not contain an Animator.");
            }

            Dictionary<UnityEngine.Object, UnityEngine.Object> abilityMap =
                CloneEquippedAbilities(player, nodachiClips);
            GameObject nodachiVisual = CreateNodachiVisual(player, oldAnimator, controller);
            Animator newAnimator = nodachiVisual.GetComponent<Animator>();

            ReplacePlayerReferences(player, oldAnimator, newAnimator, abilityMap);
            ConfigureCombatNodes(player, newAnimator, nodachiVisual.transform);

            player.name = "Player_Nodachi";
            EditorSceneManager.MarkSceneDirty(integrationScene);
            if (!EditorSceneManager.SaveScene(integrationScene, TargetScenePath))
            {
                throw new InvalidOperationException($"Could not save {TargetScenePath}.");
            }
        }
        finally
        {
            if (!wasAlreadyLoaded && integrationScene.IsValid() && integrationScene.isLoaded &&
                SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(integrationScene, true);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SessionState.SetBool(SessionCompleteKey, true);
        Debug.Log(
            "Nodachi integration completed. Original Main scene, character, controller and abilities were preserved. " +
            $"Open {TargetScenePath} to test the new player.");
    }

    private static void ValidateSourceAssets()
    {
        string[] paths =
        {
            SourceScenePath,
            SourceControllerPath,
            NewPrefabPath,
            NewModelPath
        };

        foreach (string path in paths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                throw new FileNotFoundException($"Required integration asset is missing: {path}");
            }
        }
    }

    private static Dictionary<string, AnimationClip> LoadNodachiClips()
    {
        var result = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { NewAnimationRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && !result.ContainsKey(clip.name))
            {
                result.Add(clip.name, clip);
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("No Nodachi animation clips were found.");
        }

        return result;
    }

    private static AnimatorController CreateNodachiController(
        IReadOnlyDictionary<string, AnimationClip> nodachiClips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
        if (controller == null)
        {
            if (!AssetDatabase.CopyAsset(SourceControllerPath, TargetControllerPath))
            {
                throw new InvalidOperationException("Could not copy the existing player Animator Controller.");
            }

            AssetDatabase.ImportAsset(TargetControllerPath, ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
        }

        if (controller == null)
        {
            throw new InvalidOperationException("The Nodachi Animator Controller could not be loaded.");
        }

        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            ReplaceStateMachineMotions(layer.stateMachine, nodachiClips, unresolved);
        }

        if (unresolved.Count > 0)
        {
            throw new InvalidOperationException(
                "These controller clips could not be mapped to Nodachi animations: " +
                string.Join(", ", unresolved.OrderBy(name => name)));
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        List<string> legacyReferences = CollectControllerClips(controller)
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => path.StartsWith(OldAnimationRoot, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (legacyReferences.Count > 0)
        {
            throw new InvalidOperationException(
                "The Nodachi controller still references legacy character animations: " +
                string.Join(", ", legacyReferences));
        }

        return controller;
    }

    private static void ReplaceStateMachineMotions(
        AnimatorStateMachine stateMachine,
        IReadOnlyDictionary<string, AnimationClip> nodachiClips,
        ISet<string> unresolved)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            childState.state.motion = ReplaceMotion(childState.state.motion, nodachiClips, unresolved);
            EditorUtility.SetDirty(childState.state);
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            ReplaceStateMachineMotions(childMachine.stateMachine, nodachiClips, unresolved);
        }
    }

    private static Motion ReplaceMotion(
        Motion motion,
        IReadOnlyDictionary<string, AnimationClip> nodachiClips,
        ISet<string> unresolved)
    {
        if (motion is AnimationClip clip)
        {
            AnimationClip replacement = FindReplacementClip(clip, nodachiClips);
            if (replacement != null)
            {
                return replacement;
            }

            unresolved.Add(clip.name);
            return clip;
        }

        if (motion is BlendTree blendTree)
        {
            ChildMotion[] children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                children[i].motion = ReplaceMotion(children[i].motion, nodachiClips, unresolved);
            }

            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);
        }

        return motion;
    }

    private static AnimationClip FindReplacementClip(
        AnimationClip source,
        IReadOnlyDictionary<string, AnimationClip> nodachiClips)
    {
        if (source == null)
        {
            return null;
        }

        string normalized = source.name.EndsWith("_RM", StringComparison.OrdinalIgnoreCase)
            ? source.name.Substring(0, source.name.Length - 3)
            : source.name;

        if (ExplicitClipMap.TryGetValue(normalized, out string explicitName))
        {
            normalized = explicitName;
        }

        return nodachiClips.TryGetValue(normalized, out AnimationClip clip) ? clip : null;
    }

    private static IEnumerable<AnimationClip> CollectControllerClips(AnimatorController controller)
    {
        var clips = new HashSet<AnimationClip>();
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            CollectStateMachineClips(layer.stateMachine, clips);
        }

        return clips;
    }

    private static void CollectStateMachineClips(
        AnimatorStateMachine stateMachine,
        ISet<AnimationClip> clips)
    {
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            CollectMotionClips(state.state.motion, clips);
        }

        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            CollectStateMachineClips(child.stateMachine, clips);
        }
    }

    private static void CollectMotionClips(Motion motion, ISet<AnimationClip> clips)
    {
        if (motion is AnimationClip clip)
        {
            clips.Add(clip);
        }
        else if (motion is BlendTree blendTree)
        {
            foreach (ChildMotion child in blendTree.children)
            {
                CollectMotionClips(child.motion, clips);
            }
        }
    }

    private static Dictionary<UnityEngine.Object, UnityEngine.Object> CloneEquippedAbilities(
        GameObject player,
        IReadOnlyDictionary<string, AnimationClip> nodachiClips)
    {
        CombatController combatController = player.GetComponent<CombatController>();
        if (combatController == null)
        {
            throw new InvalidOperationException("Player is missing CombatController.");
        }

        var sourceAbilities = combatController.CombatDatas
            .SelectMany(group => group.CombatObjs)
            .Where(ability => ability != null)
            .Distinct()
            .ToList();
        var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

        foreach (AbilityScriptableObject sourceAbility in sourceAbilities)
        {
            AnimationClip newClip = FindReplacementClip(sourceAbility.Clip, nodachiClips);
            if (newClip == null)
            {
                throw new InvalidOperationException(
                    $"Ability {sourceAbility.name} has no matching Nodachi animation for {sourceAbility.Clip?.name}.");
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceAbility);
            string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{AbilityTargetFolder}/{Path.GetFileName(sourcePath)}");
            string existingPath = $"{AbilityTargetFolder}/{Path.GetFileName(sourcePath)}";
            AbilityScriptableObject clonedAbility =
                AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(existingPath);

            if (clonedAbility == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    throw new InvalidOperationException($"Could not clone ability {sourcePath}.");
                }

                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
                clonedAbility = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(destinationPath);
            }

            if (clonedAbility == null)
            {
                throw new InvalidOperationException($"Cloned ability could not be loaded: {sourcePath}");
            }

            SerializedObject serializedAbility = new(clonedAbility);
            SerializedProperty clipProperty = serializedAbility.FindProperty("Clip");
            clipProperty.objectReferenceValue = newClip;
            serializedAbility.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clonedAbility);
            replacements[sourceAbility] = clonedAbility;
        }

        AssetDatabase.SaveAssets();

        // Combo-window subassets may point to the next legacy ability. Redirect
        // every cloned main/subasset reference after all clones are available.
        foreach (UnityEngine.Object clonedMainAsset in replacements.Values)
        {
            string path = AssetDatabase.GetAssetPath(clonedMainAsset);
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                ReplaceSerializedObjectReferences(subAsset, replacements);
            }
        }

        AssetDatabase.SaveAssets();
        return replacements;
    }

    private static GameObject CreateNodachiVisual(
        GameObject player,
        Animator oldAnimator,
        RuntimeAnimatorController controller)
    {
        Transform existingVisual = player.transform.Find("NodachiVisual");
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewPrefabPath);
        GameObject nodachiVisual = existingVisual != null
            ? existingVisual.gameObject
            : PrefabUtility.InstantiatePrefab(sourcePrefab, player.transform) as GameObject;
        if (nodachiVisual == null)
        {
            throw new InvalidOperationException("Could not instantiate the Nodachi character prefab.");
        }

        nodachiVisual.name = "NodachiVisual";
        nodachiVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        nodachiVisual.transform.localScale = Vector3.one;

        Animator animator = nodachiVisual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = nodachiVisual.AddComponent<Animator>();
        }

        if (animator == null)
        {
            throw new InvalidOperationException("Could not add an Animator to the Nodachi visual root.");
        }
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(NewModelPath)
            .OfType<Avatar>()
            .FirstOrDefault(candidate => candidate.isHuman && candidate.isValid);
        if (avatar == null)
        {
            throw new InvalidOperationException("The Nodachi model does not expose a valid Humanoid Avatar.");
        }

        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        oldAnimator.gameObject.name = "LegacyCharacter_Disabled";
        oldAnimator.gameObject.SetActive(false);
        return nodachiVisual;
    }

    private static void ReplacePlayerReferences(
        GameObject player,
        Animator oldAnimator,
        Animator newAnimator,
        IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> abilityMap)
    {
        var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>(abilityMap)
        {
            [oldAnimator] = newAnimator
        };

        foreach (MonoBehaviour component in player.GetComponents<MonoBehaviour>())
        {
            ReplaceSerializedObjectReferences(component, replacements);
        }

        RootMotionParentApplier rootMotion = player.GetComponent<RootMotionParentApplier>();
        if (rootMotion != null)
        {
            SerializedObject serializedRootMotion = new(rootMotion);
            SerializedProperty receiver = serializedRootMotion.FindProperty("receiver");
            SerializedProperty sourceAnimator = serializedRootMotion.FindProperty("sourceAnimator");
            receiver.objectReferenceValue = null;
            sourceAnimator.objectReferenceValue = newAnimator;
            serializedRootMotion.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rootMotion);
        }
    }

    private static void ReplaceSerializedObjectReferences(
        UnityEngine.Object target,
        IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.GetIterator();
        bool changed = false;
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue == null ||
                !replacements.TryGetValue(property.objectReferenceValue, out UnityEngine.Object replacement))
            {
                continue;
            }

            property.objectReferenceValue = replacement;
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void ConfigureCombatNodes(
        GameObject player,
        Animator animator,
        Transform visualRoot)
    {
        CombatController controller = player.GetComponent<CombatController>();
        Transform weapon = FindDeepChild(visualRoot, "Nodachi_Weapon_R") ??
                           FindDeepChild(visualRoot, "Nodachi_Sword") ?? visualRoot;
        (Transform weaponBase, Transform weaponTip) = EnsureWeaponMarkers(weapon, visualRoot);

        Transform spine = animator.GetBoneTransform(HumanBodyBones.Chest) ??
                          animator.GetBoneTransform(HumanBodyBones.Spine) ?? visualRoot;
        Transform body = animator.GetBoneTransform(HumanBodyBones.Hips) ?? visualRoot;
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand) ?? visualRoot;
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand) ?? visualRoot;
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot) ?? visualRoot;
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot) ?? visualRoot;

        controller._animator = animator;
        controller.Nodes = new List<CharacterNode>
        {
            Node(CharacterNode.NodeType.Animator, animator.transform),
            Node(CharacterNode.NodeType.BottomCenter, player.transform),
            Node(CharacterNode.NodeType.BodyCenter, body),
            Node(CharacterNode.NodeType.Head, animator.GetBoneTransform(HumanBodyBones.Head) ?? visualRoot),
            Node(CharacterNode.NodeType.Spine, spine),
            Node(CharacterNode.NodeType.Hand, rightHand),
            Node(CharacterNode.NodeType.RHand, rightHand),
            Node(CharacterNode.NodeType.LHand, leftHand),
            Node(CharacterNode.NodeType.Foot, rightFoot),
            Node(CharacterNode.NodeType.LFoot, leftFoot),
            Node(CharacterNode.NodeType.RFoot, rightFoot),
            Node(CharacterNode.NodeType.Weapon, weapon),
            Node(CharacterNode.NodeType.WeaponBase, weaponBase),
            Node(CharacterNode.NodeType.WeaponTip, weaponTip)
        };
        EditorUtility.SetDirty(controller);
    }

    private static CharacterNode Node(CharacterNode.NodeType type, Transform transform)
    {
        return new CharacterNode { type = type, NodeTrans = transform };
    }

    private static (Transform weaponBase, Transform weaponTip) EnsureWeaponMarkers(
        Transform weapon,
        Transform visualRoot)
    {
        Transform weaponBase = weapon.Find("WeaponBase");
        Transform weaponTip = weapon.Find("WeaponTip");
        if (weaponBase != null && weaponTip != null)
        {
            return (weaponBase, weaponTip);
        }

        Bounds? localBounds = null;
        foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true)
                     .Where(item => item.name.IndexOf("Nodachi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    item.name.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z),
                new(min.x, max.y, min.z), new(max.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z),
                new(min.x, max.y, max.z), new(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 local = weapon.InverseTransformPoint(corner);
                if (localBounds == null)
                {
                    localBounds = new Bounds(local, Vector3.zero);
                }
                else
                {
                    Bounds expanded = localBounds.Value;
                    expanded.Encapsulate(local);
                    localBounds = expanded;
                }
            }
        }

        Vector3 basePosition = Vector3.zero;
        Vector3 tipPosition = Vector3.forward;
        if (localBounds.HasValue)
        {
            Bounds bounds = localBounds.Value;
            int longestAxis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 :
                bounds.size.y >= bounds.size.z ? 1 : 2;
            basePosition = bounds.center;
            tipPosition = bounds.center;
            basePosition[longestAxis] = bounds.min[longestAxis];
            tipPosition[longestAxis] = bounds.max[longestAxis];
        }

        weaponBase ??= CreateMarker("WeaponBase", weapon, basePosition);
        weaponTip ??= CreateMarker("WeaponTip", weapon, tipPosition);
        return (weaponBase, weaponTip);
    }

    private static Transform CreateMarker(string name, Transform parent, Vector3 localPosition)
    {
        GameObject marker = new(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool IntegrationSceneIsComplete()
    {
        string path = ToAbsolutePath(TargetScenePath);
        if (!File.Exists(path))
        {
            return false;
        }

        string sceneText = File.ReadAllText(path);
        return sceneText.Contains("m_Name: Player_Nodachi") &&
               sceneText.Contains("m_Name: NodachiVisual") &&
               sceneText.Contains("m_Name: LegacyCharacter_Disabled") &&
               !sceneText.Contains("guid: c2e28c6312c3ae34c902ae692de56a1e") &&
               !sceneText.Contains("guid: 5880d7edbd714d245ad3f919041e9078") &&
               !sceneText.Contains("guid: 6b6f3a263c7ba034ca74b6e1ac28c6a9") &&
               !sceneText.Contains("guid: 693585e0c9a86d140ad56dce1b6b3307");
    }

    private static void ConvertNodachiMaterialsToUrp()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");
        }

        EnsureFolder(UrpMaterialFolder);
        Scene scene = SceneManager.GetSceneByPath(TargetScenePath);
        bool wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasAlreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject player = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Player_Nodachi");
            Transform visual = player != null ? player.transform.Find("NodachiVisual") : null;
            if (visual == null)
            {
                throw new InvalidOperationException("Main_Nodachi does not contain NodachiVisual.");
            }

            var converted = new Dictionary<Material, Material>();
            bool sceneChanged = false;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                    {
                        continue;
                    }

                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    if (!sourcePath.StartsWith(
                            "Assets/ThirdParty/Nodachi Sword Animation Pack/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!converted.TryGetValue(source, out Material target))
                    {
                        target = CreateOrUpdateUrpMaterial(source, urpLit);
                        converted.Add(source, target);
                    }

                    materials[i] = target;
                    rendererChanged = true;
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    sceneChanged = true;
                }
            }

            if (converted.Count == 0)
            {
                bool allUrp = visual.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .All(material => material.shader != null &&
                                     material.shader.name.StartsWith(
                                         "Universal Render Pipeline/",
                                         StringComparison.Ordinal));
                if (!allUrp)
                {
                    throw new InvalidOperationException(
                        "No convertible Nodachi materials were found, but non-URP materials remain.");
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, TargetScenePath))
                {
                    throw new InvalidOperationException("Could not save URP material overrides to Main_Nodachi.");
                }
            }
        }
        finally
        {
            if (!wasAlreadyLoaded && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        AssetDatabase.SaveAssets();
        SessionState.SetBool(SessionMaterialKey, true);
        Debug.Log("Nodachi materials converted to project-owned URP Lit materials.");
    }

    private static Material CreateOrUpdateUrpMaterial(Material source, Shader urpLit)
    {
        Texture baseTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        Texture normalTexture = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;
        Color baseColor = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        float metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
        float smoothness = source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.5f;
        Vector2 textureScale = source.HasProperty("_MainTex")
            ? source.GetTextureScale("_MainTex")
            : Vector2.one;
        Vector2 textureOffset = source.HasProperty("_MainTex")
            ? source.GetTextureOffset("_MainTex")
            : Vector2.zero;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string suffix = sourcePath.IndexOf("/Materials/Materials/", StringComparison.OrdinalIgnoreCase) >= 0
            ? "Weapon"
            : "Character";
        string targetPath = $"{UrpMaterialFolder}/{source.name}_{suffix}_URP.mat";
        Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (target == null)
        {
            target = new Material(urpLit) { name = $"{source.name}_{suffix}_URP" };
            AssetDatabase.CreateAsset(target, targetPath);
        }
        else
        {
            target.shader = urpLit;
        }

        target.SetTexture("_BaseMap", baseTexture);
        target.SetTextureScale("_BaseMap", textureScale);
        target.SetTextureOffset("_BaseMap", textureOffset);
        target.SetColor("_BaseColor", baseColor);
        target.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        target.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        target.SetFloat("_Surface", 0f);
        target.SetFloat("_AlphaClip", 0f);
        if (normalTexture != null)
        {
            target.SetTexture("_BumpMap", normalTexture);
            target.EnableKeyword("_NORMALMAP");
        }
        else
        {
            target.SetTexture("_BumpMap", null);
            target.DisableKeyword("_NORMALMAP");
        }

        target.renderQueue = -1;
        EditorUtility.SetDirty(target);
        return target;
    }
}
