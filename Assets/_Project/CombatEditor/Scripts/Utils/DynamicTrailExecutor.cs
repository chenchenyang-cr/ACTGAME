using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
{	
	
	public class DynamicTrailExecutor : MonoBehaviour
	{
	    public DynamicTrailGenerator trail;
	    public bool TrailEnabled = false;
	    private float visualFrameAccumulator;
	    public void StartTrail()
	    {
	        TrailEnabled = true;
	    }
	    public void StopTrail()
	    {
	        TrailEnabled = false;
	    }
	    private void Update()
	    {
	        visualFrameAccumulator += GlobalHitStopRuntime.CurrentVisualSpeed;
	        if (visualFrameAccumulator < 1f) return;
	        visualFrameAccumulator -= 1f;
	        if (TrailEnabled)
	        {
	            trail.UpdateTrailOnCurrentFrame();
	        }
	        else
	        {
	            trail.StopTrailSmoothly();
	            if (trail.QuadCount == 0)
	            {
	                Destroy(gameObject);
	            }
	        }
	    }
	}
	
}
