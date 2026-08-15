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
        AnimationClip exitClip = AssetDatabase.LoadAllAssetsAtPath(ExitClipPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

        if (exitClip == null)
        {
            Debug.LogError($"Combat stance exit clip was not found at {ExitClipPath}.");
            return;
        }

        AnimatorControllerLayer layer = controller.layers
            .FirstOrDefault(candidate => candidate.name == UpperBodyLayerName);

        bool changed = maskChanged;
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
            bool shouldBeActive = part == AvatarMaskBodyPart.Body ||
                                  part == AvatarMaskBodyPart.Head ||
                                  part == AvatarMaskBodyPart.LeftArm ||
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
}
