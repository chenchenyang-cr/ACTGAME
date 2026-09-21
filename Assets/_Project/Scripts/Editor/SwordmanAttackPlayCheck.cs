using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
                StartScenario();
            }
            if (Time.realtimeSinceStartup - started > 12f) throw new Exception("Timed out in scenario " + scenario + ": " + string.Join(" -> ", observed) + "; state=" + machine.CurrentState?.GetType().Name + "; enabled=" + machine.enabled + "; buffer=" + buffer.Count);
            string current = machine.Combat.CurrentAbility == null ? "Idle" : machine.Combat.CurrentAbility.name;
            if (current != previous)
            {
                previous = current;
                if (current != "Idle" || observed.Count > 0) observed.Add(current);
            }
            if (queued < scenario && current == $"Swordman_Attack_{queued:00}" &&
                machine.ActionAnimator.TryGetTrackedActionTime(out float time) && time >= 0.65f && time < 0.9f)
            {
                buffer.AddInput(PlayerActionCommand.LightAttack);
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
            if (scenario <= 3) StartScenario();
            else Finish();
        }
        catch (Exception e) { results.Add("FAIL " + e); Finish(); }
    }

    static void StartScenario()
    {
        observed.Clear(); previous = null; queued = 1;
        started = Time.realtimeSinceStartup;
        buffer.Clear(); buffer.AddInput(PlayerActionCommand.LightAttack);
    }

    static void Finish()
    {
        File.WriteAllLines(Report, results);
        SessionState.SetBool(Key, false);
        EditorApplication.ExitPlaymode();
    }
}
