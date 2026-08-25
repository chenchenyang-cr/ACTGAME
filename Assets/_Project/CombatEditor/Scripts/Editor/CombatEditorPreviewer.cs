using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

 namespace CombatEditor
{	
	public class CombatPreviewController 
	{
	    public CombatController _combatController;
	    CombatEditor editor;
	    public AbilityScriptableObject AbilityObj;
	
	    //CurrentRoot Data is needed when animations have rootmotions in preview.
	    public static Vector3 CurrentRootT;
	    public static Vector3 CurrentRootQ;
	    public static Vector3 CurrentMotionT;
	    public static Vector3 CurrentMotionQ;
	
	
	    public void SetPreviewTarget(CombatController combatController, AbilityScriptableObject animObj)
	    {
	        if (_combatController != combatController)
	        {
	            RestoreControllerPosition();
	            hasRecordedStartPosition = false;
	        }

	        _combatController = combatController;
	    }
	
	    public void FlushAndInsAllPreviews( bool ResetToFrame0 = true)
	    {
	        if (EditorApplication.isPlaying) return;
	        if (_combatController == null)
	        {  return; }
	        if (_combatController._animator == null)
	        { Debug.Log("Please Assign the Animator In gear Icon"); return; }
	        FetchAbility();
	
	        OnDestroyPreview();
	
	        ResetPreviewGroup();
	        InitAllPreviews();
	
	        SetExpandedRecursive(GameObject.Find(CombatGlobalEditorValue.PreviewGroupName), true);
	        RecordPositionBeforeStart();
	   }
	
	    public Vector3 StartControllerPosition;
	    bool hasRecordedStartPosition;
	
	    public GameObject PreviewGroupObj;
	    public void ResetPreviewGroup()
	    {
	    
	        DestroyPreviewGroupObj();
	        //Reload 
	        if (PreviewGroupObj == null)
	        {
	            PreviewGroupObj = new GameObject(CombatGlobalEditorValue.PreviewGroupName);
	            PreviewGroupObj.hideFlags = HideFlags.DontSaveInEditor;
	        }
	    }
	
	    public void DestroyPreviewGroupObj()
	    {
	        PreviewGroupObj = GameObject.Find(CombatGlobalEditorValue.PreviewGroupName);
	        if (PreviewGroupObj != null)
	        {
	            Object.DestroyImmediate(PreviewGroupObj);
	        }
	    }
	    
	    
	
	    public void OnDestroyPreview()
	    {
	        if (EditorApplication.isPlaying) return;
	        previewsSelfDestroy();
	        ClearAllPreviewHandles();
	        DestroyPreviewGroupObj();
	        ResetMotions();
	        if (AnimationMode.InAnimationMode())
	        {
	            AnimationMode.StopAnimationMode();
	        }
	        RestoreControllerPosition();
	    }
	
	    //Calls when click the stopbutton.
	    public void OnPreviewBackToStart()
	    {
	        if (EditorApplication.isPlaying) return;
	        previewBackToStart();
	
	    }
	
	
	    public void RecordPositionBeforeStart()
	    {
	        StartControllerPosition = _combatController.transform.position;
	        hasRecordedStartPosition = true;
	        CombatGlobalEditorValue.CharacterTransPosBeforePreview = _combatController.transform.position; ;
	    }

	    void RestoreControllerPosition()
	    {
	        if (!hasRecordedStartPosition || _combatController == null)
	        {
	            return;
	        }

	        _combatController.transform.position = StartControllerPosition;
	        CombatGlobalEditorValue.CharacterRootCenterAtCurrentFrame = StartControllerPosition;
	    }
	
	
	    public void ResetMotions()
	    {
	        CurrentRootT = Vector3.zero;
	        CurrentRootQ = Vector3.zero;
	        CurrentMotionT = Vector3.zero;
	        CurrentMotionQ = Vector3.zero;
	    }
	
	    public void previewsSelfDestroy()
	    {
	        if (previews != null)
	        {
	            for (int i = 0; i < previews.Count; i++)
	            {
	                previews[i].DestroyPreview();
	            }
	        }
	    }
	    public void previewBackToStart()
	    {
	        if (previews != null)
	        {
	            for (int i = 0; i < previews.Count; i++)
	            {
	                previews[i].BackToStart();
	            }
	        }
	    }
	
	    public void OnPlayModeStart()
	    {
	        DestroyPreviewGroupObj();
	    }
	
