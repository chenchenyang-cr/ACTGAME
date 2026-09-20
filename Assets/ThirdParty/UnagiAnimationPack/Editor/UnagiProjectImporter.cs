using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Only acts on this package and an explicit local request marker.
[InitializeOnLoad]
public static class UnagiProjectImporter
{
    private const string Root = "Assets/ThirdParty/UnagiAnimationPack";
    private const string Request = "Library/UnagiImport.request";
    private const string ReportPath = "Library/UnagiImportReport.json";
    private static bool running;
    [Serializable] public class Entry { public string file; public string error; public int meshes; public int clips; public int blendShapes; public bool valid; }
    [Serializable] public class Report { public string project; public int files; public int animationFiles; public int animationClips; public int failures; public Entry[] entries; }

    static UnagiProjectImporter() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Run();
    }
    [MenuItem("Tools/Unagi/Configure and Validate Imported Animations")]
    public static void Run()
    {
        if (running) return;
        running = true;
        var entries = new List<Entry>();
        try
        {
            var paths = Directory.GetFiles(Root + "/FBX", "*.fbx", SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray();
            if(paths.Length != 149) throw new InvalidOperationException("Expected 149 FBX files; found " + paths.Length);
            foreach(var p in paths)
            {
                var entry = new Entry {file = p};
                try
                {
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(p) as ModelImporter;
                    if(importer == null) throw new InvalidOperationException("ModelImporter unavailable");
                    var modelOnly = p.Contains("/00_Model/");
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.importAnimation = !modelOnly;
                    importer.importBlendShapes = true;
                    importer.animationCompression = ModelImporterAnimationCompression.Off;
                    if(!modelOnly)
                    {
                        var clips = importer.defaultClipAnimations;
                        var name = Path.GetFileNameWithoutExtension(p);
                        foreach(var clip in clips)
                        {
                            clip.name = name;
                            clip.loopTime = name == "Unagi_Idle" || name == "Unagi_Run" || name == "Unagi_Ghost_Idle";
                        }
                        importer.clipAnimations = clips;
                    }
                    importer.SaveAndReimport();
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var animations = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                    entry.meshes = renderers.Length;
                    entry.clips = animations.Length;
                    entry.blendShapes = renderers.Sum(r=>r.sharedMesh != null ? r.sharedMesh.blendShapeCount : 0);
                    entry.valid = entry.meshes > 0 && (modelOnly || (entry.clips == 1 && animations[0].length > 0));
                    if(!entry.valid) entry.error = "Missing skinned mesh or animation clip";
                }
                catch(Exception e) { entry.error=e.ToString(); entry.valid=false; }
                entries.Add(entry);
                WriteReport(entries);
            }
            AssetDatabase.SaveAssets();
            WriteReport(entries);
            if(File.Exists(Request)) File.Move(Request, Request + ".completed");
            Debug.Log("Unagi import completed: " + entries.Count + " FBX, failures=" + entries.Count(e=>!e.valid));
        }
        catch(Exception e)
        {
            entries.Add(new Entry {file=Root,error=e.ToString(),valid=false});
            WriteReport(entries);
            if(File.Exists(Request)) File.Move(Request,Request+".failed");
            Debug.LogException(e);
        }
        finally { running=false; }
    }
    private static void WriteReport(List<Entry> entries)
    {
        var report = new Report {project=Application.dataPath,files=entries.Count,animationFiles=entries.Count(e=>e.valid && e.clips>0),animationClips=entries.Sum(e=>e.clips),failures=entries.Count(e=>!e.valid),entries=entries.ToArray()};
        File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
    }
}
