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
	
        private bool collisionInRange;
        private float currentTimePercentage;
        public bool CollisionVisible => InstantiatedObj != null && Obj.IsActive && Obj.EnableCollision && eve != null &&
            ((eve.Previewable && collisionInRange) ||
             CombatEditorTransformEditState.IsEditing(Obj, CombatEditorTransformEditState.EditMode.Position) ||
             CombatEditorTransformEditState.IsEditing(Obj, CombatEditorTransformEditState.EditMode.Rotation));
	    ParticleColliderPreviewHandle collisionHandle;
	    ParticleSystem[] particles;
	    float[] particleInitSpeeds;
	    float lastWorldSimulationTime = -1f;
	    bool worldEmissionStopped;
	
	    public override void InitPreview()
	    {
	        base.InitPreview();
	        if (InstantiatedObj != null && Obj.IsPerfectDodgeFeedback && Obj.ObjData.FollowNode)
	        {
	            foreach (ParticleSystem particle in InstantiatedObj.GetComponentsInChildren<ParticleSystem>(true))
	            {
	                var main = particle.main;
	                main.simulationSpace = Obj.LeaveParticlesInWorld
	                    ? ParticleSystemSimulationSpace.World
	                    : ParticleSystemSimulationSpace.Local;
	            }
	        }
	        if (InstantiatedObj != null)
	        {
	            ParticleSizeUtility.ApplySize(InstantiatedObj, Obj.SizeMultiplier);
	            ParticleCenterOffsetUtility.ApplyOffset(InstantiatedObj, Obj.CenterOffset);
	        }
            if (InstantiatedObj != null && Obj.EnableCollision && Obj.Collision != null)
            {
                Collider collider = Obj.Collision.CreateCollider(InstantiatedObj);
                // Preview is geometry only; it must not participate in physics or damage.
                collider.enabled = false;
                collider.gameObject.hideFlags = HideFlags.HideAndDontSave;
                collisionHandle = collider.gameObject.AddComponent<ParticleColliderPreviewHandle>();
                collisionHandle.ParticlePreview = this;
                collisionHandle._combatController = _combatController;
                collisionHandle._preview = this;
                collisionHandle.Init();
            }
            SetParticleData();
	    }
	    public void SetParticleData()
	    {
	        lastWorldSimulationTime = -1f;
	        worldEmissionStopped = false;
	        if (InstantiatedObj != null)
	        {
	            particles = InstantiatedObj.GetComponentsInChildren<ParticleSystem>(true);
	            if (particles != null)
	            {
	                particleInitSpeeds = new float[particles.Length];
	                for (int i = 0; i < particles.Length; i++)
	                {
	                    particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
	                    particles[i].Clear(false);
	                    particles[i].useAutoRandomSeed = false;
	                    particleInitSpeeds[i] = particles[i].main.simulationSpeed;
	                }
	            }
	        }
	    }

	    public void ResetParticlePreview()
	    {
	        lastWorldSimulationTime = -1f;
	        worldEmissionStopped = false;
	        if (particles == null)
	        {
	            return;
	        }

	        for (int i = 0; i < particles.Length; i++)
	        {
	            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
	            particles[i].Clear(false);
	            particles[i].Simulate(0f, false, true);
	        }
	        SceneView.RepaintAll();
	    }
	
	    /// <summary>
	    /// The particle's real time is influenced by timescale event.
	    /// </summary>
	    /// <param name="ScaledPercentage"></param>
        public override void PreviewRunning(float percentage)
        {
            currentTimePercentage = percentage;
            base.PreviewRunning(percentage);
        }

	    public override void PreviewRunningInScale(float ScaledPercentage)
	    {
	        base.PreviewRunningInScale(ScaledPercentage);
	        if (InstantiatedObj == null) return;
	        //Debug.Log("Simulate?");
            collisionInRange =
                ScaledPercentage >= StartTimeScaledPercentage &&
                Obj.IsCollisionDelayElapsed(currentTimePercentage, StartTimePercentage, AnimObj.Clip) &&
                (Obj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime ||
                 ScaledPercentage < EndTimeScaledPercentage);
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
	            if (Obj.LeaveParticlesInWorld &&
	                EventObj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventRange &&
	                ScaledPercentage >= StartTimeScaledPercentage)
	            {
	                IsInRange = true;
	            }
	            if(EventObj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime && ScaledPercentage >= StartTimeScaledPercentage)
	            {
	                IsInRange = true;
	            }
	            // Sample elapsed event time without advancing one extra animation frame.
	            if (IsInRange)
	            {
	                    float elapsed = Mathf.Max(0f, (ScaledPercentage - StartTimeScaledPercentage) * AnimLength);
	                    if (Obj.LeaveParticlesInWorld)
	                        SimulateWorldParticles(elapsed);
	                    else
	                        SimulateAllParticles(elapsed);
                    SceneView.RepaintAll();
	            }
	            else
	            {
                    // Simulate(0, restart:true) can emit a burst at time zero.
                    // Before the event, clear without restarting the system.
                    for (int i = 0; i < particles.Length; i++)
                        particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
	                    lastWorldSimulationTime = -1f;
	                    worldEmissionStopped = false;
                    SceneView.RepaintAll();
	            }
	        }
	    }

	    void SimulateAllParticles(float time)
	    {
	        for (int i = 0; i < particles.Length; i++)
	        {
	            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
	            particles[i].Clear(false);
	            particles[i].Simulate(time, false, true);
	        }
	    }

	    void SimulateWorldParticles(float time)
	    {
	        float emissionEnd = Mathf.Max(0f,
	            (EndTimeScaledPercentage - StartTimeScaledPercentage) * AnimLength);
	        if (lastWorldSimulationTime < 0f || time < lastWorldSimulationTime)
	        {
	            worldEmissionStopped = false;
	            for (int i = 0; i < particles.Length; i++)
	            {
	                particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
	                particles[i].Simulate(Mathf.Min(time, emissionEnd), false, true);
	            }
	        }
	        else if (time > lastWorldSimulationTime && !worldEmissionStopped)
	        {
	            float delta = Mathf.Min(time, emissionEnd) - lastWorldSimulationTime;
	            for (int i = 0; i < particles.Length; i++)
	                if (delta > 0f) particles[i].Simulate(delta, false, false);
	        }
	        if (time >= emissionEnd && !worldEmissionStopped)
	        {
	            for (int i = 0; i < particles.Length; i++)
	                particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
	            worldEmissionStopped = true;
	        }
	        if (worldEmissionStopped && time > Mathf.Max(lastWorldSimulationTime, emissionEnd))
	        {
	            float drain = time - Mathf.Max(lastWorldSimulationTime, emissionEnd);
	            for (int i = 0; i < particles.Length; i++)
	                particles[i].Simulate(drain, false, false);
	        }
	        lastWorldSimulationTime = time;
	}

        public override void DestroyPreview()
        {
            collisionInRange = false;
            if (collisionHandle != null)
                collisionHandle.SelfDestroy();
            collisionHandle = null;
            base.DestroyPreview();
        }
	    void ApplyPreviewSpeed()
	    {
	        if (particles == null || particleInitSpeeds == null)
	        {
	            return;
	        }

	        float speedMultiplier = Mathf.Max(0.01f, Obj.PlaySpeed);
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
