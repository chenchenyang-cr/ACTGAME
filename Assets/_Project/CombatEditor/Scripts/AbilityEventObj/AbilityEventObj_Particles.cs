using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
	[System.Serializable]
	public class InsedObject
	{
	    public GameObject TargetObj;
	    public PreviewTransformHandle.ControlTypeEnum controlType;
	    public Vector3 Offset;
	    public Quaternion Rot;
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
	    public float PlaySpeed = 1f;
	    public float SizeMultiplier = 1f;
	    public Vector3 CenterOffset = Vector3.zero;
	    public override EventTimeType GetEventTimeType()
	    {
	        return TimeType;
	    }
	
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
	    public AbilityEventEffect_Particles(AbilityEventObj Obj) : base(Obj)
	    {
	        _EventObj = Obj;
	    }
	
	    public override void EndEffect()
	    {
	        base.EndEffect();
	        if (Obj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventRange)
	        {
	            if (InsedParticle != null)
	            {
	                Object.Destroy(InsedParticle);
	            }
	        }
	    }
	    public override void StartEffect()
	    {
	        base.StartEffect();
	        InsedParticle = Obj.ObjData.CreateObject(_combatController);
	        if (InsedParticle != null)
	        {
	            InsedParticle.transform.localScale = Vector3.one;
	        }
	        ParticleSizeUtility.ApplySize(InsedParticle, Obj.SizeMultiplier);
	        ParticleCenterOffsetUtility.ApplyOffset(InsedParticle, Obj.CenterOffset);
	        ParticlePlaybackSpeedUtility.ApplySpeed(InsedParticle, Obj.PlaySpeed);
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

	        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            var shape = particleSystems[i].shape;
	            if (shape.enabled)
	            {
	                shape.position += centerOffset;
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
	            shape.radius *= sizeMultiplier;
	            shape.length *= sizeMultiplier;
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

	        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
	        for (int i = 0; i < particleSystems.Length; i++)
	        {
	            var main = particleSystems[i].main;
	            main.simulationSpeed *= speedMultiplier;
	        }
	    }
	}
}
