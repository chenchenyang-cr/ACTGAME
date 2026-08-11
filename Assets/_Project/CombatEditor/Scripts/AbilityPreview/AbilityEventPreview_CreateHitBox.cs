using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
#if UNITY_EDITOR
	public class AbilityEventPreview_CreateHitBox : AbilityEventPreview_CreateObjWithHandle
	{
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
	
	        if (Obj.ObjData.TargetObj == null)
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
	    public override void PreviewRunning(float CurrentTime)
	    {
	        //Set Preview Position and Rotation
	        base.PreviewRunning(CurrentTime);
	        ApplyColliderSettings();
	    }

	    private void ApplyColliderSettings()
	    {
	        if (InstantiatedObj == null)
	        {
	            return;
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
	            sphereCollider.radius = Obj.Radius;
	        }

	        CapsuleCollider capsuleCollider = InstantiatedObj.GetComponent<CapsuleCollider>();
	        if (capsuleCollider != null)
	        {
	            capsuleCollider.center = Vector3.zero;
	            capsuleCollider.radius = Obj.Radius;
	            capsuleCollider.height = Obj.Height;
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
	        if (InstantiatedObj != null)
	        {
	            Object.DestroyImmediate(InstantiatedObj);
	        }
	        base.DestroyPreview();
	    }
	    
	}
#endif
}
