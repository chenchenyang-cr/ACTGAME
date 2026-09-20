using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Explicit request only; exercises the real input buffer, Animator and gameplay windows.
[InitializeOnLoad]
public static class SwordmanAttackPlayCheck
{
    const string Key = "SwordmanAttackPlayCheck.Running";
    const string Report = "Library/SwordmanAttackPlayCheckReport.txt";
    static PlayerStateMachine machine;
    static PlayerInputBuffer buffer;
    static int scenario = 1;
    static int queued;
    static float started;
    static string previous;
    static readonly List<string> observed = new List<string>();
    static readonly List<string> results = new List<string>();
    static Mouse testMouse;
    static Keyboard testKeyboard;
    static bool movementQueued;
    static InputSettings originalInputSettings;

    static SwordmanAttackPlayCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        };
    }

    static void Tick()
    {
        if (File.Exists("Library/SwordmanAttackPlayCheck.stop"))
        {
            File.Delete("Library/SwordmanAttackPlayCheck.stop");
            SessionState.SetBool(Key, false);
            EditorApplication.isPaused = false;
            EditorApplication.ExitPlaymode();
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (!File.Exists("Library/SwordmanAttackPlayCheck.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete("Library/SwordmanAttackPlayCheck.request");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            Application.runInBackground = true;
            if (machine == null)
            {
                machine = GameObject.Find("Player_Swordman").GetComponent<PlayerStateMachine>();
                if (machine.ActionAnimator == null) { machine = null; return; }
                buffer = machine.GetComponent<PlayerInputBuffer>();
                if (!machine.GetComponent<PlayerCommandInputAdapter>().isActiveAndEnabled || !machine.Combat.isActiveAndEnabled)
                    throw new Exception("Swordman input/combat adapter is disabled.");
                originalInputSettings = InputSystem.settings;
                InputSystem.settings = UnityEngine.Object.Instantiate(originalInputSettings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testMouse = InputSystem.AddDevice<Mouse>();
                testKeyboard = InputSystem.AddDevice<Keyboard>();
                // Isolate this temporary play-mode check from real mouse/gamepad input.
                var reader = machine.GetComponent<PlayerInputReader>();
                var actions = (PlayerAction)typeof(PlayerInputReader).GetField("_gameInput",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(reader);
                actions.asset.devices = new InputDevice[] { testMouse, testKeyboard };
                StartScenario();
            }
            if (Time.realtimeSinceStartup - started > 12f) throw new Exception("Timed out in scenario " + scenario + ": " + string.Join(" -> ", observed) + "; state=" + machine.CurrentState?.GetType().Name + "; enabled=" + machine.enabled + "; mouse=" + testMouse.enabled + "; buffer=" + buffer.Count);
            string current = machine.Combat.CurrentAbility == null ? "Idle" : machine.Combat.CurrentAbility.name;
            if (current != previous)
            {
                previous = current;
                if (current != "Idle" || observed.Count > 0) observed.Add(current);
            }
            if (scenario > 3)
            {
                string cancelAbility = scenario == 4 ? "Swordman_Attack_01" : "Swordman_Attack_01_End";
                float cancelTime = scenario == 4 ? 0.8f : 0.6f;
                if (!movementQueued && current == cancelAbility &&
                    machine.ActionAnimator.TryGetTrackedActionTime(out float cancelProgress) && cancelProgress >= cancelTime)
                {
                    movementQueued = true;
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                }
                if (movementQueued && machine.CurrentState == machine.LocomotionState)
                {
                    var animator = machine.GetComponentInChildren<Animator>();
                    var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                    if (!state.IsName("Base Layer.NormalLocomotion.Loop")) return;
                    results.Add("PASS " + cancelAbility + " + W -> NormalLocomotion.Loop");
                    scenario++;
                    if (scenario <= 5) StartScenario(); else Finish();
                }
                return;
            }
            if (queued < scenario && current == $"Swordman_Attack_{queued:00}" &&
                machine.ActionAnimator.TryGetTrackedActionTime(out float time) && time >= 0.65f && time < 0.9f)
            {
                ClickAttack();
                queued++;
            }
            if (observed.Count == 0 || current != "Idle") return;
            var expected = new List<string>();
            for (int i = 1; i <= scenario; i++) expected.Add($"Swordman_Attack_{i:00}");
            expected.Add($"Swordman_Attack_{scenario:00}_End");
            expected.Add("Idle");
            if (string.Join(",", expected) != string.Join(",", observed))
                throw new Exception("Unexpected sequence: " + string.Join(" -> ", observed));
            if (machine.CurrentState != machine.IdleState) throw new Exception("Did not return to IdleState.");
            results.Add("PASS " + string.Join(" -> ", observed));
            scenario++;
            if (scenario <= 5) StartScenario();
            else Finish();
        }
        catch (Exception e) { results.Add("FAIL " + e); Finish(); }
    }

    static void StartScenario()
    {
        observed.Clear(); previous = null; queued = 1;
        movementQueued = false;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        started = Time.realtimeSinceStartup;
        buffer.Clear(); ClickAttack();
    }

    static void ClickAttack()
    {
        InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
        InputSystem.QueueStateEvent(testMouse, new MouseState());
    }

    static void Finish()
    {
        if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
        if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
        if (originalInputSettings != null)
        {
            var temporarySettings = InputSystem.settings;
            InputSystem.settings = originalInputSettings;
            UnityEngine.Object.DestroyImmediate(temporarySettings);
        }
        File.WriteAllLines(Report, results);
        SessionState.SetBool(Key, false);
        EditorApplication.ExitPlaymode();
    }
}
