using System;
using System.IO;
using System.Linq;
using System.Text;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiAttack01WeaponFrameRepair
{
    const string ModelPath = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/02_Idle/Unagi_Idle.fbx";
    const string SourcePath = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/01_Attack/Unagi_Attack_01.fbx";
    const string FixedPath = "Assets/_Project/Animations/Clips/Unagi_Attack_01_Fixed.anim";
    const string ControllerPath = "Assets/_Project/Animations/Controllers/Player_Swordman.controller";
    const string AbilityPath = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Swordman/Unagi_Attack_01.asset";
    const string HiltPath = "Bip001/Bn_katana_hilt";
    const string Request = "Library/UnagiAttack01WeaponFrameRepair.request";
    const string Report = "Library/UnagiAttack01WeaponFrameRepair.txt";

    static UnagiAttack01WeaponFrameRepair() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Repair();
    }

    [MenuItem("Tools/Unagi/Repair Attack 01 Weapon Frame")]
    public static void Repair()
    {
        var report = new StringBuilder();
        try
        {
            AnimationClip source = LoadClip(SourcePath);
            if (Mathf.RoundToInt(source.frameRate) != 30 || Mathf.RoundToInt(source.length * source.frameRate) != 20)
                throw new InvalidOperationException("Attack 01 source timing changed; expected 30 FPS and frames 0-20.");

            AnimationClip fixedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FixedPath);
            if (fixedClip == null)
            {
                fixedClip = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(fixedClip, FixedPath);
            }
            else EditorUtility.CopySerialized(source, fixedClip);
            fixedClip.name = "Unagi_Attack_01";

            GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                Transform hand = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bip001 R Hand");
                Transform hilt = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bn_katana_hilt");
                float previousTime = 2f / source.frameRate;
                float badTime = 3f / source.frameRate;
                float nextTime = 4f / source.frameRate;

                source.SampleAnimation(instance, previousTime);
                Vector3 previousPosition = hilt.localPosition;
                Quaternion previousRotation = hilt.localRotation;
                source.SampleAnimation(instance, nextTime);
                Vector3 nextPosition = hilt.localPosition;
                Quaternion nextRotation = hilt.localRotation;
                // This is an isolated bad key: both neighboring poses are correct.
                // Rebuild it halfway between those neighbors in the authored parent space.
                Vector3 targetPosition = Vector3.Lerp(previousPosition, nextPosition, 0.5f);
                Quaternion targetRotation = Quaternion.Slerp(previousRotation, nextRotation, 0.5f);

                SetKey(fixedClip, "m_LocalPosition.x", badTime, targetPosition.x);
                SetKey(fixedClip, "m_LocalPosition.y", badTime, targetPosition.y);
                SetKey(fixedClip, "m_LocalPosition.z", badTime, targetPosition.z);
                SetKey(fixedClip, "m_LocalRotation.x", badTime, targetRotation.x);
                SetKey(fixedClip, "m_LocalRotation.y", badTime, targetRotation.y);
                SetKey(fixedClip, "m_LocalRotation.z", badTime, targetRotation.z);
                SetKey(fixedClip, "m_LocalRotation.w", badTime, targetRotation.w);
                EditorUtility.SetDirty(fixedClip);

                // The fixed clip keeps the original state name so PlayerActionAnimator
                // can continue resolving Base Layer.Unagi_Attack_01 by clip name.
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                var state = controller.layers[0].stateMachine.states.Select(s => s.state)
                    .Single(s => s.name == "Unagi_Attack_01");
                state.motion = fixedClip;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(controller);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityPath);
                ability.Clip = fixedClip;
                EditorUtility.SetDirty(ability);
                AssetDatabase.SaveAssets();

                source.SampleAnimation(instance, badTime);
                Vector3 sourceBadPosition = hilt.localPosition;
                Quaternion sourceBadRotation = hilt.localRotation;
                fixedClip.SampleAnimation(instance, badTime);
                Vector3 fixedBadPosition = hilt.localPosition;
                Quaternion fixedBadRotation = hilt.localRotation;
                float sourceFrame2Distance = SampleDistance(source, instance, hand, hilt, previousTime);
                float sourceFrame3Distance = SampleDistance(source, instance, hand, hilt, badTime);
                float fixedFrame3Distance = SampleDistance(fixedClip, instance, hand, hilt, badTime);
                float sourceFrame4Distance = SampleDistance(source, instance, hand, hilt, nextTime);
                float fixedFrame4Distance = SampleDistance(fixedClip, instance, hand, hilt, nextTime);
                report.AppendLine("Source frame 2 hilt-hand distance: " + sourceFrame2Distance.ToString("F6") + " m");
                report.AppendLine("Source frame 3 hilt-hand distance: " + sourceFrame3Distance.ToString("F6") + " m");
                report.AppendLine("Fixed frame 3 hilt-hand distance: " + fixedFrame3Distance.ToString("F6") + " m");
                report.AppendLine("Source frame 4 hilt-hand distance: " + sourceFrame4Distance.ToString("F6") + " m");
                report.AppendLine("Fixed frame 4 hilt-hand distance: " + fixedFrame4Distance.ToString("F6") + " m");
                report.AppendLine("Source frame 3 deviation from neighbor midpoint: " +
                                  Vector3.Distance(sourceBadPosition, targetPosition).ToString("F6") + " m, " +
                                  Quaternion.Angle(sourceBadRotation, targetRotation).ToString("F3") + " degrees");
                report.AppendLine("Fixed frame 3 deviation from neighbor midpoint: " +
                                  Vector3.Distance(fixedBadPosition, targetPosition).ToString("F6") + " m, " +
                                  Quaternion.Angle(fixedBadRotation, targetRotation).ToString("F3") + " degrees");
                if (Vector3.Distance(fixedBadPosition, targetPosition) > 0.0001f ||
                    Quaternion.Angle(fixedBadRotation, targetRotation) > 0.05f ||
                    Mathf.Abs(fixedFrame4Distance - sourceFrame4Distance) > 0.0001f)
                    throw new InvalidOperationException("Corrected grip validation failed.");
                report.AppendLine("PASS: frame 3 is interpolated from correct frames 2 and 4; adjacent frames are unchanged.");
                report.AppendLine("Controller and Swordman_Attack_01 ability reference the fixed project clip.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        finally { File.WriteAllText(Report, report.ToString()); }
    }

    static void SetKey(AnimationClip clip, string property, float time, float value)
    {
        var binding = EditorCurveBinding.FloatCurve(HiltPath, typeof(Transform), property);
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding) ??
                               throw new InvalidOperationException("Missing curve: " + property);
        int index = Enumerable.Range(0, curve.length).OrderBy(i => Mathf.Abs(curve.keys[i].time - time)).First();
        if (Mathf.Abs(curve.keys[index].time - time) > 0.001f) throw new InvalidOperationException("Missing frame 3 key: " + property);
        Keyframe key = curve.keys[index];
        key.value = value;
        curve.MoveKey(index, key);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    static float SampleDistance(AnimationClip clip, GameObject instance, Transform hand, Transform hilt, float time)
    {
        clip.SampleAnimation(instance, time);
        return Vector3.Distance(hand.position, hilt.position);
    }

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
}
