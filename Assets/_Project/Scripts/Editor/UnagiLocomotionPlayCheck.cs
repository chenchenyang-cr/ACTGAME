using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Runs an explicit, temporary play-mode regression and returns to edit mode.
[InitializeOnLoad]
public static class UnagiLocomotionPlayCheck
{
    private const string Key = "UnagiLocomotionPlayCheck.Running";
    private static PlayerStateMachine machine;
    private static PlayerMovement movement;
    private static Transform skeleton;
    private static Transform visual;
    private static Vector3 origin;
    private static Vector3 stopPosition;
    private static float startTime;
    private static float maxDrift;
    private static float maxVisualOffset;
    private static float maxReturn;
    private static bool stopped;

    static UnagiLocomotionPlayCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        };
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key, false))
        {
            if (!File.Exists("Library/UnagiPlayCheck.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete("Library/UnagiPlayCheck.request");
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
                machine.enabled = false;
                movement = machine.GetComponent<PlayerMovement>();
                var serialized = new SerializedObject(movement);
                serialized.FindProperty("cameraTransform").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var controller = machine.GetComponent<CharacterController>();
                controller.enabled = false;
                machine.transform.SetPositionAndRotation(new Vector3(0, 0.3f, -20), Quaternion.identity);
                controller.enabled = true;
                visual = machine.GetComponentInChildren<Animator>().transform;
                skeleton = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Bip001");
                origin = machine.transform.position;
                startTime = Time.time;
            }
            float elapsed = Time.time - startTime;
            bool moving = elapsed < 5f;
            if (!moving && !stopped)
            {
                stopped = true;
                stopPosition = machine.transform.position;
            }
            movement.Tick(moving ? Vector2.up : Vector2.zero, moving);
            Vector3 local = machine.transform.InverseTransformPoint(skeleton.position);
            maxDrift = Mathf.Max(maxDrift, new Vector2(local.x, local.z).magnitude);
            maxVisualOffset = Mathf.Max(maxVisualOffset, visual.localPosition.magnitude);
            if (stopped) maxReturn = Mathf.Max(maxReturn, stopPosition.z - machine.transform.position.z);
            if (elapsed < 9.5f) return;
            float travel = machine.transform.position.z - origin.z;
            var animator = visual.GetComponent<Animator>();
            bool idle = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle");
            bool pass = travel > 20 && maxDrift < 0.65f && maxVisualOffset < 0.01f && maxReturn < 0.3f && idle;
            File.WriteAllText("Library/UnagiPlayCheckReport.txt",
                "Travel=" + travel + "m\nMaxSkeletonPlanarOffset=" + maxDrift +
                "m\nMaxVisualLocalOffset=" + maxVisualOffset + "m\nMaxStopBackwardSnap=" + maxReturn +
                "m\nReturnedToIdle=" + idle + "\n" + (pass ? "PASS" : "FAIL"));
            Finish();
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/UnagiPlayCheckReport.txt", exception.ToString());
            Finish();
        }
    }

    private static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.ExitPlaymode();
    }
}
