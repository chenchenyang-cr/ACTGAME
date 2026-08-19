using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class LocomotionAnimatorBuilder
{
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/9CG_Sword.controller";
    private const string BackupPath = "Assets/_Project/Animations/Controllers/9CG_Sword_Legacy.controller";
    private const string ClipRoot = "Assets/ThirdParty/SwordAnimationPack/Animation/Humanoid";

    private const string MoveX = "MoveX";
    private const string MoveY = "MoveY";
    private const string MoveSpeed = "MoveSpeed";
    private const string CombatWeight = "CombatWeight";
    private const string IsMoving = "IsMoving";
    private const string StartX = "StartX";
    private const string StartY = "StartY";
    private const string StopX = "StopX";
    private const string StopY = "StopY";
    private const string TurnDirection = "TurnDirection";
    private const string DodgeX = "DodgeX";
    private const string DodgeY = "DodgeY";
    private const string LocomotionStartTag = "LocomotionStart";
    private const string LocomotionLoopTag = "LocomotionLoop";
    private const string LocomotionEndTag = "LocomotionEnd";
    private const string LocomotionTurn180Tag = "LocomotionTurn180";
    private const float SharedTransitionDuration = 0.12f;

    // Captured from the currently tuned Animator Controller.
    private static readonly LocomotionTransitionConfig NormalTransitionConfig =
        new LocomotionTransitionConfig(
            startToLoopExitTime: 0.6f,
            startToLoopDuration: 0.5f,
            loopToEndDuration: 0.06f,
            endToIdleExitTime: 0.7f,
            endToIdleDuration: 0.5f);

    private static readonly LocomotionTransitionConfig CombatTransitionConfig =
        new LocomotionTransitionConfig(
            startToLoopExitTime: 0.6f,
            startToLoopDuration: 0.5f,
            loopToEndDuration: 0.06f,
            endToIdleExitTime: 0.7f,
            endToIdleDuration: 0.5f);

    private static readonly DirectionClip[] ForwardDirections =
    {
        new DirectionClip("F_0", new Vector2(0f, 1f)),
        new DirectionClip("F_L_45", new Vector2(-0.707107f, 0.707107f)),
        new DirectionClip("F_R_45", new Vector2(0.707107f, 0.707107f)),
        new DirectionClip("F_L_90", new Vector2(-1f, 0f)),
        new DirectionClip("F_R_90", new Vector2(1f, 0f))
    };

    private static readonly DirectionClip[] BackwardDirections =
    {
        new DirectionClip("B_180", new Vector2(0f, -1f)),
        new DirectionClip("B_L_45", new Vector2(-0.707107f, -0.707107f)),
        new DirectionClip("B_R_45", new Vector2(0.707107f, -0.707107f)),
        new DirectionClip("B_L_90", new Vector2(-1f, 0f)),
        new DirectionClip("B_R_90", new Vector2(1f, 0f))
    };

    private static readonly DirectionClip[] PhaseDirections =
    {
        new DirectionClip("F_0", new Vector2(0f, 1f)),
        new DirectionClip("F_L_45", new Vector2(-0.707107f, 0.707107f)),
        new DirectionClip("F_R_45", new Vector2(0.707107f, 0.707107f)),
        new DirectionClip("F_L_90", new Vector2(-1f, 0f)),
        new DirectionClip("F_R_90", new Vector2(1f, 0f)),
        new DirectionClip("B_L_45", new Vector2(-0.707107f, -0.707107f)),
        new DirectionClip("B_R_45", new Vector2(0.707107f, -0.707107f)),
        new DirectionClip("B_180", new Vector2(0f, -1f))
    };

    private static Dictionary<string, AnimationClip> clipsByName;

    [MenuItem("Tools/Locomotion/Rebuild Start Loop Stop Animator")]
    public static void RebuildFromMenu()
    {
        Build(true);
    }

    private static void Build(bool force)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Locomotion controller was not found at {ControllerPath}.");
            return;
        }

        if (!force && IsAlreadyBuilt(controller))
        {
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(BackupPath))
        {
            AssetDatabase.CopyAsset(ControllerPath, BackupPath);
        }

        CacheClips();
        try
        {
            RebuildController(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Built complete Normal/Combat Start-Loop-Stop locomotion state machine.", controller);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool IsAlreadyBuilt(AnimatorController controller)
    {
        bool hasParameters = controller.parameters.Any(parameter => parameter.name == IsMoving) &&
                             controller.parameters.Any(parameter => parameter.name == StartX) &&
                             controller.parameters.Any(parameter => parameter.name == StopX);
        if (!hasParameters || controller.layers.Length == 0)
        {
            return false;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine normal = root.stateMachines
            .Select(child => child.stateMachine)
            .FirstOrDefault(machine => machine.name == "NormalLocomotion");
        AnimatorStateMachine combat = root.stateMachines
            .Select(child => child.stateMachine)
            .FirstOrDefault(machine => machine.name == "CombatLocomotion");

        return HasLocomotionStates(normal) &&
               HasLocomotionStates(combat) &&
               HasTransitionConfig(normal, NormalTransitionConfig) &&
               HasTransitionConfig(combat, CombatTransitionConfig);
    }

    private static bool HasLocomotionStates(AnimatorStateMachine machine)
    {
        if (machine == null)
        {
            return false;
        }

        Dictionary<string, AnimatorState> states = machine.states
            .Select(child => child.state)
            .ToDictionary(state => state.name, state => state);

        return states.TryGetValue("Start", out AnimatorState start) && start.tag == LocomotionStartTag &&
               states.TryGetValue("Loop", out AnimatorState loop) && loop.tag == LocomotionLoopTag &&
               states.TryGetValue("End", out AnimatorState end) && end.tag == LocomotionEndTag;
    }

    private static bool HasTransitionConfig(
        AnimatorStateMachine machine,
        LocomotionTransitionConfig config)
    {
        Dictionary<string, AnimatorState> states = machine.states
            .Select(child => child.state)
            .ToDictionary(state => state.name, state => state);

        if (!states.TryGetValue("Start", out AnimatorState start) ||
            !states.TryGetValue("Loop", out AnimatorState loop))
        {
            return false;
        }

        if (!states.TryGetValue("End", out AnimatorState end))
        {
            return false;
        }

        bool hasStartToLoop = start.transitions.Any(transition =>
            transition.hasExitTime &&
            transition.destinationState == loop &&
            !transition.hasFixedDuration &&
            Mathf.Approximately(transition.exitTime, config.startToLoopExitTime) &&
            Mathf.Approximately(transition.duration, config.startToLoopDuration));
        bool hasLoopToEnd = loop.transitions.Any(transition =>
            transition.destinationState == end &&
            !transition.hasExitTime &&
            transition.hasFixedDuration &&
            Mathf.Approximately(transition.duration, config.loopToEndDuration));
        bool hasEndToIdle = end.transitions.Any(transition =>
            transition.hasExitTime &&
            transition.hasFixedDuration &&
            Mathf.Approximately(transition.exitTime, config.endToIdleExitTime) &&
            Mathf.Approximately(transition.duration, config.endToIdleDuration));
        return hasStartToLoop && hasLoopToEnd && hasEndToIdle;
    }

    private static void RebuildController(AnimatorController controller)
    {
        AnimatorStateMachine root = GetOrCreateRootStateMachine(controller);
        ClearStateMachine(root, controller);
        ConfigureParameters(controller);

        AnimatorState idle = CreateState(root, "Idle", CreateIdleTree(controller), new Vector3(280f, 80f));
        AnimatorStateMachine normalMachine = root.AddStateMachine("NormalLocomotion", new Vector3(180f, 240f));
        AnimatorStateMachine combatMachine = root.AddStateMachine("CombatLocomotion", new Vector3(420f, 240f));

        AnimatorState normalStart = CreateState(normalMachine, "Start", CreateLocomotionTree(controller, "Normal_Start", false, "Start", StartX, StartY, MoveSpeed), new Vector3(80f, 120f));
        AnimatorState normalLoop = CreateState(normalMachine, "Loop", CreateLocomotionTree(controller, "Normal_Loop", false, "Loop", MoveX, MoveY, MoveSpeed), new Vector3(280f, 120f));
        AnimatorState normalTurn180 = CreateState(normalMachine, "Turn180", CreateTurn180Tree(controller), new Vector3(480f, 40f));
        AnimatorState normalStop = CreateState(normalMachine, "End", CreateLocomotionTree(controller, "Normal_Stop", false, "Stop", StopX, StopY, MoveSpeed), new Vector3(680f, 120f));
        AnimatorState combatStart = CreateState(combatMachine, "Start", CreateLocomotionTree(controller, "Combat_Start", true, "Start", StartX, StartY, MoveSpeed), new Vector3(80f, 120f));
        AnimatorState combatLoop = CreateState(combatMachine, "Loop", CreateLocomotionTree(controller, "Combat_Loop", true, "Loop", MoveX, MoveY, MoveSpeed), new Vector3(280f, 120f));
        AnimatorState combatStop = CreateState(combatMachine, "End", CreateLocomotionTree(controller, "Combat_Stop", true, "Stop", StopX, StopY, MoveSpeed), new Vector3(480f, 120f));

        SetLocomotionTags(normalStart, normalLoop, normalStop);
        SetLocomotionTags(combatStart, combatLoop, combatStop);
        normalTurn180.tag = LocomotionTurn180Tag;

        root.defaultState = idle;
        normalMachine.defaultState = normalStart;
        combatMachine.defaultState = combatStart;

        AddTransition(idle, normalStart, false, 0f,
            Condition(AnimatorConditionMode.If, IsMoving),
            Condition(AnimatorConditionMode.Less, CombatWeight, 0.5f));
        AddTransition(idle, combatStart, false, 0f,
            Condition(AnimatorConditionMode.If, IsMoving),
            Condition(AnimatorConditionMode.Greater, CombatWeight, 0.5f));

        AnimatorStateTransition turn180ToLoop = AddTransition(
            normalTurn180,
            normalLoop,
            true,
            0.75f,
            Condition(AnimatorConditionMode.If, IsMoving));
        turn180ToLoop.duration = 0.1f;
        AnimatorStateTransition turn180ToStop = AddTransition(
            normalTurn180,
            normalStop,
            true,
            0.75f,
            Condition(AnimatorConditionMode.IfNot, IsMoving));
        turn180ToStop.duration = 0.1f;

        ConfigureLocomotionChain(normalStart, normalLoop, normalStop, idle, NormalTransitionConfig);
        ConfigureLocomotionChain(combatStart, combatLoop, combatStop, idle, CombatTransitionConfig);

        AddTransition(normalStart, combatStart, false, 0f, Condition(AnimatorConditionMode.Greater, CombatWeight, 0.5f));
        AddTransition(combatStart, normalStart, false, 0f, Condition(AnimatorConditionMode.Less, CombatWeight, 0.5f));
        AddTransition(normalLoop, combatLoop, false, 0f, Condition(AnimatorConditionMode.Greater, CombatWeight, 0.5f));
        AddTransition(combatLoop, normalLoop, false, 0f, Condition(AnimatorConditionMode.Less, CombatWeight, 0.5f));
        AddTransition(normalStop, combatStop, false, 0f, Condition(AnimatorConditionMode.Greater, CombatWeight, 0.5f));
        AddTransition(combatStop, normalStop, false, 0f, Condition(AnimatorConditionMode.Less, CombatWeight, 0.5f));

        CombatStanceAnimatorConfigurator.EnsureConfigured();
        DodgeAnimatorConfigurator.EnsureConfigured(controller);

    }

    private static AnimatorStateMachine GetOrCreateRootStateMachine(AnimatorController controller)
    {
        if (controller.layers.Length > 0 && controller.layers[0].stateMachine != null)
        {
            AnimatorStateMachine existing = controller.layers[0].stateMachine;
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Base Layer",
                    defaultWeight = 1f,
                    stateMachine = existing
                }
            };
            return existing;
        }

        AnimatorStateMachine root = new AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(root, controller);
        controller.layers = new[]
        {
            new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = root
            }
        };
        return root;
    }

    private static void ClearStateMachine(AnimatorStateMachine root, AnimatorController controller)
    {
        foreach (ChildAnimatorState child in root.states.ToArray())
        {
            root.RemoveState(child.state);
        }

        foreach (ChildAnimatorStateMachine child in root.stateMachines.ToArray())
        {
            root.RemoveStateMachine(child.stateMachine);
        }

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree)
            {
                UnityEngine.Object.DestroyImmediate(asset, true);
            }
        }
    }

    private static void ConfigureParameters(AnimatorController controller)
    {
        controller.parameters = new[]
        {
            FloatParameter(MoveX),
            FloatParameter(MoveY),
            FloatParameter(MoveSpeed),
            FloatParameter(CombatWeight),
            BoolParameter(IsMoving),
            FloatParameter(StartX),
            FloatParameter(StartY),
            FloatParameter(StopX),
            FloatParameter(StopY),
            FloatParameter(TurnDirection),
            FloatParameter(DodgeX),
            FloatParameter(DodgeY),
        };
    }

    private static AnimatorControllerParameter FloatParameter(string name)
    {
        return new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float };
    }

    private static AnimatorControllerParameter BoolParameter(string name)
    {
        return new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Bool };
    }

    private static AnimatorState CreateState(AnimatorStateMachine root, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = root.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static void SetLocomotionTags(AnimatorState start, AnimatorState loop, AnimatorState end)
    {
        start.tag = LocomotionStartTag;
        loop.tag = LocomotionLoopTag;
        end.tag = LocomotionEndTag;
    }

    private static BlendTree CreateIdleTree(AnimatorController controller)
    {
        BlendTree tree = CreateTree(controller, "Generated_Idle", BlendTreeType.Simple1D, CombatWeight, CombatWeight);
        tree.useAutomaticThresholds = false;
        tree.AddChild(RequireClip("Idle"), 0f);
        tree.AddChild(RequireClip("Idle_Combat"), 1f);
        return tree;
    }

    private static BlendTree CreateTurn180Tree(AnimatorController controller)
    {
        BlendTree tree = CreateTree(
            controller,
            "Generated_Normal_Turn180",
            BlendTreeType.Simple1D,
            TurnDirection,
            TurnDirection);
        tree.useAutomaticThresholds = false;
        tree.AddChild(RequireClip("Run_Fast_Turn_L_RM"), -1f);
        tree.AddChild(RequireClip("Run_Fast_Turn_R_RM"), 1f);
        return tree;
    }

    private static BlendTree CreateLocomotionTree(
        AnimatorController controller,
        string label,
        bool combat,
        string phase,
        string xParameter,
        string yParameter,
        string speedParameter)
    {
        BlendTree gaitTree = CreateTree(controller, "Generated_" + label, BlendTreeType.Simple1D, speedParameter, yParameter);
        gaitTree.useAutomaticThresholds = false;
        gaitTree.AddChild(CreateDirectionTree(controller, label + "_Walk", "Walk", combat, phase, xParameter, yParameter), 0.35f);
        gaitTree.AddChild(CreateDirectionTree(controller, label + "_Run", "Run", combat, phase, xParameter, yParameter), 1f);
        return gaitTree;
    }

    private static BlendTree CreateDirectionTree(
        AnimatorController controller,
        string label,
        string gait,
        bool combat,
        string phase,
        string xParameter,
        string yParameter)
    {
        if (!string.Equals(phase, "Loop", StringComparison.Ordinal))
        {
            return CreatePhaseDirectionTree(
                controller,
                label,
                gait,
                combat,
                phase,
                xParameter,
                yParameter);
        }

        BlendTree directionTree = CreateTree(controller, "Generated_" + label, BlendTreeType.Simple1D, yParameter, xParameter);
        directionTree.useAutomaticThresholds = false;
        directionTree.AddChild(CreateHemisphereTree(controller, label + "_Backward", gait, combat, phase, xParameter, yParameter, BackwardDirections), -0.001f);
        directionTree.AddChild(CreateHemisphereTree(controller, label + "_Forward", gait, combat, phase, xParameter, yParameter, ForwardDirections), 0.001f);
        return directionTree;
    }

    private static BlendTree CreatePhaseDirectionTree(
        AnimatorController controller,
        string label,
        string gait,
        bool combat,
        string phase,
        string xParameter,
        string yParameter)
    {
        BlendTree tree = CreateTree(
            controller,
            "Generated_" + label + "_Discrete8Way",
            BlendTreeType.FreeformDirectional2D,
            xParameter,
            yParameter);

        foreach (DirectionClip direction in PhaseDirections)
        {
            tree.AddChild(
                RequireDirectionalClip(gait, combat, phase, direction.suffix),
                direction.position);
        }

        return tree;
    }

    private static BlendTree CreateHemisphereTree(
        AnimatorController controller,
        string label,
        string gait,
        bool combat,
        string phase,
        string xParameter,
        string yParameter,
        IEnumerable<DirectionClip> directions)
    {
        BlendTree tree = CreateTree(controller, "Generated_" + label, BlendTreeType.FreeformDirectional2D, xParameter, yParameter);
        foreach (DirectionClip direction in directions)
        {
            tree.AddChild(RequireDirectionalClip(gait, combat, phase, direction.suffix), direction.position);
        }
        return tree;
    }

    private static BlendTree CreateTree(
        AnimatorController controller,
        string name,
        BlendTreeType type,
        string parameter,
        string parameterY)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            hideFlags = HideFlags.HideInHierarchy,
            blendType = type,
            blendParameter = parameter,
            blendParameterY = parameterY
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static AnimationClip RequireDirectionalClip(string gait, bool combat, string phase, string suffix)
    {
        string prefix = combat ? gait + "_Combat" : gait;
        string withRootMotionSuffix = $"{prefix}_{phase}_{suffix}_RM";
        if (clipsByName.TryGetValue(withRootMotionSuffix, out AnimationClip clip))
        {
            return clip;
        }

        return RequireClip($"{prefix}_{phase}_{suffix}");
    }

    private static AnimationClip RequireClip(string clipName)
    {
        if (clipsByName.TryGetValue(clipName, out AnimationClip clip))
        {
            return clip;
        }

        throw new InvalidOperationException($"Required animation clip was not found: {clipName}");
    }

    private static void CacheClips()
    {
        clipsByName = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && !clipsByName.ContainsKey(clip.name))
            {
                clipsByName.Add(clip.name, clip);
            }
        }
    }

    private static void ConfigureLocomotionChain(
        AnimatorState start,
        AnimatorState loop,
        AnimatorState stop,
        AnimatorState idle,
        LocomotionTransitionConfig config)
    {
        AddTransition(start, stop, false, 0f, Condition(AnimatorConditionMode.IfNot, IsMoving));
        AnimatorStateTransition startToLoop = AddTransition(
            start,
            loop,
            true,
            config.startToLoopExitTime,
            Condition(AnimatorConditionMode.If, IsMoving));
        startToLoop.hasFixedDuration = false;
        startToLoop.duration = config.startToLoopDuration;
        AnimatorStateTransition loopToStop = AddTransition(
            loop,
            stop,
            false,
            0f,
            Condition(AnimatorConditionMode.IfNot, IsMoving));
        loopToStop.duration = config.loopToEndDuration;
        AddTransition(stop, start, false, 0f, Condition(AnimatorConditionMode.If, IsMoving));
        AnimatorStateTransition stopToIdle = AddTransition(
            stop,
            idle,
            true,
            config.endToIdleExitTime,
            Condition(AnimatorConditionMode.IfNot, IsMoving));
        stopToIdle.duration = config.endToIdleDuration;
    }

    private static AnimatorStateTransition AddTransition(
        AnimatorState source,
        AnimatorState destination,
        bool hasExitTime,
        float exitTime,
        params TransitionCondition[] conditions)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = SharedTransitionDuration;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        transition.orderedInterruption = true;

        foreach (TransitionCondition condition in conditions)
        {
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }

        return transition;
    }

    private readonly struct LocomotionTransitionConfig
    {
        public readonly float startToLoopExitTime;
        public readonly float startToLoopDuration;
        public readonly float loopToEndDuration;
        public readonly float endToIdleExitTime;
        public readonly float endToIdleDuration;

        public LocomotionTransitionConfig(
            float startToLoopExitTime,
            float startToLoopDuration,
            float loopToEndDuration,
            float endToIdleExitTime,
            float endToIdleDuration)
        {
            this.startToLoopExitTime = startToLoopExitTime;
            this.startToLoopDuration = startToLoopDuration;
            this.loopToEndDuration = loopToEndDuration;
            this.endToIdleExitTime = endToIdleExitTime;
            this.endToIdleDuration = endToIdleDuration;
        }
    }

    private static TransitionCondition Condition(AnimatorConditionMode mode, string parameter, float threshold = 0f)
    {
        return new TransitionCondition(mode, parameter, threshold);
    }

    private readonly struct DirectionClip
    {
        public readonly string suffix;
        public readonly Vector2 position;

        public DirectionClip(string suffix, Vector2 position)
        {
            this.suffix = suffix;
            this.position = position;
        }
    }

    private readonly struct TransitionCondition
    {
        public readonly AnimatorConditionMode mode;
        public readonly string parameter;
        public readonly float threshold;

        public TransitionCondition(AnimatorConditionMode mode, string parameter, float threshold)
        {
            this.mode = mode;
            this.parameter = parameter;
            this.threshold = threshold;
        }
    }
}

