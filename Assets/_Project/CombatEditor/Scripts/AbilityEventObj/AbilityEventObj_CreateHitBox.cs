
using UnityEngine;
 namespace CombatEditor {	
	[AbilityEvent]
	[CreateAssetMenu(menuName = "AbilityEvents / CreateHitBox")]
	//CreateHitBoxEvent
	public class AbilityEventObj_CreateHitBox : AbilityEventObj_CreateObjWithHandle
	{
	    public float Radius = 1;
	
	    public float Height = 1;
	
	    public override EventTimeType GetEventTimeType()
	    {
	        return EventTimeType.EventRange;
	    }
	    public override AbilityEventEffect Initialize()
	    {
	        return new AbilityEventEffect_CreateHitBox(this);
	    }
#if UNITY_EDITOR
	    public override AbilityEventPreview InitializePreview()
	    {
	        return new AbilityEventPreview_CreateHitBox(this);
	    }
#endif
	}
	public partial class AbilityEventEffect_CreateHitBox : AbilityEventEffect
	{
	    public HitBox CurrentHitBox;
	   
	
	    public override void StartEffect()
	    {
	        base.StartEffect();
	        var Obj = TargetObj.ObjData.CreateObject(_combatController);
	        if (Obj == null)
	        {
	            return;
	        }

	        CurrentHitBox = Obj.GetComponent<HitBox>();
            if(CurrentHitBox!=null)
            {
                CurrentHitBox.Init(_combatController, AnimObj, TargetObj);
            }

	        BoxCollider boxCollider = Obj.GetComponent<BoxCollider>();
	        if(boxCollider!=null)
	        {
	            boxCollider.center = Vector3.zero;
	        }
	        SphereCollider sphereCollider = Obj.GetComponent<SphereCollider>();
	        if(sphereCollider!=null)
	        {
	            sphereCollider.center = Vector3.zero;
	            sphereCollider.radius = TargetObj.Radius;
	        }
	        CapsuleCollider capsuleCollider = Obj.GetComponent<CapsuleCollider>();
	        if(capsuleCollider != null)
	        {
	            capsuleCollider.center = Vector3.zero;
	            capsuleCollider.radius = TargetObj.Radius;
	            capsuleCollider.height = TargetObj.Height;
	        }
            BoxCollider2D boxCollider2D = Obj.GetComponent<BoxCollider2D>();
            if (boxCollider2D != null)
            {
                boxCollider2D.transform.rotation = Quaternion.identity;
                boxCollider2D.offset = Vector2.zero;
            }
            CapsuleCollider2D capsuleCollider2D = Obj.GetComponent<CapsuleCollider2D>();
            if (capsuleCollider2D != null)
            {
                capsuleCollider2D.transform.rotation = Quaternion.identity;
                capsuleCollider2D.offset = Vector2.zero;
            }

        }
	    public override void EffectRunning()
	    {
	        base.EffectRunning();
	    }
	    public override void EndEffect()
	    {
	        if (CurrentHitBox != null)
	        {
	            GameObject.Destroy(CurrentHitBox.gameObject);
	        }
	        base.EndEffect();
	    }
	}
	public partial class AbilityEventEffect_CreateHitBox : AbilityEventEffect
	{
	    AbilityEventObj_CreateHitBox TargetObj => (AbilityEventObj_CreateHitBox)_EventObj;
	    public AbilityEventEffect_CreateHitBox(AbilityEventObj InitObj) : base(InitObj)
	    {
	        _EventObj = InitObj;
	    }
	}
}
