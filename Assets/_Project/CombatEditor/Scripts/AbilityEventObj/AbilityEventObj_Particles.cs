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
	public class AbilityEventObj_Particles : AbilityEventObj_CreateObjWithHandle
	{
	    //public InsedObject ParticleData = new InsedObject();
	    public EventTimeType TimeType = EventTimeType.EventTime;
	    [Min(0.01f)] public float PlaySpeed = 1f;
	    [InspectorName("受顿帧影响")]
	    [Tooltip("开启后，粒子在所属角色顿帧时按顿帧动画速度暂停或减速；顿帧结束后恢复粒子播放速度。")]
	    public bool AffectedByHitStop;
	    [Min(0f)] public float SizeMultiplier = 1f;
	    public Vector3 CenterOffset = Vector3.zero;
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
	    AbilityEventObj_Particles Obj => (AbilityEventObj_Particles)_EventObj;
	    //Vector3 TargetPos => _combatController.transform.position + _combatController._animator.transform.rotation * Obj.Offset;
	
	    GameObject InsedParticle;
	    ParticleSystem[] particleSystems;
	    float[] normalSimulationSpeeds;
	    public AbilityEventEffect_Particles(AbilityEventObj Obj) : base(Obj)
	    {
	        _EventObj = Obj;
	    }
	
	    public override void EndEffect()
	    {
	        base.EndEffect();
	        if (InsedParticle != null)
	        {
	            Object.Destroy(InsedParticle);
	            InsedParticle = null;
	        }
	        particleSystems = null;
	        normalSimulationSpeeds = null;
	    }
	    public override void StartEffect()
	    {
	        base.StartEffect();
	        InsedParticle = Obj.ObjData.CreateObject(_combatController);
	        ParticleSizeUtility.ApplySize(InsedParticle, Obj.SizeMultiplier);
	        ParticleCenterOffsetUtility.ApplyOffset(InsedParticle, Obj.CenterOffset);
	        ParticlePlaybackSpeedUtility.ApplySpeed(InsedParticle, Obj.PlaySpeed);
	        if (Obj.AffectedByHitStop && InsedParticle != null)
	        {
	            particleSystems = InsedParticle.GetComponentsInChildren<ParticleSystem>(true);
	            normalSimulationSpeeds = new float[particleSystems.Length];
	            for (int i = 0; i < particleSystems.Length; i++)
	                normalSimulationSpeeds[i] = particleSystems[i].main.simulationSpeed;
	            ApplyHitStopSpeed();
	        }
	    }

	    public override void EffectRunning()
	    {
	        ApplyHitStopSpeed();
	    }

	    void ApplyHitStopSpeed()
	    {
	        if (particleSystems == null || _combatController._animSpeedExecutor == null)
	            return;

	        float hitStopSpeed = _combatController._animSpeedExecutor.CurrentHitStopSpeedScale;
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            if (particleSystems[i] == null) continue;
	            var main = particleSystems[i].main;
	            main.simulationSpeed = normalSimulationSpeeds[i] * hitStopSpeed;
	        }
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
