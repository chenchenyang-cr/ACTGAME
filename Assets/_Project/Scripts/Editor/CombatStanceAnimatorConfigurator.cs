using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CombatStanceAnimatorConfigurator
{
    private const string ControllerPath =
        "Assets/_Project/Animations/Controllers/9CG_Sword.controller";
    private const string UpperBodyMaskPath =
        "Assets/_Project/Animations/AvatarMasks/CombatUpperBody.mask";
    private const string ExitClipPath =
        "Assets/ThirdParty/SwordAnimationPack/Animation/Humanoid/01_Idle/Idle_Combat_To_Idle.anim";
    private const string ArmsOnlyExitClipPath =
        "Assets/_Project/Animations/Clips/Idle_Combat_To_Idle_ArmsOnly.anim";
    private const string ArmsOnlyClipVersion = "arms-only-v2";
    private const string UpperBodyLayerName = "Combat Upper Body";
    private const string ExitStateName = "Idle_Combat_To_Idle";

    static CombatStanceAnimatorConfigurator()
    {
        EditorApplication.delayCall += EnsureConfigured;
    }

    [MenuItem("Tools/Locomotion/Configure Combat Stance Upper Body")]
    public static void EnsureConfigured()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Animator Controller was not found at {ControllerPath}.");
            return;
        }

        AvatarMask upperBodyMask = GetOrCreateUpperBodyMask(out bool maskChanged);
        AnimationClip sourceExitClip = AssetDatabase.LoadAllAssetsAtPath(ExitClipPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

        if (sourceExitClip == null)
        {
            Debug.LogError($"Combat stance exit clip was not found at {ExitClipPath}.");
            return;
        }

        AnimationClip exitClip = GetOrCreateArmsOnlyExitClip(
            sourceExitClip,
            out bool clipChanged);
        if (exitClip == null)
        {
            return;
        }

        AnimatorControllerLayer layer = controller.layers
            .FirstOrDefault(candidate => candidate.name == UpperBodyLayerName);

        bool changed = maskChanged || clipChanged;
        if (layer == null)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = UpperBodyLayerName,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            layer = new AnimatorControllerLayer
            {
                name = UpperBodyLayerName,
                defaultWeight = 0f,
                avatarMask = upperBodyMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = stateMachine
            };
            controller.AddLayer(layer);
            changed = true;
        }
        else
        {
            if (layer.avatarMask != upperBodyMask)
            {
                layer.avatarMask = upperBodyMask;
                changed = true;
            }

            if (!Mathf.Approximately(layer.defaultWeight, 0f))
            {
                layer.defaultWeight = 0f;
                changed = true;
            }
        }

        AnimatorState exitState = layer.stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == ExitStateName);

        if (exitState == null)
        {
            exitState = layer.stateMachine.AddState(ExitStateName, new Vector3(280f, 80f));
            changed = true;
        }

        if (exitState.motion != exitClip)
        {
            exitState.motion = exitClip;
            changed = true;
        }

        if (layer.stateMachine.defaultState != exitState)
        {
            layer.stateMachine.defaultState = exitState;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        EditorUtility.SetDirty(upperBodyMask);
        EditorUtility.SetDirty(exitState);
        EditorUtility.SetDirty(layer.stateMachine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured combat stance upper-body exit animation.", controller);
    }

    private static AvatarMask GetOrCreateUpperBodyMask(out bool changed)
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        changed = false;
        if (mask == null)
        {
            mask = new AvatarMask { name = "CombatUpperBody" };
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            changed = true;
        }

        for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
             part < AvatarMaskBodyPart.LastBodyPart;
             part++)
        {
            bool shouldBeActive = part == AvatarMaskBodyPart.LeftArm ||
                                  part == AvatarMaskBodyPart.RightArm ||
                                  part == AvatarMaskBodyPart.LeftFingers ||
                                  part == AvatarMaskBodyPart.RightFingers;
            if (mask.GetHumanoidBodyPartActive(part) == shouldBeActive)
            {
                continue;
            }

            mask.SetHumanoidBodyPartActive(part, shouldBeActive);
            changed = true;
        }
        return mask;
    }

    private static AnimationClip GetOrCreateArmsOnlyExitClip(
        AnimationClip sourceClip,
        out bool changed)
    {
        string sourceSignature =
            $"{ArmsOnlyClipVersion}:{AssetDatabase.GetAssetDependencyHash(ExitClipPath)}";
        AnimationClip armsOnlyClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ArmsOnlyExitClipPath);
        AssetImporter importer = armsOnlyClip != null
            ? AssetImporter.GetAtPath(ArmsOnlyExitClipPath)
            : null;

        if (armsOnlyClip != null && importer != null &&
            importer.userData == sourceSignature)
        {
            changed = false;
            return armsOnlyClip;
        }

        bool created = armsOnlyClip == null;
        if (created)
        {
            armsOnlyClip = new AnimationClip
            {
                name = "Idle_Combat_To_Idle_ArmsOnly"
            };
            AssetDatabase.CreateAsset(armsOnlyClip, ArmsOnlyExitClipPath);
        }
        else
        {
            ClearClipCurves(armsOnlyClip);
        }

        armsOnlyClip.frameRate = sourceClip.frameRate;
        armsOnlyClip.wrapMode = sourceClip.wrapMode;
        AnimationClipSettings clipSettings =
            AnimationUtility.GetAnimationClipSettings(sourceClip);
        clipSettings.loopTime = false;
        clipSettings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(armsOnlyClip, clipSettings);

        foreach (EditorCurveBinding binding in
                 AnimationUtility.GetCurveBindings(sourceClip))
        {
            if (!IsArmCurve(binding))
            {
                continue;
            }

            AnimationUtility.SetEditorCurve(
                armsOnlyClip,
                binding,
                AnimationUtility.GetEditorCurve(sourceClip, binding));
        }

        foreach (EditorCurveBinding binding in
                 AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
        {
            if (!IsArmCurve(binding))
            {
                continue;
            }

            AnimationUtility.SetObjectReferenceCurve(
                armsOnlyClip,
                binding,
                AnimationUtility.GetObjectReferenceCurve(sourceClip, binding));
        }

        AnimationUtility.SetAnimationEvents(
            armsOnlyClip,
            AnimationUtility.GetAnimationEvents(sourceClip));
        armsOnlyClip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(armsOnlyClip);
        AssetDatabase.SaveAssets();

        importer = AssetImporter.GetAtPath(ArmsOnlyExitClipPath);
        if (importer != null)
        {
            importer.userData = sourceSignature;
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(ArmsOnlyExitClipPath);
        }

        changed = true;
        return armsOnlyClip;
    }

    private static void ClearClipCurves(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        foreach (EditorCurveBinding binding in
                 AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }
    }

    private static bool IsArmCurve(EditorCurveBinding binding)
    {
        string propertyName = binding.propertyName;
        if (propertyName.StartsWith("Left Shoulder ") ||
            propertyName.StartsWith("Right Shoulder ") ||
            propertyName.StartsWith("Left Arm ") ||
            propertyName.StartsWith("Right Arm ") ||
            propertyName.StartsWith("Left Forearm ") ||
            propertyName.StartsWith("Right Forearm ") ||
            propertyName.StartsWith("Left Hand ") ||
            propertyName.StartsWith("Right Hand ") ||
            propertyName.StartsWith("LeftHand.") ||
            propertyName.StartsWith("RightHand.") ||
            propertyName.StartsWith("LeftHandT.") ||
            propertyName.StartsWith("RightHandT.") ||
            propertyName.StartsWith("LeftHandQ.") ||
            propertyName.StartsWith("RightHandQ."))
        {
            return true;
        }

        string path = binding.path.ToLowerInvariant();
        return path.Contains("clavicle_l") ||
               path.Contains("clavicle_r") ||
               path.Contains("upperarm_l") ||
               path.Contains("upperarm_r") ||
               path.Contains("lowerarm_l") ||
               path.Contains("lowerarm_r") ||
               path.Contains("hand_l") ||
               path.Contains("hand_r");
    }
}
