using UnityEditor;
using UnityEngine;

public class AssignEllenZZZMaterials : AssetPostprocessor
{
    const string ModelPath = "Assets/Ellen/00_标准角色模型/Ellen_Standard.fbx";
    const string MatDir = "Assets/Ellen/00_标准角色模型/Materials/";

    void OnPostprocessModel(GameObject g)
    {
        if (!assetPath.Replace('\\', '/').EndsWith("Ellen_Standard.fbx"))
            return;
        Apply(g);
    }

    [MenuItem("Ellen/Apply ZZZ Toon Materials")]
    static void MenuApply()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (go == null)
        {
            Debug.LogError("Ellen_Standard.fbx not found.");
            return;
        }
        Apply(go);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        Debug.Log("Assigned Ellen/ZZZToon materials on Ellen_Standard.");
    }

    static void Apply(GameObject root)
    {
        var body1 = Load("Ellen_Body_Map1_PBR.mat");
        var body2 = Load("Ellen_Body_Map2_PBR.mat");
        var face = Load("Ellen_Face_PBR.mat");
        var weapon = Load("Ellen_Weapon_PBR.mat");
        var hairShadow = Load("Ellen_HairShadow_Helper_Transparent.mat");
        if (body1 == null || body2 == null || face == null || weapon == null)
            return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var name = mats[i] != null ? mats[i].name : renderer.name;
                Material next = null;
                if (name.Contains("Body_Map1") || renderer.name.Contains("Body_1"))
                    next = body1;
                else if (name.Contains("Body_Map2") || renderer.name.Contains("Body_2"))
                    next = body2;
                else if (name.Contains("Face") || renderer.name.Contains("Face"))
                    next = face;
                else if (name.Contains("Weapon"))
                    next = weapon;
                else if (name.Contains("HairShadow"))
                    next = hairShadow;
                if (next != null && mats[i] != next)
                {
                    mats[i] = next;
                    changed = true;
                }
            }
            if (changed)
                renderer.sharedMaterials = mats;
        }
    }

    static Material Load(string file) =>
        AssetDatabase.LoadAssetAtPath<Material>(MatDir + file);
}