[InitializeOnLoad]
internal static class CombatLocomotionTransitionConfigurator
{
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/9CG_Sword.controller";

    static CombatLocomotionTransitionConfigurator()
    {
        EditorApplication.delayCall += EnsureConfiguredWhenReady;
    }

    private static void EnsureConfiguredWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureConfiguredWhenReady;
            return;
        }

        EnsureConfigured();
    }

    private static void EnsureConfigured()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0)
        {
            return;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine normal = FindMachine(root, "NormalLocomotion");
        AnimatorStateMachine combat = FindMachine(root, "CombatLocomotion");
        AnimatorState idle = FindState(root, "Idle");
        if (normal == null || combat == null || idle == null)
        {
            return;
        }

        AnimatorState normalStart = FindState(normal, "Start");
        AnimatorState normalLoop = FindState(normal, "Loop");
        AnimatorState normalEnd = FindState(normal, "End");
        AnimatorState combatStart = FindState(combat, "Start");
        AnimatorState combatLoop = FindState(combat, "Loop");
        AnimatorState combatEnd = FindState(combat, "End");
        if (normalStart == null || normalLoop == null || normalEnd == null ||
            combatStart == null || combatLoop == null || combatEnd == null)
        {
            return;
        }

        bool changed = false;
        changed |= CopyTransition(normalStart, normalLoop, combatStart, combatLoop);
        changed |= CopyTransition(normalStart, normalEnd, combatStart, combatEnd);
        changed |= CopyTransition(normalLoop, normalEnd, combatLoop, combatEnd);
        changed |= CopyTransition(normalEnd, normalStart, combatEnd, combatStart);
        changed |= CopyTransition(normalEnd, idle, combatEnd, idle);

        if (!changed)
        {
            return;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Synchronized CombatLocomotion transitions with NormalLocomotion.", controller);
    }

    private static bool CopyTransition(
        AnimatorState sourceState,
        AnimatorState sourceDestination,
        AnimatorState targetState,
        AnimatorState targetDestination)
    {
        AnimatorStateTransition source = sourceState.transitions
            .FirstOrDefault(transition => transition.destinationState == sourceDestination);
        AnimatorStateTransition target = targetState.transitions
            .FirstOrDefault(transition => transition.destinationState == targetDestination);
        if (source == null || target == null)
        {
            return false;
        }

        bool changed = source.hasExitTime != target.hasExitTime ||
                       !Mathf.Approximately(source.exitTime, target.exitTime) ||
                       source.hasFixedDuration != target.hasFixedDuration ||
                       !Mathf.Approximately(source.duration, target.duration) ||
                       !Mathf.Approximately(source.offset, target.offset) ||
                       source.interruptionSource != target.interruptionSource ||
                       source.orderedInterruption != target.orderedInterruption ||
                       source.mute != target.mute ||
                       source.solo != target.solo ||
                       !ConditionsMatch(source.conditions, target.conditions);
        if (!changed)
        {
            return false;
        }

        target.hasExitTime = source.hasExitTime;
        target.exitTime = source.exitTime;
        target.hasFixedDuration = source.hasFixedDuration;
        target.duration = source.duration;
        target.offset = source.offset;
        target.interruptionSource = source.interruptionSource;
        target.orderedInterruption = source.orderedInterruption;
        target.mute = source.mute;
        target.solo = source.solo;
        target.conditions = source.conditions;
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool ConditionsMatch(
        AnimatorCondition[] source,
        AnimatorCondition[] target)
    {
        if (source.Length != target.Length)
        {
            return false;
        }

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i].mode != target[i].mode ||
                source[i].parameter != target[i].parameter ||
                !Mathf.Approximately(source[i].threshold, target[i].threshold))
            {
                return false;
            }
        }

        return true;
    }

    private static AnimatorStateMachine FindMachine(
        AnimatorStateMachine root,
        string machineName)
    {
        return root.stateMachines
            .Select(child => child.stateMachine)
            .FirstOrDefault(machine => machine.name == machineName);
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == stateName);
    }
}

