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
	    BoxCollider boxCollider;
	    CapsuleCollider capsuleCollider;
	    CapsuleCollider2D capsuleCollider2D;
	    SphereCollider sphereCollider;


	    public override void Init()
	    {
	        base.Init();
	        boxCollider = GetComponent<BoxCollider>();
	        if (boxCollider != null)
	        {
	            boxHandle = new BoxBoundsHandle
	            {
	                center = boxCollider.center,
	                size = boxCollider.size,
	                handleColor = Color.green,
	                wireframeColor = Color.green
	            };
	        }
	        capsuleCollider = GetComponent<CapsuleCollider>();
            capsuleCollider2D = GetComponent<CapsuleCollider2D>();

	        if(capsuleCollider!=null || capsuleCollider2D !=null)
	        {
	            capsuleHandle = new CapsuleBoundsHandle();
	            capsuleHandle.axes = PrimitiveBoundsHandle.Axes.All;
	            capsuleHandle.radius = GetCapsuleRadius();
	            capsuleHandle.height = GetCapsuleHeight();
	            capsuleHandle.handleColor = Color.green;
	            capsuleHandle.wireframeColor = Color.green;
	        }
	        sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider!=null)
	        {
	            sphereHandle = new SphereBoundsHandle();
	            sphereHandle.axes = PrimitiveBoundsHandle.Axes.All;
	            sphereHandle.radius = sphereCollider.radius;
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

	        // Inspector edits do not advance the preview timeline, so make sure the
	        // preview object uses the latest serialized scale before drawing.
	        if (EventObj != null && EventObj.ObjData != null)
	        {
	            transform.localScale = EventObj.ObjData.GetValidScale();
	        }
	
	        Quaternion AnimatorRotation = colliderPreview._combatController._animator.transform.rotation;
	
	        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
	
	
	        EditorGUI.BeginChangeCheck();
	        var BoundsMatrix = Matrix4x4.identity;
	
	        BoundsMatrix = Matrix4x4.TRS(transform.position, transform.rotation,
	            transform.lossyScale);
	        Quaternion RelativeRot = Quaternion.identity;
	
	        //Handles.color = Color.white;
	
	        Vector3 TargetPos = Vector3.zero;
	        Handles.color = Color.white;
        using (new Handles.DrawingScope(BoundsMatrix))
	        {
	            if (boxHandle != null)
	            {
	                boxHandle.center = boxCollider.center;
	                boxHandle.DrawHandle();
	            }

	            if (capsuleHandle != null)
	            {
	                capsuleHandle.radius = GetCapsuleRadius();
	                capsuleHandle.height = GetCapsuleHeight();
	                capsuleHandle.center = Vector3.zero;
	                capsuleHandle.DrawHandle();
	            }
	
	            if(sphereHandle!=null)
	            {
	                sphereHandle.radius = sphereCollider.radius;
	                sphereHandle.center = Vector3.zero;
	                sphereHandle.DrawHandle();
	            }
	
	        }
	
	        if (EditorGUI.EndChangeCheck())
	        {
	            Undo.RecordObject(EventObj, "SetHandle!");
	            if (boxHandle != null && EventObj.ObjData != null)
	            {
	                Vector3 baseSize = boxCollider.size;
	                Vector3 ratio = new Vector3(
	                    SafeRatio(boxHandle.size.x, baseSize.x),
	                    SafeRatio(boxHandle.size.y, baseSize.y),
	                    SafeRatio(boxHandle.size.z, baseSize.z));
	                EventObj.ObjData.Scale = Vector3.Scale(
	                    EventObj.ObjData.GetValidScale(), ratio);
	                boxHandle.size = baseSize;
	            }
	            if (capsuleHandle != null)
	            {
	                ApplyCapsuleHandleScale();
	            }
	            if(sphereHandle!=null)
	            {
	                float ratio = SafeRatio(sphereHandle.radius,
	                    sphereCollider.radius);
	                EventObj.ObjData.Scale *= ratio;
	                sphereHandle.radius = sphereCollider.radius;
	            }
	            EditorUtility.SetDirty(EventObj);
	
	        }
	
        #endregion
	    }

	    private static float SafeRatio(float value, float divisor)
	    {
	        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : 1f;
	    }

	    private float GetCapsuleRadius()
	    {
	        if (capsuleCollider != null) return capsuleCollider.radius;
	        if (capsuleCollider2D == null) return 0.5f;
	        return Mathf.Min(capsuleCollider2D.size.x,
	            capsuleCollider2D.size.y) * 0.5f;
	    }

	    private float GetCapsuleHeight()
	    {
	        if (capsuleCollider != null) return capsuleCollider.height;
	        if (capsuleCollider2D == null) return 1f;
	        return Mathf.Max(capsuleCollider2D.size.x,
	            capsuleCollider2D.size.y);
	    }

	    private void ApplyCapsuleHandleScale()
	    {
	        float radiusRatio = SafeRatio(capsuleHandle.radius,
	            GetCapsuleRadius());
	        float heightRatio = SafeRatio(capsuleHandle.height,
	            GetCapsuleHeight());
	        Vector3 ratio = Vector3.one;

	        if (capsuleCollider != null)
	        {
	            ratio = new Vector3(radiusRatio, radiusRatio, radiusRatio);
	            ratio[capsuleCollider.direction] = heightRatio;
	        }
	        else if (capsuleCollider2D != null)
	        {
	            bool vertical = capsuleCollider2D.direction ==
	                CapsuleDirection2D.Vertical;
	            ratio.x = vertical ? radiusRatio : heightRatio;
	            ratio.y = vertical ? heightRatio : radiusRatio;
	        }

	        EventObj.ObjData.Scale = Vector3.Scale(
	            EventObj.ObjData.GetValidScale(), ratio);
	        capsuleHandle.radius = GetCapsuleRadius();
	        capsuleHandle.height = GetCapsuleHeight();
	    }
	}
}
#endif
