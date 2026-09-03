using System.Collections.Generic;
using System.IO;
using System.Linq;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityLearning.EnemySystem;

public static class SwordEnemyPrefabBuilder
{
    // 直接复用角色模型自带的 Humanoid Avatar，并只从 Humanoid 动画目录选取动作。
    private const string AutoBuildRequestFile = "Temp/SwordEnemyAutoBuild.request";
    private const string LocomotionRepairRequestFile =
        "Temp/SwordEnemyLocomotionRepair.request";
    private const string SourceRoot = "Assets/ThirdParty/SwordAnimationPack";
    private const string SourceModelPath = SourceRoot + "/Model/9CG_Sword.FBX";
    private const string HumanoidAnimationRoot = SourceRoot + "/Animation/Humanoid";
    private const string OutputRoot = "Assets/_Project/Enemies/SwordEnemy";
    private const string AbilityPath = OutputRoot + "/Abilities/SwordEnemy_Attack_01.asset";
    private const string ConfigPath = OutputRoot + "/Config/SwordEnemyConfig.asset";
    private const string ControllerPath = OutputRoot + "/Animators/SwordEnemy.controller";
    private const string PrefabPath = OutputRoot + "/Prefabs/SwordEnemy.prefab";

    [MenuItem("Tools/Enemy/Rebuild Sword Enemy Prefab")]
    public static void Build()
    {
        EnsureFolders();

        DeleteGeneratedAsset(OutputRoot + "/Animations");
        AnimationClip idle = FindClip("Idle_Combat");
        AnimationClip alert = FindClip("Idle_To_Idle_Combat");
        AnimationClip[] locomotionStart = FindDirectionalClips("Walk_Combat_Start");
        AnimationClip[] locomotionLoop = FindDirectionalClips("Walk_Combat_Loop");
        AnimationClip[] locomotionStop = FindDirectionalClips("Walk_Combat_Stop");
        AnimationClip attack = FindClip("Combo_Attack_01_01");
        AnimationClip hit = FindClip("Hit_Combat_F");
        AnimationClip death = FindClip("Hit_Combat_Death");

        AbilityScriptableObject ability = CreateAbility(attack);
        EnemyConfig config = CreateConfig(ability);
        AnimatorController animatorController = CreateAnimatorController(
            idle,
            alert,
            locomotionStart,
            locomotionLoop,
            locomotionStop,
            attack,
            hit,
            death);
        CreatePrefab(config, ability, animatorController);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Sword enemy generated at {PrefabPath}");
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    [MenuItem("Tools/Enemy/Repair Sword Enemy Locomotion Transitions")]
    public static void RepairLocomotionTransitions()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            throw new MissingReferenceException(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = FindState(stateMachine, "Idle");
        AnimatorState startState = FindState(stateMachine, "LocomotionStart");
        AnimatorState loopState = FindState(stateMachine, "Locomotion");
        AnimatorState stopState = FindState(stateMachine, "LocomotionStop");

        ConfigureLocomotionTags(startState, loopState, stopState);

        // 生成器拥有这四个状态的移动过渡，修复时先移除旧的不完整链路。
        ClearTransitions(idleState);
        ClearTransitions(startState);
        ClearTransitions(loopState);
        ClearTransitions(stopState);
        ConfigureLocomotionTransitions(idleState, startState, loopState, stopState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("SwordEnemy Locomotion 过渡已修复：Idle / Start / Loop / Stop 由 IsMoving 完整驱动。", controller);
    }

    [InitializeOnLoadMethod]
    private static void BuildWhenRequested()
    {
        string requestPath = Path.GetFullPath(AutoBuildRequestFile);
        if (!File.Exists(requestPath)) return;

        File.Delete(requestPath);
        EditorApplication.delayCall += () =>
        {
            try
            {
                Build();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [InitializeOnLoadMethod]
    private static void RepairLocomotionWhenRequested()
    {
        string requestPath = Path.GetFullPath(LocomotionRepairRequestFile);
        if (!File.Exists(requestPath)) return;

        File.Delete(requestPath);
        EditorApplication.delayCall += () =>
        {
            try
            {
                RepairLocomotionTransitions();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    private static AbilityScriptableObject CreateAbility(AnimationClip attackClip)
    {
        DeleteGeneratedAsset(AbilityPath);
        AbilityScriptableObject ability = ScriptableObject.CreateInstance<AbilityScriptableObject>();
        ability.name = "SwordEnemy_Attack_01";
        ability.AbilityType = AbilityScriptableObject.AbilityTypes.OneShot;
        ability.Clip = attackClip;
        ability.PreviewPercentageRange = new Vector2(0f, 1f);
        ability.events = new List<AbilityEvent>();
        AssetDatabase.CreateAsset(ability, AbilityPath);
        return ability;
    }

    private static EnemyConfig CreateConfig(AbilityScriptableObject ability)
    {
        DeleteGeneratedAsset(ConfigPath);
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        config.name = "SwordEnemyConfig";

        SerializedObject serialized = new SerializedObject(config);
        SetFloat(serialized, "detectionDistance", 8f);
        SetFloat(serialized, "loseTargetDistance", 12f);
        SetFloat(serialized, "alertDuration", 0.45f);
        SetFloat(serialized, "rotationSpeed", 540f);
        SetFloat(serialized, "arrivalTolerance", 0.3f);
        SetFloat(serialized, "combatEnterDistance", 4f);
        SetFloat(serialized, "chaseResumeDistance", 5f);
        SetFloat(serialized, "decisionInterval", 0.25f);
        SetFloat(serialized, "attackApproachAllowance", 2f);
        SetFloat(serialized, "postAttackRecovery", 0.8f);
        SetFloat(serialized, "defaultStaggerDuration", 0.45f);
        SetFloat(serialized, "maximumHealth", 100f);
        SetFloat(serialized, "maximumPoise", 30f);
        SetFloat(serialized, "poiseRecoveryDelay", 1.5f);
        SetFloat(serialized, "poiseRecoveryPerSecond", 15f);
        SetString(serialized, "idleState", "Idle");
        SetString(serialized, "alertState", "Alert");
        SetString(serialized, "locomotionState", "Locomotion");
        SetString(serialized, "locomotionStartState", "LocomotionStart");
        SetString(serialized, "locomotionStopState", "LocomotionStop");
        SetString(serialized, "staggerState", "Hit");
        SetString(serialized, "deathState", "Death");
        SetString(serialized, "moveSpeedParameter", "MoveSpeed");
        SetString(serialized, "moveXParameter", "MoveX");
        SetString(serialized, "moveYParameter", "MoveY");
        SetString(serialized, "isMovingParameter", "IsMoving");
        SetString(serialized, "startXParameter", "StartX");
        SetString(serialized, "startYParameter", "StartY");
        SetString(serialized, "stopXParameter", "StopX");
        SetString(serialized, "stopYParameter", "StopY");

        SerializedProperty attacks = serialized.FindProperty("attacks");
        attacks.arraySize = 1;
        SerializedProperty firstAttack = attacks.GetArrayElementAtIndex(0);
        firstAttack.FindPropertyRelative("displayName").stringValue = "基础斩击";
        firstAttack.FindPropertyRelative("ability").objectReferenceValue = ability;
        firstAttack.FindPropertyRelative("minimumRange").floatValue = 0.8f;
        firstAttack.FindPropertyRelative("maximumRange").floatValue = 2.25f;
        firstAttack.FindPropertyRelative("cooldown").floatValue = 1.15f;
        firstAttack.FindPropertyRelative("priority").floatValue = 1f;
        firstAttack.FindPropertyRelative("facingTolerance").floatValue = 18f;
        firstAttack.FindPropertyRelative("entryTolerance").floatValue = 0.3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(config, ConfigPath);
        return config;
    }

    private static AnimatorController CreateAnimatorController(
        AnimationClip idle,
        AnimationClip alert,
        AnimationClip[] locomotionStart,
        AnimationClip[] locomotionLoop,
        AnimationClip[] locomotionStop,
        AnimationClip attack,
        AnimationClip hit,
        AnimationClip death)
    {
        DeleteGeneratedAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("StartX", AnimatorControllerParameterType.Float);
        controller.AddParameter("StartY", AnimatorControllerParameterType.Float);
        controller.AddParameter("StopX", AnimatorControllerParameterType.Float);
        controller.AddParameter("StopY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(200f, 0f));
        idleState.motion = idle;
        stateMachine.defaultState = idleState;
        stateMachine.AddState("Alert", new Vector3(420f, -80f)).motion = alert;
        AnimatorState locomotionStartState = stateMachine.AddState(
            "LocomotionStart",
            new Vector3(400f, 40f));
        locomotionStartState.motion = CreateDirectionalBlendTree(
            controller,
            "Enemy_Combat_Start_8Way",
            "StartX",
            "StartY",
            locomotionStart);
        AnimatorState locomotionState = stateMachine.AddState(
            "Locomotion",
            new Vector3(560f, 40f));
        locomotionState.motion = CreateDirectionalBlendTree(
            controller,
            "Enemy_Combat_Loop_8Way",
            "MoveX",
            "MoveY",
            locomotionLoop,
            idle);
        AnimatorState locomotionStopState = stateMachine.AddState(
            "LocomotionStop",
            new Vector3(720f, 40f));
        locomotionStopState.motion = CreateDirectionalBlendTree(
            controller,
            "Enemy_Combat_Stop_8Way",
            "StopX",
            "StopY",
            locomotionStop);
        ConfigureLocomotionTags(
            locomotionStartState,
            locomotionState,
            locomotionStopState);
        ConfigureLocomotionTransitions(
            idleState,
            locomotionStartState,
            locomotionState,
            locomotionStopState);
        stateMachine.AddState(attack.name, new Vector3(640f, 40f)).motion = attack;
        stateMachine.AddState("Hit", new Vector3(640f, -80f)).motion = hit;
        stateMachine.AddState("Death", new Vector3(860f, -80f)).motion = death;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static BlendTree CreateDirectionalBlendTree(
        AnimatorController controller,
        string treeName,
        string parameterX,
        string parameterY,
        AnimationClip[] clips,
        AnimationClip center = null)
    {
        if (clips == null || clips.Length != 8)
            throw new System.ArgumentException("八方向动画必须正好包含 8 个片段。", nameof(clips));

        BlendTree tree = new BlendTree
        {
            name = treeName,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = parameterX,
            blendParameterY = parameterY,
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        if (center != null) tree.AddChild(center, Vector2.zero);

        tree.AddChild(clips[0], new Vector2(0f, 1f));
        tree.AddChild(clips[1], new Vector2(-0.707107f, 0.707107f));
        tree.AddChild(clips[2], new Vector2(0.707107f, 0.707107f));
        tree.AddChild(clips[3], new Vector2(-1f, 0f));
        tree.AddChild(clips[4], new Vector2(1f, 0f));
        tree.AddChild(clips[5], new Vector2(-0.707107f, -0.707107f));
        tree.AddChild(clips[6], new Vector2(0.707107f, -0.707107f));
        tree.AddChild(clips[7], new Vector2(0f, -1f));
        return tree;
    }

    private static void AddExitTimeTransition(
        AnimatorState from,
        AnimatorState to,
        float exitTime,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
    }

    private static void ConfigureLocomotionTransitions(
        AnimatorState idleState,
        AnimatorState startState,
        AnimatorState loopState,
        AnimatorState stopState)
    {
        // 完整链路统一由 IsMoving 驱动，避免代码 CrossFade 与 Animator 过渡互相抢状态。
        AddBoolTransition(idleState, startState, true, 0.14f);
        AddBoolTransition(startState, stopState, false, 0.1f);
        AddExitTimeTransition(
            startState,
            loopState,
            0.72f,
            0.16f,
            true);
        AddBoolTransition(loopState, stopState, false, 0.14f);
        AddBoolTransition(stopState, startState, true, 0.12f);
        AddExitTimeTransition(
            stopState,
            idleState,
            0.78f,
            0.16f,
            false);
    }

    private static void ConfigureLocomotionTags(
        AnimatorState startState,
        AnimatorState loopState,
        AnimatorState stopState)
    {
        // RootMotionParentApplier 通过 Tag 识别 Start→Loop，并平滑两段动画的速度差。
        startState.tag = "LocomotionStart";
        loopState.tag = "LocomotionLoop";
        stopState.tag = "LocomotionStop";
    }

    private static void AddBoolTransition(
        AnimatorState from,
        AnimatorState to,
        bool expectedValue,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        transition.canTransitionToSelf = false;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            "IsMoving");
    }

    private static void AddExitTimeTransition(
        AnimatorState from,
        AnimatorState to,
        float exitTime,
        float duration,
        bool expectedIsMoving)
    {
        AddExitTimeTransition(from, to, exitTime, duration);
        AnimatorStateTransition transition = from.transitions.Last();
        transition.canTransitionToSelf = false;
        transition.AddCondition(
            expectedIsMoving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            "IsMoving");
    }

    private static AnimatorState FindState(
        AnimatorStateMachine stateMachine,
        string stateName)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state != null && child.state.name == stateName)
                return child.state;
        }
        throw new MissingReferenceException($"Animator 状态缺失：{stateName}");
    }

    private static void ClearTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions.ToArray();
        foreach (AnimatorStateTransition transition in transitions)
            state.RemoveTransition(transition);
    }

    private static void CreatePrefab(
        EnemyConfig config,
        AbilityScriptableObject ability,
        RuntimeAnimatorController animatorController)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (source == null) throw new MissingReferenceException(SourceModelPath);

        GameObject enemy = new GameObject("SwordEnemy");
        try
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            visual.name = "Visual_9CG_Sword";
            visual.transform.SetParent(enemy.transform, false);

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(SourceModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null) throw new MissingReferenceException("9CG_Sword Avatar");
            animator.avatar = avatar;
            // 动画产生位移，NavMeshAgent 只提供寻路意图。
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RootMotionReceiver rootMotionReceiver = visual.GetComponent<RootMotionReceiver>();
            if (rootMotionReceiver == null)
                rootMotionReceiver = visual.AddComponent<RootMotionReceiver>();

            RootMotionParentApplier rootMotionApplier = enemy.GetComponent<RootMotionParentApplier>();
            if (rootMotionApplier == null)
                rootMotionApplier = enemy.AddComponent<RootMotionParentApplier>();
            rootMotionApplier.SetSourceAnimator(animator);

            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.35f;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.speed = 3.5f;
            agent.acceleration = 12f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.updatePosition = false;
            agent.updateRotation = false;

            CombatController combat = enemy.GetComponent<CombatController>();
            if (combat == null) combat = enemy.AddComponent<CombatController>();
            combat._animator = animator;
            combat.AllowMotionTranslation = true;
            combat.CombatDatas = new List<CombatGroup>
            {
                new CombatGroup
                {
                    Label = "Enemy Attacks",
                    CombatObjs = new List<AbilityScriptableObject> { ability }
                }
            };

            if (enemy.GetComponent<EnemyBrain>() == null) enemy.AddComponent<EnemyBrain>();
            if (enemy.GetComponent<EnemyMotor>() == null) enemy.AddComponent<EnemyMotor>();
            if (enemy.GetComponent<EnemyCombatAdapter>() == null) enemy.AddComponent<EnemyCombatAdapter>();
            if (enemy.GetComponent<EnemyStateMachine>() == null) enemy.AddComponent<EnemyStateMachine>();
            if (enemy.GetComponent<EnemyDamageReceiver>() == null) enemy.AddComponent<EnemyDamageReceiver>();
            EnemyController enemyController = enemy.GetComponent<EnemyController>();
            if (enemyController == null) enemyController = enemy.AddComponent<EnemyController>();
            SerializedObject serializedEnemy = new SerializedObject(enemyController);
            serializedEnemy.FindProperty("config").objectReferenceValue = config;
            serializedEnemy.FindProperty("findPlayerTargetOnAwake").boolValue = true;
            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();

            // 直接覆盖可保留 Prefab 的 GUID 与本地对象映射，避免场景实例产生失效覆盖。
            PrefabUtility.SaveAsPrefabAsset(enemy, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }

    private static AnimationClip FindClip(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets(
            $"{clipName} t:AnimationClip",
            new[] { HumanoidAnimationRoot });
        AnimationClip clip = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .FirstOrDefault(candidate => candidate != null && candidate.name == clipName);
        if (clip == null) throw new MissingReferenceException($"Animation clip not found: {clipName}");
        return clip;
    }

    private static AnimationClip[] FindDirectionalClips(string prefix)
    {
        return new[]
        {
            FindClip(prefix + "_F_0_RM"),
            FindClip(prefix + "_F_L_45_RM"),
            FindClip(prefix + "_F_R_45_RM"),
            FindClip(prefix + "_F_L_90_RM"),
            FindClip(prefix + "_F_R_90_RM"),
            FindClip(prefix + "_B_L_45_RM"),
            FindClip(prefix + "_B_R_45_RM"),
            FindClip(prefix + "_B_180_RM")
        };
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Project", "Enemies");
        EnsureFolder("Assets/_Project/Enemies", "SwordEnemy");
        EnsureFolder(OutputRoot, "Abilities");
        EnsureFolder(OutputRoot, "Animators");
        EnsureFolder(OutputRoot, "Config");
        EnsureFolder(OutputRoot, "Prefabs");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void DeleteGeneratedAsset(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        serialized.FindProperty(name).floatValue = value;
    }

    private static void SetString(SerializedObject serialized, string name, string value)
    {
        serialized.FindProperty(name).stringValue = value;
    }
}