[InitializeOnLoad]
internal static class DodgeAnimatorConfigurator
{
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/9CG_Sword.controller";
    private const string NormalClipRoot =
        "Assets/ThirdParty/SwordAnimationPack/Animation/Humanoid/06_Dodge/01_Dodge";
    private const string CombatClipRoot =
        "Assets/ThirdParty/SwordAnimationPack/Animation/Humanoid/06_Dodge/02_Dodge_Combat";
    private const string DodgeX = "DodgeX";
    private const string DodgeY = "DodgeY";
    private const string DodgeTag = "Dodge";
    private const float Diagonal = 0.707107f;

    private static readonly DodgeClip[] NormalClips =
    {
        Clip(NormalClipRoot, "Dodge_F", 0f, 1f),
        Clip(NormalClipRoot, "Dodge_F_R_45", Diagonal, Diagonal),
        Clip(NormalClipRoot, "Dodge_R", 1f, 0f),
        Clip(NormalClipRoot, "Dodge_B_R_45", Diagonal, -Diagonal),
        Clip(NormalClipRoot, "Dodge_B", 0f, -1f),
        Clip(NormalClipRoot, "Dodge_B_L_45", -Diagonal, -Diagonal),
        Clip(NormalClipRoot, "Dodge_L", -1f, 0f),
        Clip(NormalClipRoot, "Dodge_F_L_45", -Diagonal, Diagonal),
    };

