
using CombatCamera;
using UnityEngine;
 namespace CombatEditor {	
	[AbilityEvent]
	[CreateAssetMenu(menuName = "AbilityEvents / CreateHitBox")]
	//CreateHitBoxEvent
	public class AbilityEventObj_CreateHitBox : AbilityEventObj_CreateObjWithHandle
	{
	    [Header("Damage")]
	    [Min(0f)] public float Damage = 10f;
	    [Min(0f)] public float PoiseDamage = 10f;
	    public CombatHitReactionPolicy HitReaction =
	        CombatHitReactionPolicy.FirstHitOnly;
	    [Min(0f)] public float StaggerDuration;

	    [Header("Hit Rules")]
	    public CombatHitMode HitMode = CombatHitMode.Single;
	    [Min(1)] public int RepeatIntervalFrames = 6;
	    [Tooltip("0 means unlimited until the hit-box window closes.")]
	    [Min(0)] public int MaximumHitsPerTarget;
	    public LayerMask TargetLayers = ~0;
	    public bool AllowFriendlyFire;

	    [Header("Confirmed Hit Camera Shake")]
	    [Tooltip("Played after this hit-box produces an accepted hit. Repeated hit-boxes play it once per accepted hit.")]
	    public bool EnableHitCameraShake;
	    [Min(0.01f)] public float HitCameraShakeDuration = 0.16f;
	    public bool HitCameraShakeUseUnscaledTime = true;
	    public CameraShakeSettings HitCameraShakeSettings = new CameraShakeSettings
	    {
	        Channel = CameraShakeChannel.Impact,
	        EnableDirectionalImpulse = true
	    };

	    [Header("Editor Preview")]
	    [Tooltip("Simulate one confirmed hit at the beginning of this HitBox range while previewing the timeline.")]
	    public bool PreviewHitCameraShake;

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
	        if (TargetObj.ObjData == null)
	        {
	            return;
	        }
	        var Obj = TargetObj.ObjData.CreateObject(_combatController);
	        if (Obj == null)
	        {
	            return;
	        }

	        CurrentHitBox = Obj.GetComponent<HitBox>();
            if(CurrentHitBox!=null)
            {
                CurrentHitBox.Init(_combatController, AnimObj, TargetObj);
                CurrentHitBox.UpdateAnimationTime(eve.GetEventStartTime());
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
	        }
	        CapsuleCollider capsuleCollider = Obj.GetComponent<CapsuleCollider>();
	        if(capsuleCollider != null)
	        {
	            capsuleCollider.center = Vector3.zero;
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
	    public override void EffectRunning(float currentTimePercentage)
	    {
	        base.EffectRunning(currentTimePercentage);
	        CurrentHitBox?.UpdateAnimationTime(currentTimePercentage);
	    }
	    public override void EffectRunningFixedUpdate(float currentTimePercentage)
	    {
	        base.EffectRunningFixedUpdate(currentTimePercentage);
	        CurrentHitBox?.UpdateAnimationTime(currentTimePercentage);
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