	    //The currentRunning percentage.
	    float PercentageTime;
	
	    // Because of timescale, the particles need the RealTime to perform.
	    float RealTime;
	
	    // Preview the animation at the requested normalized time.
	    public void ShowPreviewAtPercentage(float Percentage)
	    {
	        FetchAbility();
	        if (EditorApplication.isPlaying || AbilityObj == null || _combatController == null || _combatController._animator == null) return;
	        if (AbilityObj.Clip == null) return;
	        PercentageTime = Percentage;
	        UpdateAnimation(Percentage);
	    }
	
	    public void FetchAbility()
	    {
	        if (editor == null)
	        {
	            editor = CombatEditorUtility.GetCurrentEditor();
	        }
	        if (editor == null)
	        {
	            AbilityObj = null;
	            return;
	        }
	        AbilityObj = editor.SelectedAbilityObj;
	    }
	
	    public int DebugAnimFrame;
	
	   
	
	    int TotalFrame => (int)(AbilityObj.Clip.length * 60);
	
	
	    /// <summary>
	    /// Sample and refresh the animation preview.
	    /// </summary>
	    public void UpdateAnimation(float percentage)
	    {
	        if (!AnimationMode.InAnimationMode())
	        {
	            AnimationMode.StartAnimationMode();
	      
	        }
	        if (AnimationMode.InAnimationMode())
	        {
	            UpdatePreview(percentage);
	
	        }
	        SceneView.RepaintAll();
	    }
	
	
	    public void UpdateAnimationInEditMode(float time)
	    {
	        AnimationMode.BeginSampling();
	        CombatGlobalEditorValue.Percentage = time;
	        if (_combatController != null)
	        {
	            AnimationMode.SampleAnimationClip(_combatController._animator.gameObject, AbilityObj.Clip, time * AbilityObj.Clip.length);
	            GetCurrentRootMotion(time);
	
	            GetCurrentAnimationMotion(time);
	
	            CombatGlobalEditorValue.CurrentRootMotionOffset = _combatController._animator.transform.rotation * CombatGlobalEditorValue.CurrentMotionTAtGround;
	
	            CurrentCharacterCenter = StartControllerPosition + CombatGlobalEditorValue.CurrentRootMotionOffset + CurrentFrameMotions;
	
	            CombatGlobalEditorValue.CharacterRootCenterAtCurrentFrame = CurrentCharacterCenter;
	        }
	        else
	        {
	        }
	        AnimationMode.EndSampling();
	        if (_combatController != null)
	        {
	            _combatController.transform.position = CurrentCharacterCenter;
	        }
	    }
	
	   
	
	    // added of Current Motions and Current Roots.
	    public Vector3 CurrentCharacterCenter;
	
	    Vector3 CurrentFrameMotions;
	    public void GetCurrentAnimationMotion(float timePercentage)
	    {
	        CurrentFrameMotions = Vector3.zero;
	        if(previews!=null)
	        {
	            List<AbilityEventPreview_Motion> Motions = new List<AbilityEventPreview_Motion>();
	            for(int i =0;i<previews.Count;i++)
	            {
	                if(previews[i].GetType() == typeof(AbilityEventPreview_Motion))
	                {
	                    Motions.Add((AbilityEventPreview_Motion)previews[i]);
	                }
	            }
	            for(int i =0;i<Motions.Count;i++)
	            {
	                CurrentFrameMotions += Motions[i].GetOffsetAtCurrentFrame(timePercentage);
	            }
	
	
	        }
	    }
	
