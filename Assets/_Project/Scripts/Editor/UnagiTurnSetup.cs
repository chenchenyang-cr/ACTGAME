using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class UnagiTurnSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Main_Nodachi.unity";
    private const string ClipPath = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/04_Walk/Unagi_Walk.fbx";
    private const string Request = "Library/UnagiTurnSetup.request";

    static UnagiTurnSetup() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception error)
        {
            File.WriteAllText("Library/UnagiTurnSetupReport.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Unagi/Configure Target Matched Turning")]
    public static void Configure()
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>()
            .Single(c => !c.name.StartsWith("__preview__"));
        Scene preview = EditorSceneManager.NewPreviewScene();
        AnimationCurve progress;
        float totalYaw;
        try
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath));
            SceneManager.MoveGameObjectToScene(model, preview);
            var root = model.GetComponentsInChildren<Transform>().Single(t => t.name == "Root");
            int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
            var angles = new float[frames + 1];
            float previousYaw = 0;
            float accumulated = 0;
            for (int i = 0; i <= frames; i++)
            {
                clip.SampleAnimation(model, clip.length * i / frames);
                float yaw = root.localEulerAngles.y;
                if (i > 0) accumulated += Mathf.DeltaAngle(previousYaw, yaw);
                angles[i] = accumulated;
                previousYaw = yaw;
            }
            totalYaw = accumulated;
            if (Mathf.Abs(Mathf.Abs(totalYaw) - 180f) > 5f)
                throw new InvalidOperationException("Unexpected source yaw: " + totalYaw);
            progress = new AnimationCurve();
            for (int i = 0; i <= frames; i++)
                progress.AddKey((float)i / frames, Mathf.Clamp01(angles[i] / totalYaw));
            // Linear tangents retain the measured per-frame motion without curve overshoot.
            for (int i = 0; i < progress.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(progress, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(progress, i, AnimationUtility.TangentMode.Linear);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var player = scene.GetRootGameObjects().Single(o => o.name == "Player_Swordman");
        var movement = player.GetComponent<PlayerMovement>();
        var serialized = new SerializedObject(movement);
        serialized.FindProperty("matchTurn180ToInput").boolValue = true;
        serialized.FindProperty("authoredTurn180Angle").floatValue = totalYaw;
        serialized.FindProperty("turn180YawProgress").animationCurveValue = progress;
        serialized.FindProperty("turn180RecoveryTime").floatValue = 0.04f;
        serialized.FindProperty("rotationSpeed").floatValue = 720f;
        serialized.FindProperty("rotationSmoothTime").floatValue = 0.06f;
        serialized.FindProperty("rotationSpeedByAngle").animationCurveValue = AnimationCurve.Constant(0, 1, 1);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Do not fade out the turn before its authored rotation has completed.
        var controller = player.GetComponentInChildren<Animator>().runtimeAnimatorController;
        var states = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
            .OfType<UnityEditor.Animations.AnimatorState>().Where(s => s.name == "Turn180");
        float completion = progress.keys.First(k => k.value >= 0.999f).time;
        foreach (var state in states)
        foreach (var transition in state.transitions)
        {
            transition.hasExitTime = true;
            transition.exitTime = Mathf.Max(transition.exitTime, completion);
            EditorUtility.SetDirty(transition);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate(player, progress, totalYaw);
    }

    private static void Validate(GameObject player, AnimationCurve progress, float authoredYaw)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        var lines = new System.Collections.Generic.List<string> {
            "Source yaw=" + authoredYaw + ", completion=" + progress.keys.First(k => k.value >= 0.999f).time
        };
        try
        {
            var sourceAnimator = player.GetComponentInChildren<Animator>();
            var actor = new GameObject("Turn Validation");
            SceneManager.MoveGameObjectToScene(actor, preview);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath), actor.transform);
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
            var movement = actor.AddComponent<PlayerMovement>();
            Set(movement, "animator", animator);
            Set(movement, "matchTurn180ToInput", true);
            Set(movement, "authoredTurn180Angle", authoredYaw);
            Set(movement, "turn180YawProgress", progress);
            Set(movement, "turn180RequiresFastMovement", false);
            animator.Rebind();

            foreach (float targetYaw in new[] { 150f, 160f, 180f, -160f, -150f })
            {
                actor.transform.rotation = Quaternion.Euler(0, 37, 0);
                Quaternion expected = Quaternion.Euler(0, 37 + targetYaw, 0);
                Set(movement, "matchedTurnStartRotation", actor.transform.rotation);
                float angle = targetYaw;
                if (angle * authoredYaw < 0) angle += Mathf.Sign(authoredYaw) * 360;
                Set(movement, "matchedTurnAngle", angle);
                Set(movement, "matchedTurnActive", true);
                Set(movement, "matchedTurnStateSeen", false);
                for (int frame = 0; frame <= 120; frame++)
                {
                    animator.Play("Base Layer.NormalLocomotion.Turn180", 0, frame / 120f);
                    animator.Update(0);
                    // New input during the turn must not replace the locked target.
                    Set(movement, "worldMoveDirection", frame < 60 ? Vector3.forward : Vector3.left);
                    actor.transform.rotation *= (Quaternion)Call(movement, "CalculateMatchedTurnRotation");
                }
                float error = Quaternion.Angle(actor.transform.rotation, expected);
                if (error > 0.05f) throw new InvalidOperationException("Turn target error " + error);
                Quaternion before = actor.transform.rotation;
                animator.Play("Base Layer.NormalLocomotion.Loop", 0, 0);
                animator.Update(0);
                actor.transform.rotation *= (Quaternion)Call(movement, "CalculateMatchedTurnRotation");
                float snap = Quaternion.Angle(before, actor.transform.rotation);
                if (snap > 0.01f) throw new InvalidOperationException("Turn exit snapped " + snap);
                lines.Add("Target=" + targetYaw + ", error=" + error + ", exitSnap=" + snap);
            }
            lines.Add("PASS: target angles, nonzero initial heading, changed input, no exit snap.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllLines("Library/UnagiTurnSetupReport.txt", lines);
        }
    }

    private static void Set(object target, string field, object value) =>
        typeof(PlayerMovement).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string method) =>
        typeof(PlayerMovement).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
