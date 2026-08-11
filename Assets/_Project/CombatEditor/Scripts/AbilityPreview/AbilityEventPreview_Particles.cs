using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

 namespace CombatEditor
{	
	
# if UNITY_EDITOR
	public class AbilityEventPreview_Particles : AbilityEventPreview_CreateObjWithHandle
	{
	    public AbilityEventObj_Particles Obj => (AbilityEventObj_Particles)_EventObj;
	
	    public AbilityEventPreview_Particles(AbilityEventObj Obj) : base(Obj)
	    {
	        _EventObj = Obj;
	    }
	
	
	    public bool PreviewActive()
	    {
	        return eve.Previewable;
	    }
	
	    ParticleSystem[] particles;
	    float[] particleInitSpeeds;
	
	    public override void InitPreview()
	    {
	        base.InitPreview();
	        if (InstantiatedObj != null)
	        {
	            InstantiatedObj.transform.localScale = Vector3.one;
	            ParticleSizeUtility.ApplySize(InstantiatedObj, Obj.SizeMultiplier);
	            ParticleCenterOffsetUtility.ApplyOffset(InstantiatedObj, Obj.CenterOffset);
	        }
	        SetParticleData();
	    }
	    public void SetParticleData()
	    {
	        if (InstantiatedObj != null)
	        {
	            particles = InstantiatedObj.GetComponentsInChildren<ParticleSystem>(true);
	            if (particles != null)
	            {
	                particleInitSpeeds = new float[particles.Length];
	                for (int i = 0; i < particles.Length; i++)
	                {
	                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	                    particles[i].Clear(true);
	                    particles[i].useAutoRandomSeed = false;
	                    particleInitSpeeds[i] = particles[i].main.simulationSpeed;
	                }
	            }
	        }
	    }

	    public void ResetParticlePreview()
	    {
	        if (particles == null)
	        {
	            return;
	        }

	        for (int i = 0; i < particles.Length; i++)
	        {
	            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	            particles[i].Clear(true);
	            particles[i].Simulate(0f, true, true);
	        }
	        SceneView.RepaintAll();
	    }
	
	    /// <summary>
	    /// The particle's real time is influenced by timescale event.
	    /// </summary>
	    /// <param name="ScaledPercentage"></param>
	    public override void PreviewRunningInScale(float ScaledPercentage)
	    {
	        base.PreviewRunningInScale(ScaledPercentage);
	        if (InstantiatedObj == null) return;
	        //Debug.Log("Simulate?");
	        SimulateParticles(ScaledPercentage);
	    }
	    public void SimulateParticles(float ScaledPercentage)
	    {
	        //Set Preview Percentage
	        if (Obj.IsActive && particles != null && particles.Length > 0)
	        {
	            ApplyPreviewSpeed();
	
	            bool IsInRange = false;
	            if (EventObj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventRange && CurrentInScaledRange)
	            {
	                IsInRange = true;
	            }
	            if(EventObj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime && ScaledPercentage >= StartTimeScaledPercentage)
	            {
	                IsInRange = true;
	            }
	            //ParticleSystem need 1/60f to start simulate
	            if (IsInRange)
	            {
                    SimulateAllParticles(1 / 60f + (ScaledPercentage - StartTimeScaledPercentage) * AnimLength);
                    SceneView.RepaintAll();
	            }
	            else
	            {
                    SimulateAllParticles(0f);
                    SceneView.RepaintAll();
	            }
	        }
	    }

	    void SimulateAllParticles(float time)
	    {
	        for (int i = 0; i < particles.Length; i++)
	        {
	            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	            particles[i].Clear(true);
	            particles[i].Simulate(time, true, true);
	        }
	    }

	    void ApplyPreviewSpeed()
	    {
	        if (particles == null || particleInitSpeeds == null)
	        {
	            return;
	        }

	        float speedMultiplier = Obj.PlaySpeed;
	        for (int i = 0; i < particles.Length; i++)
	        {
	            var main = particles[i].main;
	            float baseSpeed = particleInitSpeeds[i];
	            main.simulationSpeed = baseSpeed * speedMultiplier;
	        }
	    }
	}
	
#endif
}