	    public void GetCurrentRootMotion(float timePercentage)
	    {
	        var bindings = AnimationUtility.GetCurveBindings(AbilityObj.Clip);
	        Vector3 currentRootPosition = Vector3.zero;
	        Vector3 startRootPosition = Vector3.zero;
	        CurrentRootQ = Vector3.zero;
	        CurrentMotionT = Vector3.zero;
	        CurrentMotionQ = Vector3.zero;
	
	        for (int i = 0; i < bindings.Length; i++)
	        {
	            var curve = AnimationUtility.GetEditorCurve(AbilityObj.Clip, bindings[i]);
	            float value = curve.Evaluate(timePercentage * AbilityObj.Clip.length);
	            float startValue = curve.Evaluate(0f);
	            switch (bindings[i].propertyName)
	            {
	                case "RootT.x":
	                    currentRootPosition.x = value;
	                    startRootPosition.x = startValue;
	                    break;
	                case "RootT.y":
	                    currentRootPosition.y = value;
	                    startRootPosition.y = startValue;
	                    break;
	                case "RootT.z":
	                    currentRootPosition.z = value;
	                    startRootPosition.z = startValue;
	                    break;
	                case "RootQ.x":
	                    CurrentRootQ.x = value; break;
	                case "RootQ.y":
	                    CurrentRootQ.y = value; break;
	                case "RootQ.z":
	                    CurrentRootQ.z = value; break;
	                case "MotionT.x":
	                    CurrentMotionT.x = value; break;
	                case "MotionT.y":
	                    CurrentMotionT.y = value; break;
	                case "MotionT.z":
	                    CurrentMotionT.z = value; break;
	            }
	        }

	        CurrentRootT = currentRootPosition - startRootPosition;
	        CombatGlobalEditorValue.CurrentMotionTAtGround = new Vector3(CurrentRootT.x, 0, CurrentRootT.z);
	
	        //Debug.Log(timePercentage + ":" + new Vector3(CurrentRootT.x, CurrentRootT.y, CurrentRootT.z));
	
	        //Debug.Log(timePercentage + ":" + new Vector3(CurrentMotionT.x, 0, CurrentMotionT.z));
	
	    }
	
	    List<AbilityEventPreview> previews;
	
	    public void InitAllPreviews()
	    {
	        previews = new List<AbilityEventPreview>();
	        bool removedInvalidEvent = false;

	        if (AbilityObj != null)
	        {
	            for (int i = 0; i < AbilityObj.events.Count; i++)
	            {
	                var abilityEvent = AbilityObj.events[i];
	                if (abilityEvent == null || abilityEvent.Obj == null)
	                {
	                    if (!removedInvalidEvent)
	                    {
	                        Undo.RecordObject(AbilityObj, "Remove Missing Ability Event");
	                    }

	                    AbilityObj.events.RemoveAt(i);
	                    i--;
	                    removedInvalidEvent = true;
	                    continue;
	                }
	                if (abilityEvent.Obj.IsActive)
	                {
	                    if (abilityEvent.Obj != null)
	                    {
	                        AbilityEventPreview preview;
	                        preview = abilityEvent.Obj.InitializePreview();
	
	                        if (preview != null)
	                        {
	                            preview.eve = abilityEvent;
	                            preview._combatController = _combatController;
	                            preview.AnimObj = AbilityObj;
	                            previews.Add(preview);
	                        }
	                    }
	                }
	            }
	        }

	        if (removedInvalidEvent)
	        {
	            EditorUtility.SetDirty(AbilityObj);
	            AssetDatabase.SaveAssets();
	        }

	        foreach (var preview in previews)
	        {
	            preview.InitPreview();
	        }
	    }
	    /// <summary>
	    /// Clear All Preview Objects In Scene. 
	    /// Used when preview object changes, or after compile.
	    /// </summary>
	    public void ClearAllPreviewHandles()
	    {
	        PreviewGroupObj = GameObject.Find(CombatGlobalEditorValue.PreviewGroupName);
	        if (PreviewGroupObj != null)
	        {
	            var handles = PreviewGroupObj.GetComponents<PreviewerOnObject>();
	            foreach (var handle in handles)
	            {
	                handle.SelfDestroy();
	            }
	        }
	    }
	
	    public float PreviewAnimSpeed = 1;
	
	
	    public float GetScaledPercentage(float percentage)
	    {
	        percentage = Mathf.Clamp01(percentage);
	        if (previews == null || AbilityObj == null || AbilityObj.Clip == null)
	        {
	            return percentage;
	        }

	        int totalFrames = Mathf.Max(1, Mathf.RoundToInt(AbilityObj.Clip.length * 60));
	        int targetFrame = Mathf.RoundToInt(percentage * totalFrames);
	        float scaledPercentage = 0;

	        for (int frame = 0; frame < targetFrame; frame++)
	        {
	            float startPercentage = frame / (float)totalFrames;
	            float endPercentage = (frame + 1) / (float)totalFrames;
	            float midPercentage = (startPercentage + endPercentage) * 0.5f;
	            float speedMultiplier = GetPreviewSpeedMultiplier(midPercentage);
	            scaledPercentage += (endPercentage - startPercentage) / speedMultiplier;
	        }

	        return scaledPercentage;
	    }

