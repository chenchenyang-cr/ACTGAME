
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
        [Tooltip("Whether this attack can be deflected during a parry window.")]
        public bool CanBeParried = true;

	    [Header("命中后镜头震动")]
	    [InspectorName("启用命中震动")]
	    [Tooltip("碰撞盒确认命中后触发震动；重复命中时每次确认都会触发。")]
	    public bool EnableHitCameraShake;
	    [InspectorName("震动预设")]
	    public CameraShakeProfile HitCameraShakeProfile;

	    // Backwards-compatible fallback for ability assets not migrated yet.
	    [HideInInspector]
	    [Min(0.01f)] public float HitCameraShakeDuration = 0.16f;
	    [HideInInspector]
	    public bool HitCameraShakeUseUnscaledTime = true;
	    [HideInInspector]
	    public CameraShakeSettings HitCameraShakeSettings = new CameraShakeSettings
	    {
	        Channel = CameraShakeChannel.Impact,
	        EnableDirectionalImpulse = true
	    };

	    [Header("编辑器预览")]
	    [InspectorName("预览命中震动")]
	    [Tooltip("在碰撞盒区间起点模拟一次确认命中。")]
	    public bool PreviewHitCameraShake;

	    public CameraShakeSettings ResolveHitCameraShakeSettings()
	    {
	        return HitCameraShakeProfile != null
	            ? HitCameraShakeProfile.Settings
	            : HitCameraShakeSettings;
	    }

	    public float ResolveHitCameraShakeDuration()
	    {
	        return HitCameraShakeProfile != null
	            ? HitCameraShakeProfile.Duration
	            : HitCameraShakeDuration;
	    }

	    public bool ResolveHitCameraShakeUseUnscaledTime()
	    {
	        return HitCameraShakeProfile != null
	            ? HitCameraShakeProfile.UseUnscaledTime
	            : HitCameraShakeUseUnscaledTime;
	    }

	    public bool ResolveHitCameraShakeAffectedByHitStop()
	    {
	        return HitCameraShakeProfile != null &&
	               HitCameraShakeProfile.AffectedByHitStop;
	    }

	    public float ResolveHitCameraShakeHitStopWeight()
	    {
	        return HitCameraShakeProfile != null
	            ? Mathf.Clamp01(HitCameraShakeProfile.HitStopWeight)
	            : 0f;
	    }

	    [Header("命中顿帧")]
	    [InspectorName("启用命中顿帧")]
	    [Tooltip("命中确认后，攻击者立即应用顿帧，受击者延迟 1 个游戏帧应用。")]
	    public bool EnableHitAnimationSpeed;
	    [InspectorName("顿帧帧数")]
	    [Tooltip("按动作时间线 60 FPS 换算持续时间，6 帧 = 0.1 秒。0 表示不顿帧；不受游戏帧率和动画速度影响。")]
	    [Min(0)] public int HitStopFrames = 5;
	    [InspectorName("顿帧动画速度")]
	    [Tooltip("相对于当前动作速度的倍率：0 完全暂停，0.5 半速，1 保持原速。只在顿帧持续时间内生效。")]
	    [Range(0f, 1f)] public float HitStopAnimationSpeed;

	    [Header("Confirmed Hit VFX")]
	    [Tooltip("Spawn this effect only after the hit has been accepted by the target.")]
	    public bool EnableHitVfx;
	    public GameObject HitVfxPrefab;
	    public CombatHitVfxDirectionMode HitVfxDirection =
	        CombatHitVfxDirectionMode.AttackDirection;
	    [InspectorName("粒子生成位置模式")]
	    public CombatHitVfxPositionMode HitVfxPositionMode = CombatHitVfxPositionMode.HitPoint;
	    [InspectorName("固定位置偏移")]
	    [Tooltip("命中点模式：按特效朝向计算偏移。对手位置模式：世界坐标偏移，不随特效旋转。")]
	    public Vector3 HitVfxPositionOffset;
	    [InspectorName("随机位置偏移幅度")]
	    [Tooltip("仅对手位置模式生效。每次生成时，各世界坐标轴独立在 [-幅度, +幅度] 内随机取值；0 表示该轴不随机，负数按 0 处理。")]
	    public Vector3 HitVfxRandomPositionAmplitude;
	    public Vector3 HitVfxRotationOffset;
	    [Min(0f)] public float HitVfxScale = 1f;
	    [Tooltip("Seconds before the spawned object is destroyed. 0 automatically estimates particle duration.")]
	    [Min(0f)] public float HitVfxLifetime;
	    public CombatHitResultMask HitVfxResultMask =
	        CombatHitResultMask.Normal | CombatHitResultMask.Critical;

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
	            CurrentHitBox.CancelHits();
	            GameObject.Destroy(CurrentHitBox.gameObject);
	            CurrentHitBox = null;
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
