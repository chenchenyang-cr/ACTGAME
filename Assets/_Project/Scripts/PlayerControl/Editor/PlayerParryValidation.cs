#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CombatEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Runs only on explicit menu invocation or a local validation request file.
[InitializeOnLoad]
public static class PlayerParryValidation
{
    private const string Request = "Temp/ParryValidation.request";
    private const string Report = "Temp/ParryValidation.txt";
    private const string AbilityPath = "Assets/_Project/CombatEditor/ScriptableObjects/Abilities/Nodachi/";

    static PlayerParryValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Validate();
        };
    }

    [MenuItem("Tools/Player/Validate Parry")]
    public static void Validate()
    {
        int count = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            count++;
        }
        GameObject bufferObject = null;
        AbilityEventObj_ParryWindow window = null;
        try
        {
            window = ScriptableObject.CreateInstance<AbilityEventObj_ParryWindow>();
            Vector2 range = new Vector2(0.1f, 0.5f);
            Check(!window.Contains(0.099f, range, Vector3.forward, Vector3.forward), "Before window");
            Check(window.Contains(0.1f, range, Vector3.forward, Vector3.forward), "Inclusive start");
            Check(window.Contains(0.499f, range, Vector3.forward, Vector3.forward), "Inside window");
            Check(!window.Contains(0.5f, range, Vector3.forward, Vector3.forward), "Exclusive end");
            Check(!window.Contains(1.2f, range, Vector3.forward, Vector3.forward), "No loop rearm");
            Check(!window.Contains(0.2f, range, Vector3.forward, Vector3.back), "Back attack rejected");
            Check(!window.Contains(0.2f, range, Vector3.forward, Vector3.right), "Outside facing cone");
            window.IsActive = false;
            Check(!window.Contains(0.2f, range, Vector3.forward, Vector3.forward), "Disabled window");

            bufferObject = new GameObject("Parry buffer validation") { hideFlags = HideFlags.HideAndDontSave };
            var buffer = bufferObject.AddComponent<PlayerInputBuffer>();
            buffer.AddInput(PlayerActionCommand.LightAttack);
            buffer.AddInput(PlayerActionCommand.Parry);
            Check(buffer.TryPeekParry(out _), "Parry bypasses queued attack");
            buffer.ConsumeParry();
            Check(!buffer.TryPeekParry(out _) && buffer.TryPeek(out var input) &&
                input.Command == PlayerActionCommand.LightAttack, "Parry consumption preserves attack");
            buffer.AddInput(PlayerActionCommand.Parry);
            var timestamp = typeof(PlayerInputBuffer).GetField("parryInput",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            timestamp.SetValue(buffer, new BufferedInput(PlayerActionCommand.Parry, Time.time - 1f));
            Check(!buffer.TryPeekParry(out _), "Expired parry is discarded");
            buffer.AddInput(PlayerActionCommand.Parry);
            buffer.Clear();
            Check(!buffer.TryPeekParry(out _) && !buffer.TryPeek(out _), "Clear removes both queues");
            using (var actions = new PlayerAction())
            {
                Check(actions.GamePlay.Parry.interactions == "Press(behavior=0)", "Parry responds only on press");
                Check(actions.GamePlay.Parry.bindings.Any(b => b.path == "<Keyboard>/e"), "E binding");
                Check(actions.GamePlay.Parry.bindings.Any(b => b.path == "<Gamepad>/rightShoulder"), "RB binding");
            }

            var start = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityPath + "Parry_Start.asset");
            var end = AssetDatabase.LoadAssetAtPath<AbilityScriptableObject>(AbilityPath + "Parry_End.asset");
            Check(start != null && start.Clip != null && !start.Clip.isLooping, "Start clip nonlooping");
            Check(start.events.Any(e => e.Obj is AbilityEventObj_ParryWindow), "Start window imported");
            Check(end != null && end.Clip != null && !end.Clip.isLooping &&
                  end.events.Any(e => e.Obj is AbilityEventObj_InterruptWindow), "End recovery window imported");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/_Project/Animations/Controllers/Player_Nodachi.controller");
            foreach (string name in new[] { "Player_Block_Start", "Player_Block_End", "Player_Parry_L", "Player_Parry_R", "Player_Hit_F" })
            {
                var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == name).state;
                Check(state != null && state.motion is AnimationClip clip &&
                      !clip.isLooping, "Animator state " + name);
            }
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Report, $"PASS: {count} parry checks. {DateTime.Now:O}\n");
            Debug.Log($"Parry validation passed: {count} checks.");
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Report, $"FAIL after {count} checks: {ex}\n");
            Debug.LogException(ex);
        }
        finally
        {
            if (bufferObject != null) UnityEngine.Object.DestroyImmediate(bufferObject);
            if (window != null) UnityEngine.Object.DestroyImmediate(window);
        }
    }
}
#endif
