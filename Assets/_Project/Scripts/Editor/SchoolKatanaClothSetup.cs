using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MagicaCloth2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SchoolKatanaClothSetup
{
    const string Folder = "Library/SchoolKatanaIntegration/";
    const string SmokeKey = "SchoolKatana.ClothSmoke";
    static double started = -1;
    static readonly List<string> errors = new();

    static SchoolKatanaClothSetup()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(SmokeKey, false) && (type == LogType.Error || type == LogType.Exception))
                errors.Add(message);
        };
    }

    static void Update()
    {
        string request = Folder + "cloth-request.txt";
        if (File.Exists(request) && File.ReadAllText(request).Trim() == "restore" && EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }
        if (SessionState.GetBool(SmokeKey, false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            if (started < 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started >= 12) FinishSmoke();
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        if (File.Exists(Folder + "cloth-error.txt")) File.Delete(Folder + "cloth-error.txt");
        try
        {
            if (command == "restore") Restore();
            else if (command == "smoke")
            {
                SessionState.SetBool(SmokeKey, true);
                started = -1;
                errors.Clear();
                EditorApplication.EnterPlaymode();
            }
        }
        catch (Exception exception)
        {
            File.WriteAllText(Folder + "cloth-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    static Transform Visual()
    {
        var scene = SceneManager.GetSceneByPath(SchoolKatanaCharacterIntegrator.ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(SchoolKatanaCharacterIntegrator.ScenePath, OpenSceneMode.Additive);
        return scene.GetRootGameObjects().Single(g => g.name == "Player_Nodachi").transform.Find("SchoolKatanaVisual");
    }

    [MenuItem("Tools/Player/Restore School Katana Magica Cloth")]
    public static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Restore cloth in Edit Mode.");
        Transform target = Visual();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SchoolKatanaCharacterIntegrator.PrefabPath);
        if (!source || !target) throw new Exception("Character prefab or scene visual is missing.");
        var sources = source.GetComponentsInChildren<Component>(true)
            .Where(c => c && (c is MagicaCloth || c is ColliderComponent)).ToArray();
        if (sources.OfType<MagicaCloth>().Count() != 3 || sources.Length != 19)
            throw new Exception("Expected the original three cloth groups and sixteen colliders.");
        var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (var t in source.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, source.transform);
            var destination = path.Length == 0 ? target : target.Find(path);
            if (!destination) throw new Exception("Missing destination hierarchy: " + path);
            map[t] = destination;
            map[t.gameObject] = destination.gameObject;
            foreach (var component in t.GetComponents<Component>().Where(c => c && !(c is Transform)))
            {
                var corresponding = destination.GetComponent(component.GetType());
                if (corresponding) map[component] = corresponding;
            }
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore School Katana Magica Cloth");
        try
        {
            foreach (var component in sources)
            {
                var destination = (GameObject)map[component.gameObject];
                var copy = destination.GetComponent(component.GetType()) ?? Undo.AddComponent(destination, component.GetType());
                Undo.RecordObject(copy, "Restore cloth settings");
                map[component] = copy;
            }
            foreach (var component in sources)
            {
                var copy = (Component)map[component];
                EditorUtility.CopySerialized(component, copy);
                var serialized = new SerializedObject(copy);
                var p = serialized.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || !p.objectReferenceValue) continue;
                    if (map.TryGetValue(p.objectReferenceValue, out var replacement)) p.objectReferenceValue = replacement;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);
            }
            var report = new StringBuilder();
            foreach (var cloth in target.GetComponentsInChildren<MagicaCloth>(true))
            {
                if (!cloth.SerializeData.IsValid()) throw new Exception(cloth.name + ": " + cloth.SerializeData.VerificationResult);
                bool meshCloth = cloth.SerializeData.clothType == ClothProcess.ClothType.MeshCloth;
                if (!meshCloth && cloth.SerializeData.rootBones.Any(t => !t || !t.IsChildOf(target))) throw new Exception("Invalid cloth root bone: " + cloth.name);
                if (meshCloth && cloth.SerializeData.sourceRenderers.Any(r => !r || !r.transform.IsChildOf(target))) throw new Exception("Invalid cloth renderer: " + cloth.name);
                report.AppendLine(cloth.name + ": " + cloth.SerializeData.clothType + ", configuration valid");
            }
            foreach (var component in sources.Select(c => (Component)map[c]))
            {
                var p = new SerializedObject(component).GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var referenced = p.objectReferenceValue as Component;
                    var go = p.objectReferenceValue as GameObject;
                    var t = referenced ? referenced.transform : go ? go.transform : null;
                    if (t && !t.IsChildOf(target)) throw new Exception("Cloth reference points outside the character: " + p.propertyPath);
                }
            }
            if (!target.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand).Find("NodachiGripAlignment"))
                throw new Exception("Weapon grip alignment is missing.");
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            if (!EditorSceneManager.SaveScene(target.gameObject.scene)) throw new IOException("Could not save cloth setup.");
            Undo.CollapseUndoOperations(undo);
            report.AppendLine("Restored 3 cloth components and 16 colliders. All local references rebound; weapon alignment retained.");
            File.WriteAllText(Folder + "cloth-restoration.txt", report.ToString());
            Selection.activeGameObject = target.gameObject;
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static void FinishSmoke()
    {
        try
        {
            var target = Visual();
            var cloths = target.GetComponentsInChildren<MagicaCloth>();
            var report = new StringBuilder("12-second Play Mode cloth smoke test\n");
            bool valid = cloths.Length == 3;
            foreach (var cloth in cloths)
            {
                report.AppendLine(cloth.name + ": valid=" + cloth.IsValid() + ", team=" + cloth.Process.TeamId + ", result=" + cloth.Process.Result.GetResultString());
                valid &= cloth.IsValid() && !cloth.Process.Result.IsError();
            }
            report.AppendLine("Runtime errors: " + errors.Count);
            foreach (string error in errors.Distinct()) report.AppendLine(error);
            report.AppendLine(valid && errors.Count == 0 ? "PASS" : "FAIL");
            File.WriteAllText(Folder + "cloth-playmode.txt", report.ToString());
        }
        catch (Exception e) { File.WriteAllText(Folder + "cloth-error.txt", e.ToString()); }
        finally
        {
            SessionState.SetBool(SmokeKey, false);
            EditorApplication.ExitPlaymode();
            started = -1;
        }
    }
}
