using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicit request only. All input overrides live in Play Mode and are discarded on exit.
[InitializeOnLoad]
public static class UnagiTurnPlayValidation
{
    private const string Request = "Library/UnagiTurnPlay.request";
    private const string Key = "UnagiTurnPlay.Active";
    private static readonly float[] Targets = { 150, 160, 180, -160, -150, 160, 180 };
    private static PlayerMovement movement;
    private static Animator animator;
    private static Transform actor;
    private static int test;
    private static float started;
    private static int lastFrame = -1;
    private static bool sawTurn;
    private static bool sawExit;
    private static float targetError;
    private static float handoffSpeed;
    private static float releaseTime;
    private static bool releasedDuringTurn;
    private static bool steeredDuringRecovery;
    private static Quaternion previousRotation;
    private static float previousTime;
    private static bool previousActive;
    private static readonly System.Collections.Generic.List<string> Lines = new();

    static UnagiTurnPlayValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false);
                File.WriteAllText("Library/UnagiTurnPlayReport.txt", "INTERRUPTED before validation finished.");
            }
        };
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            Application.runInBackground = true;
            if (movement == null)
            {
                var player = GameObject.Find("Player_Swordman");
                player.GetComponent<PlayerStateMachine>().enabled = false;
                player.GetComponent<PlayerInputReader>().enabled = false;
                movement = player.GetComponent<PlayerMovement>();
                actor = player.transform;
                animator = player.GetComponentInChildren<Animator>();
                typeof(PlayerMovement).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(movement, null);
                BeginCase();
            }
            float elapsed = Time.time - started;
            bool active = (bool)typeof(PlayerMovement).GetField("matchedTurnActive",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(movement);
            float dt = Time.time - previousTime;
            if (active)
            {
                sawTurn = true;
                if (elapsed > 0.55f)
                    targetError = Mathf.Max(targetError, Quaternion.Angle(actor.rotation,
                        Quaternion.Euler(0, Targets[test], 0)));
            }
            else if (sawTurn)
            {
                if (!sawExit)
                {
                    releaseTime = elapsed;
                    releasedDuringTurn = animator.GetCurrentAnimatorStateInfo(0).IsTag("LocomotionTurn180");
                    targetError = Quaternion.Angle(actor.rotation, Quaternion.Euler(0, Targets[test], 0));
                }
                sawExit = true;
                if (dt > 0 && !previousActive)
                    handoffSpeed = Mathf.Max(handoffSpeed, Quaternion.Angle(previousRotation, actor.rotation) / dt);
                if (test == 5 && elapsed < 0.7f &&
                    animator.GetCurrentAnimatorStateInfo(0).IsTag("LocomotionTurn180") &&
                    Quaternion.Angle(actor.rotation, Quaternion.Euler(0, Targets[test], 0)) > 5f)
                    steeredDuringRecovery = true;
            }
            if (previousActive && !active && dt > 0 &&
                Quaternion.Angle(previousRotation, actor.rotation) > 720 * dt + 0.2f)
                throw new Exception("Abrupt rotation on exit.");
            previousRotation = actor.rotation;
            previousTime = Time.time;
            previousActive = active;
            float inputYaw = test == 5 && elapsed > 0.2f ? 90 : Targets[test];
            bool hasInput = !(test == 6 && elapsed > 0.2f);
            Vector3 direction = Quaternion.Euler(0, inputYaw, 0) * Vector3.forward;
            movement.Tick(hasInput ? new Vector2(direction.x, direction.z) : Vector2.zero, hasInput);
            if (elapsed < 1.7f) return;
            if (!sawTurn || !sawExit || targetError > 1f || handoffSpeed > 740f ||
                !releasedDuringTurn || releaseTime > 0.6f || (test == 5 && !steeredDuringRecovery))
                throw new Exception("Turn validation failed.");
            Lines.Add("case=" + test + ", target=" + Targets[test] + ", lockedError=" +
                targetError + ", handoffSpeed=" + handoffSpeed + ", releaseTime=" + releaseTime +
                ", releasedDuringTurn=" + releasedDuringTurn + ", steeredDuringRecovery=" + steeredDuringRecovery);
            test++;
            if (test < Targets.Length) BeginCase();
            else Finish("PASS: real root-motion pipeline, changed input, release, consecutive turns.");
        }
        catch (Exception exception) { Finish("FAIL: case=" + test + ", error=" + targetError +
            ", handoffSpeed=" + handoffSpeed + ", sawTurn=" + sawTurn + ", sawExit=" + sawExit + "\n" + exception); }
    }

    private static void BeginCase()
    {
        var controller = movement.GetComponent<CharacterController>();
        controller.enabled = false;
        actor.position = new Vector3(0, 0.3f, 0);
        // Clear the previous case's input before Animator.Update(0) can invoke
        // the movement rotation callback during the new idle pose evaluation.
        movement.Tick(Vector2.zero, false, false);
        movement.FaceWorldDirectionImmediately(Vector3.forward);
        controller.enabled = true;
        animator.SetBool("IsMoving", false);
        animator.Play("Base Layer.Idle", 0, 0);
        animator.Update(0);
        var receiver = animator.GetComponent<CombatEditor.RootMotionReceiver>();
        receiver.ConsumeRootMotion();
        receiver.ConsumeRootRotation();
        started = Time.time;
        previousTime = Time.time;
        previousRotation = actor.rotation;
        previousActive = sawTurn = sawExit = false;
        releasedDuringTurn = steeredDuringRecovery = false;
        releaseTime = 0;
        targetError = handoffSpeed = 0;
    }

    private static void Finish(string result)
    {
        Lines.Add(result);
        File.WriteAllLines("Library/UnagiTurnPlayReport.txt", Lines);
        SessionState.SetBool(Key, false);
        EditorApplication.ExitPlaymode();
    }
}
