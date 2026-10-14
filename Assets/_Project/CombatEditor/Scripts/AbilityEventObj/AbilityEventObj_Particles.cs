using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
{	
	[System.Serializable]
	public class InsedObject
	{
	    public GameObject TargetObj;
	    public PreviewTransformHandle.ControlTypeEnum controlType;
	    public Vector3 Offset;
	    public Quaternion Rot = Quaternion.identity;
	    public Vector3 Scale = Vector3.one;
	    public CharacterNode.NodeType TargetNode;
	    public bool FollowNode = true;
	    public bool RotateByNode;

	    public Vector3 GetValidScale()
	    {
	        if (Scale == Vector3.zero)
	        {
	            return Vector3.one;
	        }
	        return Scale;
	    }
	
	    public GameObject CreateObject( CombatController controller)
	    {
	        GameObject _obj = null;
	        if (TargetObj != null)
	        {
	            _obj = Object.Instantiate(TargetObj);
	
	            var follower = _obj.AddComponent<NodeFollower>();
	            follower.Init(
	                controller.GetNodeTranform(TargetNode),
	                Offset,
	                Rot,
	                FollowNode,
	                RotateByNode,
	                controller
	                );
	            _obj.transform.localScale = GetValidScale();
	        }
	        return _obj;
	    }
	
	}
	
	[AbilityEvent]
	[CreateAssetMenu(menuName = "AbilityEvents / Particles")]
	public class AbilityEventObj_Particles : AbilityEventObj_HitSource
	{
	    //public InsedObject ParticleData = new InsedObject();
	    public EventTimeType TimeType = EventTimeType.EventTime;
	    [Min(0.01f)] public float PlaySpeed = 1f;
	    [InspectorName("受顿帧影响")]
	    [Tooltip("开启后，粒子在所属角色顿帧时按顿帧动画速度暂停或减速；顿帧结束后恢复粒子播放速度。")]
	    public bool AffectedByHitStop;
	    [InspectorName("不受全局顿帧影响")]
	    [Tooltip("开启后，此轨道生成的粒子特效在全局顿帧期间仍按自身速度播放；所属角色的命中顿帧仍由“受顿帧影响”控制。")]
	    public bool IgnoreGlobalHitStop;
	    [Min(0f)] public float SizeMultiplier = 1f;
	    public Vector3 CenterOffset = Vector3.zero;

	    [Header("粒子碰撞体")]
	    [InspectorName("启用碰撞体")]
	    [Tooltip("为整个特效创建一个命中碰撞体，特效实例启用期间生效。每条粒子事件独立计算命中次数。")]
	    public bool EnableCollision;
	    public ParticleHitBoxSettings Collision = new ParticleHitBoxSettings();
	    public override bool HasHitBox => EnableCollision;
        public int CollisionDelayFrames => HitProfile != null
            ? Mathf.Max(0, HitProfile.ParticleCollisionDelayFrames) : 0;

        public bool IsCollisionDelayElapsed(float currentTime, float eventStartTime, AnimationClip clip)
        {
            return CombatTimeline.ToFrame(currentTime, clip) -
                CombatTimeline.ToFrame(eventStartTime, clip) >= CollisionDelayFrames;
        }
	    public override EventTimeType GetEventTimeType()
	    {
	        return TimeType == EventTimeType.EventRange
	            ? EventTimeType.EventRange
	            : EventTimeType.EventTime;
	    }

#if UNITY_EDITOR
	    private void OnValidate()
	    {
	        if (TimeType != EventTimeType.EventTime &&
	            TimeType != EventTimeType.EventRange)
	        {
	            TimeType = EventTimeType.EventTime;
	        }

	        PlaySpeed = Mathf.Max(0.01f, PlaySpeed);
	        SizeMultiplier = Mathf.Max(0f, SizeMultiplier);
	    }
#endif
	
	    public override AbilityEventEffect Initialize()
	    {
	        return new AbilityEventEffect_Particles(this);
	    }
	
# if UNITY_EDITOR
	    public override AbilityEventPreview InitializePreview()
	    {
	        return new AbilityEventPreview_Particles(this);
	    }
#endif
	}
	
	
    public class AbilityEventEffect_Particles : AbilityEventEffect
    {
        private AbilityEventObj_Particles Obj => (AbilityEventObj_Particles)_EventObj;
        private ParticleEffectLifetime lifetime;
        public HitBox CurrentHitBox => lifetime != null ? lifetime.CurrentHitBox : null;

        public AbilityEventEffect_Particles(AbilityEventObj obj) : base(obj) { }

        public override void StartEffect()
        {
            base.StartEffect();
            GameObject instance = Obj.ObjData.CreateObject(_combatController);
            ParticleSizeUtility.ApplySize(instance, Obj.SizeMultiplier);
            ParticleCenterOffsetUtility.ApplyOffset(instance, Obj.CenterOffset);
            ParticlePlaybackSpeedUtility.ApplySpeed(instance, Obj.PlaySpeed);
            if (instance == null) return;
            lifetime = instance.AddComponent<ParticleEffectLifetime>();
            lifetime.Initialize(_combatController, AnimObj, Obj, eve.GetEventStartTime());
        }

        public override void EffectRunning() => lifetime?.ApplyHitStopSpeed();
        public override void EffectRunning(float time) => lifetime?.UpdateAnimationTime(time);
        public override void EffectRunningFixedUpdate(float time) => lifetime?.UpdateAnimationTime(time);

        // Animation ownership ends; the spawned instance keeps its own lifetime.
        public void ReleaseOnAnimationChange()
        {
            if (lifetime != null) lifetime.ReleaseFromAnimation();
            lifetime = null;
            base.EndEffect();
        }

        public override void EndEffect()
        {
            if (lifetime != null) lifetime.Finish();
            lifetime = null;
            base.EndEffect();
        }
    }


    public enum ParticleColliderShape
    {
        [InspectorName("盒体")] Box,
        [InspectorName("球体")] Sphere,
        [InspectorName("胶囊")] Capsule
    }

    [System.Serializable]
    public sealed class ParticleHitBoxSettings
    {
        public ParticleColliderShape Shape;
        public Vector3 Offset;
        public Vector3 Rotation;
        public Vector3 BoxSize = Vector3.one;
        [Min(0.001f)] public float Radius = 0.5f;
        [Min(0.002f)] public float Height = 2f;
        [Range(0, 2)] public int CapsuleDirection = 1;

        // A dedicated child avoids changing the particle prefab's own colliders.
        public Collider CreateCollider(GameObject particleRoot)
        {
            var child = new GameObject("Particle HitBox") { layer = particleRoot.layer };
            child.transform.SetParent(particleRoot.transform, false);
            child.transform.localPosition = Offset;
            child.transform.localRotation = Quaternion.Euler(Rotation);
            Collider collider;
            switch (Shape)
            {
                case ParticleColliderShape.Sphere:
                    var sphere = child.AddComponent<SphereCollider>();
                    sphere.radius = Mathf.Max(0.001f, Radius);
                    collider = sphere;
                    break;
                case ParticleColliderShape.Capsule:
                    var capsule = child.AddComponent<CapsuleCollider>();
                    capsule.radius = Mathf.Max(0.001f, Radius);
                    capsule.height = Mathf.Max(capsule.radius * 2f, Height);
                    capsule.direction = Mathf.Clamp(CapsuleDirection, 0, 2);
                    collider = capsule;
                    break;
                default:
                    var box = child.AddComponent<BoxCollider>();
                    box.size = new Vector3(Mathf.Max(0.001f, BoxSize.x),
                        Mathf.Max(0.001f, BoxSize.y), Mathf.Max(0.001f, BoxSize.z));
                    collider = box;
                    break;
            }
            collider.isTrigger = true;
            return collider;
        }
    }
	public static class ParticleCenterOffsetUtility
	{
	    public static void ApplyOffset(GameObject target, Vector3 centerOffset)
	    {
	        if (target == null || centerOffset == Vector3.zero)
	        {
	            return;
	        }

	        Vector3 worldOffset = target.transform.TransformVector(centerOffset);
	        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            var shape = particleSystems[i].shape;
	            if (shape.enabled)
	            {
	                Vector3 localOffset = particleSystems[i].transform.InverseTransformVector(worldOffset);
	                shape.position += localOffset;
	            }
	        }
	    }
	}

	public static class ParticleSizeUtility
	{
	    public static void ApplySize(GameObject target, float sizeMultiplier)
	    {
	        if (target == null)
	        {
	            return;
	        }

	        sizeMultiplier = Mathf.Max(0f, sizeMultiplier);
	        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            ApplySize(particleSystems[i], sizeMultiplier);
	        }
	    }

	    static void ApplySize(ParticleSystem particleSystem, float sizeMultiplier)
	    {
	        var main = particleSystem.main;
	        if (main.startSize3D)
	        {
	            main.startSizeXMultiplier *= sizeMultiplier;
	            main.startSizeYMultiplier *= sizeMultiplier;
	            main.startSizeZMultiplier *= sizeMultiplier;
	        }
	        else
	        {
	            main.startSizeMultiplier *= sizeMultiplier;
	        }

	        var shape = particleSystem.shape;
	        if (shape.enabled)
	        {
	            shape.scale *= sizeMultiplier;
	        }
	    }
	}

	public static class ParticlePlaybackSpeedUtility
	{
	    public static void ApplySpeed(GameObject target, float speedMultiplier)
	    {
	        if (target == null)
	        {
	            return;
	        }

	        speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
	        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            var main = particleSystems[i].main;
	            main.simulationSpeed *= speedMultiplier;
	        }
	    }
	}
}
