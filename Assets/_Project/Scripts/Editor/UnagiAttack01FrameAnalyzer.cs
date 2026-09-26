using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UnagiAttack01FrameAnalyzer
{
    const string ModelPath = "Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/02_Idle/Unagi_Idle.fbx";
    const string ClipPath = "Assets/ThirdParty/UnagiAnimationPack/FBX/MAX_Variant/Unagi/01_Attack/Unagi_Attack_01.fbx";
    const string Request = "Library/UnagiAttack01FrameAnalyzer.request";
    const string Report = "Library/UnagiAttack01FrameAnalyzer.txt";

    static UnagiAttack01FrameAnalyzer() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Analyze();
    }

    [MenuItem("Tools/Unagi/Analyze Attack 01 Weapon Frame")]
    public static void Analyze()
    {
        var output = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            var animator = instance.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            var clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));

            var transforms = instance.GetComponentsInChildren<Transform>(true);
            output.AppendLine("Transforms containing Hand/Weapon/Sword:");
            foreach (var t in transforms.Where(t => Contains(t.name, "hand") || Contains(t.name, "weapon") || Contains(t.name, "sword")))
                output.AppendLine(GetPath(t, instance.transform));

            output.AppendLine("Renderers:");
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                output.AppendLine($"{GetPath(r.transform, instance.transform)} | {r.GetType().Name} | materials={string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))}");

            var rightHand = transforms.FirstOrDefault(t => Contains(t.name, "r hand") || Contains(t.name, "r_hand") || Contains(t.name, "rhand") || Contains(t.name, "hand_r"));
            var weaponRenderer = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => Contains(r.name, "weapon") || r.sharedMaterials.Any(m => m != null && Contains(m.name, "weapon")));
            if (rightHand == null || weaponRenderer == null)
                throw new InvalidOperationException($"Could not identify hand/weapon. hand={rightHand}, weapon={weaponRenderer}");

            output.AppendLine($"Selected hand: {GetPath(rightHand, instance.transform)}");
            output.AppendLine($"Selected weapon: {GetPath(weaponRenderer.transform, instance.transform)}");
            output.AppendLine("Weapon bones: " + string.Join(", ", weaponRenderer.bones.Select(b => b != null ? GetPath(b, instance.transform) : "null")));
            var hilt = transforms.Single(t => t.name == "Bn_katana_hilt");
            output.AppendLine($"Selected hilt: {GetPath(hilt, instance.transform)}");
            output.AppendLine("frame,time,handX,handY,handZ,hiltX,hiltY,hiltZ,hiltHandDistance,hiltDelta,weaponCenterDistance");

            var mesh = new Mesh();
            float previousHiltDistance = 0f;
            int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = (float)frame / clip.frameRate;
                clip.SampleAnimation(instance, time);
                weaponRenderer.BakeMesh(mesh);
                Vector3 center = Vector3.zero;
                foreach (Vector3 vertex in mesh.vertices) center += weaponRenderer.transform.TransformPoint(vertex);
                center /= mesh.vertexCount;
                float distance = Vector3.Distance(rightHand.position, center);
                float hiltDistance = Vector3.Distance(rightHand.position, hilt.position);
                output.AppendLine(string.Join(",", frame, time.ToString("F6"),
                    rightHand.position.x.ToString("F6"), rightHand.position.y.ToString("F6"), rightHand.position.z.ToString("F6"),
                    hilt.position.x.ToString("F6"), hilt.position.y.ToString("F6"), hilt.position.z.ToString("F6"), hiltDistance.ToString("F6"),
                    frame == 0 ? "0" : (hiltDistance - previousHiltDistance).ToString("F6"), distance.ToString("F6")));
                previousHiltDistance = hiltDistance;
            }
            UnityEngine.Object.DestroyImmediate(mesh);

            output.AppendLine("Animated transform curves near hand/weapon:");
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b =>
                         Contains(b.path, "hand") || Contains(b.path, "weapon") || Contains(b.path, "sword") ||
                         Contains(b.path, "katana") || Contains(b.path, "scabbard")))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                output.AppendLine($"{binding.path} | {binding.propertyName} | keys={curve.length} | " +
                                  string.Join(";", curve.keys.Select(k => $"{k.time:F6}:{k.value:F6}")));
            }

            output.AppendLine("Grip comparison: frame,localX,localY,localZ,positionDelta,rotationDeltaDegrees");
            Vector3 previousGripPosition = default;
            Quaternion previousGripRotation = Quaternion.identity;
            for (int frame = 0; frame <= frames; frame++)
            {
                clip.SampleAnimation(instance, (float)frame / clip.frameRate);
                Matrix4x4 grip = rightHand.worldToLocalMatrix * hilt.localToWorldMatrix;
                Vector3 position = grip.GetColumn(3);
                Quaternion rotation = grip.rotation;
                output.AppendLine($"{frame},{position.x:F6},{position.y:F6},{position.z:F6}," +
                                  $"{(frame == 0 ? 0f : Vector3.Distance(previousGripPosition, position)):F6}," +
                                  $"{(frame == 0 ? 0f : Quaternion.Angle(previousGripRotation, rotation)):F3}");
                previousGripPosition = position;
                previousGripRotation = rotation;
            }
        }
        catch (Exception e) { output.AppendLine("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllText(Report, output.ToString());
        }
    }

    static bool Contains(string value, string part) => value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    static string GetPath(Transform t, Transform root)
    {
        string path = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
