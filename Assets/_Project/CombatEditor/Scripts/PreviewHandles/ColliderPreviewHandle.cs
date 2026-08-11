using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace CombatEditor {
    public class ColliderPreviewHandle : PreviewerOnObject
	{
	    public AbilityEventPreview_CreateHitBox colliderPreview;
	    AbilityEventObj_CreateHitBox EventObj => colliderPreview._EventObj as AbilityEventObj_CreateHitBox;


	    BoxBoundsHandle boxHandle;
	    CapsuleBoundsHandle capsuleHandle;
	    SphereBoundsHandle sphereHandle;


	    public override void Init()
	    {
	        base.Init();
	        var capsuleCollider = GetComponent<CapsuleCollider>();
            var capsuleCollider2D = GetComponent<CapsuleCollider2D>();

	        if(capsuleCollider!=null || capsuleCollider2D !=null)
	        {
	            capsuleHandle = new CapsuleBoundsHandle();
	            capsuleHandle.axes = PrimitiveBoundsHandle.Axes.All;
	            capsuleHandle.radius = EventObj.Radius;
	            capsuleHandle.height = EventObj.Height;
	            capsuleHandle.handleColor = Color.green;
	            capsuleHandle.wireframeColor = Color.green;
	        }
	        var sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider!=null)
	        {
	            sphereHandle = new SphereBoundsHandle();
	            sphereHandle.axes = PrimitiveBoundsHandle.Axes.All;
	            sphereHandle.radius = EventObj.Radius;
	            sphereHandle.handleColor = Color.green;
	            sphereHandle.wireframeColor = Color.green;
	        }
	  
	    }
	  
	
	    public Vector3 MatrixPos;
	    public Quaternion MatrixRot;
	
	    Vector3 CenterPos;
	    public override void PaintHandle()
	    {
        #region PositionUpdate
	
	        Quaternion AnimatorRotation = colliderPreview._combatController._animator.transform.rotation;
	
	        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
	
	
	        EditorGUI.BeginChangeCheck();
	        var BoundsMatrix = Matrix4x4.identity;
	
	        BoundsMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
	        Quaternion RelativeRot = Quaternion.identity;
	
	        //Handles.color = Color.white;
	
	        Vector3 TargetPos = Vector3.zero;
	        Handles.color = Color.white;
        using (new Handles.DrawingScope(BoundsMatrix))
	        {
	            if (capsuleHandle != null)
	            {
	                capsuleHandle.radius = EventObj.Radius;
	                capsuleHandle.height = EventObj.Height;
	                capsuleHandle.center = Vector3.zero;
	                capsuleHandle.DrawHandle();
	            }
	
	            if(sphereHandle!=null)
	            {
	                sphereHandle.radius = EventObj.Radius;
	                sphereHandle.center = Vector3.zero;
	                sphereHandle.DrawHandle();
	            }
	
	        }
	
	        if (EditorGUI.EndChangeCheck())
	        {
	            Undo.RecordObject(EventObj, "SetHandle!");
	            if (capsuleHandle != null)
	            {
	                EventObj.Radius = capsuleHandle.radius;
	                EventObj.Height = capsuleHandle.height;
	            }
	            if(sphereHandle!=null)
	            {
	                EventObj.Radius = sphereHandle.radius;
	            }
	
	        }
	
        #endregion
	    }
	}
}
#endif