    private static readonly DodgeClip[] CombatClips =
    {
        Clip(CombatClipRoot, "Dodge_Combat_F", 0f, 1f),
        // The package names this clip R_L_45, but its authored root motion is forward-right.
        Clip(CombatClipRoot, "Dodge_Combat_R_L_45", Diagonal, Diagonal),
        Clip(CombatClipRoot, "Dodge_Combat_R", 1f, 0f),
        Clip(CombatClipRoot, "Dodge_Combat_B_R_45", Diagonal, -Diagonal),
        Clip(CombatClipRoot, "Dodge_Combat_B", 0f, -1f),
        Clip(CombatClipRoot, "Dodge_Combat_B_L_45", -Diagonal, -Diagonal),
        Clip(CombatClipRoot, "Dodge_Combat_L", -1f, 0f),
        Clip(CombatClipRoot, "Dodge_Combat_F_L_45", -Diagonal, Diagonal),
    };

    static DodgeAnimatorConfigurator()
    {
        EditorApplication.delayCall += EnsureConfiguredWhenReady;
    }

    public static void EnsureConfigured(AnimatorController controller = null)
    {
        controller ??= AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length == 0)
        {
            return;
        }

        AnimationClip[] normalClips = LoadClips(NormalClips);
        AnimationClip[] combatClips = LoadClips(CombatClips);
        if (normalClips == null || combatClips == null)
        {
            return;
        }

