using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
{
    [System.Serializable]
	public class CharacterAnimSpeedModifier
	{
	    public float SpeedScale;
	    public float MaxTime;
	    public float StartTime;
	    public bool SelfDestroy;
	    public AnimationCurve SpeedCurve;
	    public bool UseUnscaledTime;
	    public CharacterAnimSpeedModifier(float speedScale, float maxTime)
	    {
	        SpeedScale = speedScale;
	        MaxTime = maxTime;
	        StartTime = Time.time;
	        SelfDestroy = true;
	    }
	
	    public CharacterAnimSpeedModifier(float speedScale)
	    {
	        SpeedScale = speedScale;
	        StartTime = Time.time;
	        SelfDestroy = false;
	    }

	    public CharacterAnimSpeedModifier(AnimationCurve speedCurve, float maxTime,
	        bool useUnscaledTime)
	    {
	        SpeedScale = 1f;
	        SpeedCurve = speedCurve;
	        MaxTime = Mathf.Max(0.01f, maxTime);
	        UseUnscaledTime = useUnscaledTime;
	        StartTime = useUnscaledTime ? Time.unscaledTime : Time.time;
	        SelfDestroy = true;
	    }

	    public float CurrentTime => UseUnscaledTime ? Time.unscaledTime : Time.time;

	    public float EvaluateSpeedScale()
	    {
	        if (SpeedCurve == null || SpeedCurve.length == 0)
	            return Mathf.Max(0f, SpeedScale);

	        float normalizedTime = Mathf.Clamp01((CurrentTime - StartTime) / MaxTime);
	        return Mathf.Max(0f, SpeedCurve.Evaluate(normalizedTime));
	    }
	
	}
	
	public class AnimSpeedExecutor
	{
	    public AnimSpeedExecutor(CombatController _controller) { _combatController = _controller; }
	    public CombatController _combatController;
	    public void Execute()
	    {
	        _combatController._animator.speed = GetCurrentSpeedModifier();
	    }
	
	    public List<CharacterAnimSpeedModifier> _animSpeedModifiers = new List<CharacterAnimSpeedModifier>();
	    private CharacterAnimSpeedModifier _hitSpeedModifier;
	    public void AddSpeedModifiers(float SpeedScale, float time)
	    {
	        _animSpeedModifiers.Add(new CharacterAnimSpeedModifier(SpeedScale, time));
	    }
	    public CharacterAnimSpeedModifier AddAnimSpeedModifier(float SpeedScale)
	    {
	        CharacterAnimSpeedModifier modifier = new CharacterAnimSpeedModifier(SpeedScale);
	        _animSpeedModifiers.Add(modifier);
	        return modifier;
	    }
	    public void RemoveAnimSpeedModifier(CharacterAnimSpeedModifier modifier)
	    {
	        _animSpeedModifiers.Remove(modifier);
	    }
	    public void PlayHitSpeedCurve(AnimationCurve speedCurve, float duration,
	        bool useUnscaledTime)
	    {
	        if (_hitSpeedModifier != null)
	            _animSpeedModifiers.Remove(_hitSpeedModifier);

	        _hitSpeedModifier = new CharacterAnimSpeedModifier(speedCurve, duration,
	            useUnscaledTime);
	        _animSpeedModifiers.Add(_hitSpeedModifier);
	    }
	    public float GetCurrentSpeedModifier()
	    {
	        //var LowestSpeed = _animSpeedModifiers.OrderBy(t => t.SpeedScale).Take(1).ToArray();
	        //if(LowestSpeed.Length > 0)
	        //{
	        //    return LowestSpeed[0].SpeedScale;
	        //}
	        float Speed = 1;
	        for (int i = 0; i < _animSpeedModifiers.Count; i++)
	        {
	            CharacterAnimSpeedModifier modifier = _animSpeedModifiers[i];
	            if (modifier.SelfDestroy &&
	                modifier.CurrentTime - modifier.StartTime >= modifier.MaxTime)
	            {
	                if (modifier == _hitSpeedModifier)
	                    _hitSpeedModifier = null;
	                _animSpeedModifiers.RemoveAt(i);
	                i -= 1;
	                continue;
	            }
	            Speed *= modifier.EvaluateSpeedScale();
	        }
	        return Speed;
	    }
	
	}
}
