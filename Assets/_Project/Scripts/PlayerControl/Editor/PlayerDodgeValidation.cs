#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CombatEditor;

[InitializeOnLoad]
public static class PlayerDodgeValidation
{
    private const string Request = "Temp/DodgeValidation.request";
    private const string Report = "Temp/DodgeValidation.txt";

    static PlayerDodgeValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Validate();
        };
    }

    [MenuItem("Tools/Player/Validate Dodge Playback")]
    public static void Validate()
    {
        var player = new GameObject("Dodge playback validation") { hideFlags = HideFlags.HideAndDontSave };
        var profile = ScriptableObject.CreateInstance<PlayerAnimationProfile>();
        var controller = new AnimatorController();
        var clip = new AnimationClip();
        int checks = 0;
        try
        {
            // Use a generic animated transform so the test needs no model or Avatar.
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 2f));
            controller.AddLayer("Base Layer");
            foreach (string parameter in new[] { "DodgeX", "DodgeY", "CombatWeight" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            var states = controller.layers[0].stateMachine;
            foreach (string name in new[] { "Idle", "DodgeNormal", "DodgeCombat", "DodgeToFastRunNormal", "DodgeToFastRunCombat" })
                states.AddState(name).motion = clip;
            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            var playback = new PlayerActionAnimator(animator, profile, player);

            void CheckState(string name)
            {
                animator.Update(0f);
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName(name) || animator.IsInTransition(0))
                    throw new InvalidOperationException("Expected one active dodge state: " + name);
                checks++;
            }

            foreach (bool combat in new[] { false, true })
            {
                animator.SetFloat("CombatWeight", combat ? 1f : 0f);
                string dodge = combat ? "DodgeCombat" : "DodgeNormal";
                string run = combat ? "DodgeToFastRunCombat" : "DodgeToFastRunNormal";
                foreach (bool moving in new[] { false, true })
                {
                    animator.Play("Idle", 0, 0f);
                    animator.Update(0f);
                    if (!playback.PlayDodge(Vector2.up, moving)) throw new InvalidOperationException("Dodge start failed");
                    animator.Update(0.3f);
                    CheckState(moving ? run : dodge);
                    playback.TryGetDodgeElapsedTime(out float before);
                    playback.SetDodgeContinuation(!moving);
                    CheckState(moving ? dodge : run);
                    playback.TryGetDodgeElapsedTime(out float after);
                    if (Mathf.Abs(after - before) > 0.001f) throw new InvalidOperationException("Dodge prefix replayed");
                    checks++;
                }
            }
            checks += ValidateSteeringRecovery(player, profile, playback);
            File.WriteAllText(Report, $"PASS: {checks} dodge playback checks. {DateTime.Now:O}\n");
            Debug.Log($"Dodge playback validation passed: {checks} checks.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Report, $"FAIL after {checks} checks: {error}\n");
            Debug.LogException(error);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(controller);
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private static int ValidateSteeringRecovery(GameObject player,
        PlayerAnimationProfile profile, PlayerActionAnimator playback)
    {
        const System.Reflection.BindingFlags fields =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var machine = player.AddComponent<PlayerStateMachine>();
        var movement = player.GetComponent<PlayerMovement>();
        var combat = player.AddComponent<PlayerCombatAdapter>();
        var dodge = new DodgeState(machine);
        typeof(PlayerStateMachine).GetField("playerMovement", fields).SetValue(machine, movement);
        typeof(PlayerStateMachine).GetField("combatAdapter", fields).SetValue(machine, combat);
        typeof(PlayerStateMachine).GetField("animationProfile", fields).SetValue(machine, profile);
        typeof(PlayerStateMachine).GetProperty("ActionAnimator").SetValue(machine, playback);
        typeof(PlayerStateMachine).GetProperty("DodgeState").SetValue(machine, dodge);
        typeof(PlayerStateMachine).GetProperty("CurrentState").SetValue(machine, dodge);
        typeof(DodgeState).GetField("animationStarted", fields).SetValue(dodge, true);
        var direction = typeof(PlayerMovement).GetField("worldMoveDirection", fields);
        var rotation = typeof(PlayerMovement).GetField("rotationMode", fields);
        var windows = (System.Collections.IDictionary)typeof(PlayerCombatAdapter)
            .GetField("interruptWindows", fields).GetValue(combat);
        var window = ScriptableObject.CreateInstance<AbilityEventObj_InterruptWindow>();
        try
        {
            dodge.Tick(Vector2.right, true);
            if ((Vector3)direction.GetValue(movement) != Vector3.zero ||
                (PlayerRotationMode)rotation.GetValue(movement) != PlayerRotationMode.Preserve)
                throw new InvalidOperationException("Dodge startup must retain its direction");
            window.AllowMovement = true;
            windows.Add(default(CombatWindowHandle), window);
            dodge.Tick(Vector2.right, true);
            if ((Vector3)direction.GetValue(movement) != Vector3.right ||
                (PlayerRotationMode)rotation.GetValue(movement) != PlayerRotationMode.MovementDirection ||
                machine.CurrentState != dodge)
                throw new InvalidOperationException("Movement window must unlock steering without replacing the dodge animation");
            windows.Clear();
            dodge.Tick(Vector2.left, true);
            if ((Vector3)direction.GetValue(movement) != Vector3.left ||
                (PlayerRotationMode)rotation.GetValue(movement) != PlayerRotationMode.MovementDirection)
                throw new InvalidOperationException("Steering must remain available throughout dodge recovery");
            return 3;
        }
        finally
        {
            windows.Clear();
            UnityEngine.Object.DestroyImmediate(window);
        }
    }
}
#endif