        bool changed = EnsureFloatParameter(controller, DodgeX);
        changed |= EnsureFloatParameter(controller, DodgeY);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        BlendTree normalTree = GetOrCreateTree(controller, "Generated_Dodge_Normal", ref changed);
        BlendTree combatTree = GetOrCreateTree(controller, "Generated_Dodge_Combat", ref changed);
        changed |= ConfigureTree(normalTree, NormalClips, normalClips);
        changed |= ConfigureTree(combatTree, CombatClips, combatClips);
        changed |= EnsureState(root, "DodgeNormal", normalTree, new Vector3(820f, 20f));
        changed |= EnsureState(root, "DodgeCombat", combatTree, new Vector3(820f, 140f));

        if (!changed)
        {
            return;
        }

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured normal/combat eight-direction dodge animation states.", controller);
    }

    private static void EnsureConfiguredWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureConfiguredWhenReady;
            return;
        }

        EnsureConfigured();
    }

    private static bool EnsureFloatParameter(AnimatorController controller, string parameterName)
    {
        AnimatorControllerParameter parameter = controller.parameters
            .FirstOrDefault(candidate => candidate.name == parameterName);
        if (parameter != null)
        {
            if (parameter.type != AnimatorControllerParameterType.Float)
            {
                Debug.LogError($"Animator parameter '{parameterName}' must be a Float.", controller);
            }

            return false;
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Float);
        return true;
    }

    private static BlendTree GetOrCreateTree(
        AnimatorController controller,
        string treeName,
        ref bool changed)
    {
        BlendTree tree = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
            .OfType<BlendTree>()
            .FirstOrDefault(candidate => candidate.name == treeName);
        if (tree != null)
        {
            return tree;
        }

        tree = new BlendTree
        {
            name = treeName,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        changed = true;
        return tree;
    }

    private static bool ConfigureTree(
        BlendTree tree,
        DodgeClip[] definitions,
        AnimationClip[] clips)
    {
        ChildMotion[] children = tree.children;
        bool childrenMatch = children.Length == definitions.Length;
        if (childrenMatch)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (children[i].motion != clips[i] ||
                    Vector2.SqrMagnitude(children[i].position - definitions[i].position) > 0.000001f)
                {
                    childrenMatch = false;
                    break;
                }
            }
        }

        bool settingsMatch = tree.blendType == BlendTreeType.FreeformDirectional2D &&
                             tree.blendParameter == DodgeX &&
                             tree.blendParameterY == DodgeY;
        if (settingsMatch && childrenMatch)
        {
            return false;
        }

        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = DodgeX;
        tree.blendParameterY = DodgeY;
        tree.children = Array.Empty<ChildMotion>();
        for (int i = 0; i < definitions.Length; i++)
        {
            tree.AddChild(clips[i], definitions[i].position);
        }

        EditorUtility.SetDirty(tree);
        return true;
    }

    private static bool EnsureState(
        AnimatorStateMachine root,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = root.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == stateName);
        bool changed = false;
        if (state == null)
        {
            state = root.AddState(stateName, position);
            changed = true;
        }

        if (state.motion != motion)
        {
            state.motion = motion;
            changed = true;
        }

        if (state.tag != DodgeTag)
        {
            state.tag = DodgeTag;
            changed = true;
        }

        if (!state.writeDefaultValues)
        {
            state.writeDefaultValues = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(state);
        }

        return changed;
    }

    private static AnimationClip[] LoadClips(DodgeClip[] definitions)
    {
        AnimationClip[] clips = new AnimationClip[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(definitions[i].path);
            if (clips[i] != null)
            {
                continue;
            }

            Debug.LogError($"Dodge animation clip was not found: {definitions[i].path}");
            return null;
        }

        return clips;
    }

    private static DodgeClip Clip(string root, string clipName, float x, float y)
    {
        return new DodgeClip($"{root}/{clipName}.anim", new Vector2(x, y));
    }

    private readonly struct DodgeClip
    {
        public readonly string path;
        public readonly Vector2 position;

        public DodgeClip(string path, Vector2 position)
        {
            this.path = path;
            this.position = position;
        }
    }
}
