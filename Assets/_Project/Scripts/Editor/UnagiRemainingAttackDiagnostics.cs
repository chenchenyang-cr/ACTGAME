using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiRemainingAttackDiagnostics
{
    const string Root = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/";
    const string ModelPath = Root + "02_Idle/Unagi_Idle.fbx";
    const string Request = "Library/UnagiBatchImport/remaining_diagnose.request";
    const string Report = "Library/UnagiBatchImport/remaining_diagnose.csv";
    static readonly string[] Names = {
        "Unagi_Attack_04_02", "Unagi_Attack_05", "Unagi_Attack_05_02",
        "Unagi_Attack_06", "Unagi_Attack_Branch_01_Attack_01",
        "Unagi_Attack_Branch_02_Attack_01", "Unagi_Attack_Rush"
    };

    static UnagiRemainingAttackDiagnostics() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Diagnose();
    }

    [MenuItem("Tools/Unagi/Diagnose Remaining Attacks")]
    public static void Diagnose()
    {
        var output = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                var all = instance.GetComponentsInChildren<Transform>(true);
                var hand = all.Single(t => t.name == "Bip001 R Hand");
                var hilt = all.Single(t => t.name == "Bn_katana_hilt");
                var fingers = all.Where(t => t.name.Contains("R Finger"))
                    .OrderBy(t => t.name).ToArray();
                output.AppendLine("clip,frame,gripDistance,gripX,gripY,gripZ,gripAngle,handX,handY,handZ,hiltX,hiltY,hiltZ,maxFingerStep,maxFingerBone");
                foreach (var name in Names)
                {
                    try
                    {
                        var path = Root + "01_Attack/" + name + ".fbx";
                        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                            .Single(c => !c.name.StartsWith("__preview__"));
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        int hiltCurves = bindings.Count(b => b.path == "Bip001/Bn_katana_hilt");
                        output.AppendLine($"info,{name},fps,{clip.frameRate},lastFrame,{clip.length * clip.frameRate},bindings,{bindings.Length},hiltCurves,{hiltCurves},guid,{AssetDatabase.AssetPathToGUID(path)}");
                        Quaternion[] previous = null;
                        int steps = Mathf.RoundToInt(clip.length * clip.frameRate * 4f);
                        for (int step = 0; step <= steps; step++)
                        {
                            float frame = step / 4f;
                            clip.SampleAnimation(instance, frame / clip.frameRate);
                            var grip = hand.InverseTransformPoint(hilt.position);
                            var rotations = fingers.Select(f => f.localRotation).ToArray();
                            float maxFinger = 0f;
                            string maxBone = "";
                            if (previous != null)
                                for (int i = 0; i < fingers.Length; i++)
                                {
                                    float angle = Quaternion.Angle(previous[i], rotations[i]);
                                    if (angle > maxFinger) { maxFinger = angle; maxBone = fingers[i].name; }
                                }
                            output.AppendLine($"{name},{frame:F2},{grip.magnitude:F6},{grip.x:F6},{grip.y:F6},{grip.z:F6}," +
                                $"{Quaternion.Angle(hand.rotation, hilt.rotation):F3}," +
                                $"{hand.position.x:F6},{hand.position.y:F6},{hand.position.z:F6}," +
                                $"{hilt.position.x:F6},{hilt.position.y:F6},{hilt.position.z:F6}," +
                                $"{maxFinger:F3},{maxBone}");
                            previous = rotations;
                        }
                    }
                    catch (Exception e) { output.AppendLine("FAIL," + name + "," + e); Debug.LogException(e); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        catch (Exception e) { output.AppendLine("FAIL,setup," + e); Debug.LogException(e); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllText(Report, output.ToString());
        }
    }
}
