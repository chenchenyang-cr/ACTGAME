using System.Collections;
using System.Collections.Generic;
using CombatCamera;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
#if UNITY_EDITOR
	public class AbilityEventPreview_CreateHitBox : AbilityEventPreview_CreateObjWithHandle
	{
	    private int hitShakePreviewHandle;
	    public AbilityEventObj_CreateHitBox Obj => (AbilityEventObj_CreateHitBox)_EventObj;
	    public AbilityEventPreview_CreateHitBox(AbilityEventObj Obj) : base(Obj)
	    {
	        _EventObj = Obj;
	    }
	
	    public bool PreviewActive()
	    {
	        return eve.Previewable;
	    }
	
	    public override void InitPreview()
	    {
	        base.InitPreview();
	
	        if (Obj.ObjData == null || Obj.ObjData.TargetObj == null ||
	            InstantiatedObj == null)
	        {
	            return;
	        }
	        ApplyColliderSettings();
	        //AddControlScript.
	        CreateHitBoxHandles();
	    }
	    PreviewTransformHandle TransformHandle;
	    ColliderPreviewHandle ColliderHandle;
	    public void CreateHitBoxHandles()
	    {
	
	        ColliderHandle = InstantiatedObj.AddComponent<ColliderPreviewHandle>();
	
	        ColliderHandle._combatController = _combatController;
	        ColliderHandle._preview = this;
	        ColliderHandle.colliderPreview = this;
	        ColliderHandle.Init();
	    }
	    //SetCurrentParticleTime;
	    public override void PreviewUpdateFrame(float currentTimePercentage)
	    {
	        base.PreviewUpdateFrame(currentTimePercentage);
	        UpdateHitShakePreview(currentTimePercentage);
	    }

	    public override void PreviewRunning(float CurrentTime)
	    {
	        //Set Preview Position and Rotation
	        base.PreviewRunning(CurrentTime);
	        ApplyColliderSettings();
	    }

	    private void UpdateHitShakePreview(float currentTimePercentage)
	    {
	        if (!Obj.EnableHitCameraShake || !Obj.PreviewHitCameraShake ||
	            Obj.HitCameraShakeSettings == null)
	        {
	            ReleaseHitShakePreview();
	            return;
	        }

	        float elapsed = (currentTimePercentage - StartTimePercentage) *
	                        Mathf.Max(0.01f, AnimLength);
	        float duration = Mathf.Max(0.01f, Obj.HitCameraShakeDuration);
	        if (elapsed < 0f || elapsed > duration)
	        {
	            ReleaseHitShakePreview();
	            return;
	        }

	        CameraShakeSettings settings = Obj.HitCameraShakeSettings;
	        float trauma = Mathf.Clamp01(settings.TraumaPerPulse);
	        float noiseIntensity = Mathf.Pow(trauma,
	            Mathf.Max(1f, settings.TraumaExponent));
	        if (hitShakePreviewHandle == 0)
	            hitShakePreviewHandle = CameraShakeRuntime.Add(settings,
	                noiseIntensity);

	        Vector3 previewForceDirection = _combatController != null &&
	                                        _combatController._animator != null
	            ? _combatController._animator.transform.forward
	            : Vector3.forward;
	        float normalizedTime = elapsed / duration;
	        CameraShakeRuntime.Update(hitShakePreviewHandle, settings, elapsed,
	            normalizedTime, noiseIntensity, previewForceDirection, 1f);
	    }

	    private void ReleaseHitShakePreview()
	    {
	        CameraShakeRuntime.Remove(hitShakePreviewHandle);
	        hitShakePreviewHandle = 0;
	    }

	    public override void BackToStart()
	    {
	        ReleaseHitShakePreview();
	        base.BackToStart();
	    }

    private void ApplyColliderSettings()
    {
        if (InstantiatedObj == null)
        {
            return;
        }

        if (Obj.ObjData != null)
        {
            InstantiatedObj.transform.localScale = Obj.ObjData.GetValidScale();
        }

        BoxCollider boxCollider = InstantiatedObj.GetComponent<BoxCollider>();
	        if (boxCollider != null)
	        {
	            boxCollider.center = Vector3.zero;
	        }

	        SphereCollider sphereCollider = InstantiatedObj.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.center = Vector3.zero;
        }

	        CapsuleCollider capsuleCollider = InstantiatedObj.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            capsuleCollider.center = Vector3.zero;
        }

	        BoxCollider2D boxCollider2D = InstantiatedObj.GetComponent<BoxCollider2D>();
	        if (boxCollider2D != null)
	        {
	            boxCollider2D.transform.rotation = Quaternion.identity;
	            boxCollider2D.offset = Vector2.zero;
	        }

	        CapsuleCollider2D capsuleCollider2D = InstantiatedObj.GetComponent<CapsuleCollider2D>();
	        if (capsuleCollider2D != null)
	        {
	            capsuleCollider2D.transform.rotation = Quaternion.identity;
	            capsuleCollider2D.offset = Vector2.zero;
	        }
	    }
	
	    //Destroy Particles.
	    public override void DestroyPreview()
	    {
	        ReleaseHitShakePreview();
	        if (InstantiatedObj != null)
	        {
	            Object.DestroyImmediate(InstantiatedObj);
	        }
	        base.DestroyPreview();
	    }
	    
	}
#endif
}
