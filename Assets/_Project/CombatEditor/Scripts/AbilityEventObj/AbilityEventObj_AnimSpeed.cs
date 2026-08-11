using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

 namespace CombatEditor {	

    public enum AnimSpeedMode
    {
        Constant,
        Curve
    }
	
	[AbilityEvent]
	[CreateAssetMenu(menuName = "AbilityEvents/ AnimSpeed")]
	public class AbilityEventObj_AnimSpeed : AbilityEventObj
	{
	    public AnimSpeedMode Mode = AnimSpeedMode.Constant;
	    public float Speed = 1;
	    public float SpeedAtCurve0 = 0;
	    [FormerlySerializedAs("BaseSpeed")]
	    public float SpeedAtCurve1 = 1;
	    [MyAnimationCurve]
	    public AnimationCurve SpeedCurve = AnimationCurve.Linear(0, 1, 1, 1);

	    public float GetSpeedMultiplier(float normalizedTime)
	    {
	        if (Mode == AnimSpeedMode.Curve)
	        {
	            float curveValue = EvaluateCurve(normalizedTime);
	            return Mathf.Max(0, Mathf.Lerp(SpeedAtCurve0, SpeedAtCurve1, curveValue));
	        }

	        return Mathf.Max(0, Speed);
	    }

	    float EvaluateCurve(float normalizedTime)
	    {
	        EnsureCurve();
	        return Mathf.Clamp01(SpeedCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
	    }

	    void EnsureCurve()
	    {
	        if (SpeedCurve == null || SpeedCurve.length == 0)
	        {
	            SpeedCurve = AnimationCurve.Linear(0, 1, 1, 1);
	        }
	    }

	    public override EventTimeType GetEventTimeType()
	    {
	        return EventTimeType.EventRange;
	    }
	    public override AbilityEventEffect Initialize()
	    {
	        return new AbilityEventEffect_AnimSpeed(this);
	    }
	    public override AbilityEventPreview InitializePreview()
	    {
	        return new AbilityEventPreview_AnimSpeed(this);
	    }
	}
	
	public class AbilityEventEffect_AnimSpeed : AbilityEventEffect
	{
	
	    CharacterAnimSpeedModifier modifier;
	    public AbilityEventEffect_AnimSpeed(AbilityEventObj Obj) : base(Obj)
	    {
	        _EventObj = Obj;
	    }
	    public override void StartEffect()
	    {
	        base.StartEffect();
	        modifier = _combatController._animSpeedExecutor.AddAnimSpeedModifier(1);
	        modifier.SpeedScale = GetCurrentSpeedMultiplier(eve.GetEventStartTime());
	    }
	    public override void EffectRunning(float CurrentTimePercentage)
	    {
	        base.EffectRunning(CurrentTimePercentage);
	        if (modifier != null)
	        {
	            modifier.SpeedScale = GetCurrentSpeedMultiplier(CurrentTimePercentage);
	        }
	    }
	    public override void EndEffect()
	    {
	        base.EndEffect();
	        if (modifier != null)
	        {
	            _combatController._animSpeedExecutor.RemoveAnimSpeedModifier(modifier);
	            modifier = null;
	        }
	    }

	    float GetCurrentSpeedMultiplier(float currentTimePercentage)
	    {
	        AbilityEventObj_AnimSpeed animSpeedObj = (AbilityEventObj_AnimSpeed)_EventObj;
	        float eventDuration = eve.GetEventEndTime() - eve.GetEventStartTime();
	        if (eventDuration <= 0)
	        {
	            return animSpeedObj.GetSpeedMultiplier(0);
	        }

	        float normalizedTime = (currentTimePercentage - eve.GetEventStartTime()) / eventDuration;
	        return animSpeedObj.GetSpeedMultiplier(normalizedTime);
	    }
	}
}
