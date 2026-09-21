using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
public static class RepairProbe {
 [Serializable] public class Result { public string asset; public float fps,length,maxGripDrift; public int hiltKeyCount; public bool resample; }
 [Serializable] public class Report {public Result[] results;}
 public static void Run() {
  try {
   AssetDatabase.Refresh();
   var results=new List<Result>();
   foreach(var path in new[]{"Assets/original.fbx","Assets/fixed.fbx"}) {
    var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
    var hilt=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bn_katana_hilt");
    var hand=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip001 R Hand");
    clip.SampleAnimation(root,5f/30f);var reference=Quaternion.Inverse(hand.rotation)*hilt.rotation;float max=0;
    for(int i=800;i<=960;i++) {clip.SampleAnimation(root,i/4800f);max=Mathf.Max(max,Quaternion.Angle(reference,Quaternion.Inverse(hand.rotation)*hilt.rotation));}
    var keys=AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.EndsWith("Bn_katana_hilt")).Select(b=>AnimationUtility.GetEditorCurve(clip,b).length).DefaultIfEmpty(0).Max();
    results.Add(new Result{asset=path,fps=clip.frameRate,length=clip.length,maxGripDrift=max,hiltKeyCount=keys,resample=((ModelImporter)AssetImporter.GetAtPath(path)).resampleCurves});
    UnityEngine.Object.DestroyImmediate(root);
   }
   File.WriteAllText("../unity_validation.json",JsonUtility.ToJson(new Report{results=results.ToArray()},true));
  } catch(Exception e) { File.WriteAllText("../unity_validation.error",e.ToString());throw; }
 }
}
