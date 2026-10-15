using CombatEditor;
using UnityEditor;
using UnityEngine;

internal static class CodexEvadeAssetProbe
{
    [InitializeOnLoadMethod]
    private static void OnReload()
    {
        Debug.Log("CODEX_EVADE_PROBE reload");
        EditorApplication.delayCall += InspectAndRepair;
    }

    private static void InspectAndRepair()
    {
        const string path = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Swordman/Unagi_Evade_Front.asset";
        var ability = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(path);
        var main = AssetDatabase.LoadMainAssetAtPath(path);
        Debug.Log($"CODEX_EVADE_PROBE ability={(ability ? ability.name : "NULL")} main={(main ? main.name : "NULL")} clip={(ability && ability.Clip ? ability.Clip.name : "NULL")}");
        if (!ability || main != ability) return;
        foreach (var controller in Object.FindObjectsOfType<CombatController>(true))
        {
            if (controller.gameObject.scene.name != "Main_Nodachi") continue;
            foreach (var group in controller.CombatDatas)
            {
                if (group.Label != "Swordman Evade" || group.CombatObjs == null || group.CombatObjs.Count != 1 || group.CombatObjs[0] != null)
                    continue;
                Undo.RecordObject(controller, "Restore Evade Ability Reference");
                group.CombatObjs[0] = ability;
                EditorUtility.SetDirty(controller);
                Debug.Log("CODEX_EVADE_PROBE rebound missing Swordman Evade reference");
            }
        }
    }
}