	    float GetPreviewSpeedMultiplier(float percentage)
	    {
	        float speedMultiplier = 1;

	        foreach (var preview in previews)
	        {
	            if (preview is AbilityEventPreview_AnimSpeed animSpeedPreview && animSpeedPreview.PreviewInRange(percentage))
	            {
	                float eventDuration = animSpeedPreview.EndTimePercentage - animSpeedPreview.StartTimePercentage;
	                float normalizedTime = eventDuration > 0
	                    ? (percentage - animSpeedPreview.StartTimePercentage) / eventDuration
	                    : 0;
	                speedMultiplier *= animSpeedPreview.Obj.GetSpeedMultiplier(normalizedTime);
	            }
	        }

	        return Mathf.Max(0.0001f, speedMultiplier);
	    }
	
	
	
	
	    public void UpdatePreview(float percentage)
	    {
	        if (previews != null)
	        {
	            foreach (var preview in previews)
	            {
	
	                preview.FetchCurrentValues();
	
	                preview.StartTimeScaledPercentage = GetScaledPercentage(preview.StartTimePercentage);
	                preview.EndTimeScaledPercentage = GetScaledPercentage(preview.EndTimePercentage);
	
	                //Used for dragging to preview. SomeTimes, preview need datas when on start, for example, the particle position need to know the position on EventStart.
	                if (!(editor.IsPlaying || editor.IsLooping))
	                {
	                    if (preview.NeedStartFrameValue())
	                    {
	                        UpdateAnimationInEditMode(preview.StartTimePercentage);
	                    }
	                    preview.GetStartFrameDataBeforePreview();
	                }
	                //Used for running preview.Static particle need to reset position because of the motion event.
	                else
	                {
	                    if (preview.NeedStartFrameValue() && preview.IsOnStartFrame)
	                    {
	                        preview.GetStartFrameDataBeforePreview();
	                    }
	                }
	            }
	            UpdateAnimationInEditMode(percentage);
	            foreach (var preview in previews)
	            {
	                preview.StartTimeScaledPercentage = GetScaledPercentage(preview.StartTimePercentage);
	                preview.EndTimeScaledPercentage = GetScaledPercentage(preview.EndTimePercentage);
	
	                preview.PreviewRunning(PercentageTime);
	
	                preview.PreviewRunningInScale(GetScaledPercentage(PercentageTime));
	
	            }
	        }
	        else
	        UpdateAnimationInEditMode(percentage);
	    }
	
	
	    public static void Collapse(GameObject go, bool collapse)
	    {
	        //var LastSelected = Selection.activeObject;
	        // bail out immediately if the go doesn't have children
	        if (go.transform.childCount == 0) return;
	        // get a reference to the hierarchy window
	        var hierarchy = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow"));
	
	
	
	        // select our go
	        Selection.activeObject = go;
	
	
	        // create a new key event (RightArrow for collapsing, LeftArrow for folding)
	        var key = new Event { keyCode = collapse ? KeyCode.RightArrow : KeyCode.LeftArrow, type = EventType.KeyDown };
	        // finally, send the window the event
	        hierarchy.SendEvent(key);
	
	
	        //Selection.activeObject = LastSelected;
	
	    }
	    //public static void SelectObject(Object obj)
	    //{
	    //    Selection.activeObject = obj;
	    //}
	    public static EditorWindow GetFocusedWindow(string window)
	    {
	        FocusOnWindow(window);
	        return EditorWindow.focusedWindow;
	    }
	    public static void FocusOnWindow(string window)
	    {
	        EditorApplication.ExecuteMenuItem("Window/" + window);
	    }
	
	    public static void SetExpandedRecursive(GameObject go, bool expand)
	    {
	        if (go == null)
	        {
	            return;
	        }

	        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
	        if (type == null)
	        {
	            return;
	        }

	        var methodInfo = type.GetMethod("SetExpandedRecursive");
	        if (methodInfo == null)
	        {
	            return;
	        }

	        // This differs in unity versions.
	        // Old version should be "Window/Hierarchy."
	        EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");

	        EditorWindow hierarchyWindow = EditorWindow.GetWindow(type);
	        if (hierarchyWindow == null || !type.IsInstanceOfType(hierarchyWindow))
	        {
	            return;
	        }

	        try
	        {
	            methodInfo.Invoke(hierarchyWindow, new object[] { go.GetInstanceID(), expand });
	        }
	        catch
	        {
	            // Expanding the preview object in Hierarchy is optional; ignore version-specific reflection failures.
	        }
	    }
	
	
	 
	
	}
}
