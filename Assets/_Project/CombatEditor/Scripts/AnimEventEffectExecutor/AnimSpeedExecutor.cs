using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 namespace CombatEditor
{
    public struct HitStopFadeSettings
    {
        public bool FadeIn;
        public int FadeInFrames;
        public AnimationCurve FadeInCurve;
        public bool FadeOut;
        public int FadeOutFrames;
        public AnimationCurve FadeOutCurve;
    }

    [System.Serializable]
	public class CharacterAnimSpeedModifier
	{
	    public float SpeedScale;
	    public float MaxTime;
	    public float StartTime;
	    public bool SelfDestroy;
	    public AnimationCurve SpeedCurve;
	    public bool UseUnscaledTime;
	    public HitStopFadeSettings HitStopFades;
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
	        if (HitStopFades.FadeIn || HitStopFades.FadeOut)
	        {
	            float elapsed = Mathf.Clamp(CurrentTime - StartTime, 0f, MaxTime);
	            float fadeInDuration = HitStopFades.FadeIn
	                ? Mathf.Max(0, HitStopFades.FadeInFrames) / CombatTimeline.FramesPerSecond : 0f;
	            float fadeOutDuration = HitStopFades.FadeOut
	                ? Mathf.Max(0, HitStopFades.FadeOutFrames) / CombatTimeline.FramesPerSecond : 0f;
	            float fadeTotal = fadeInDuration + fadeOutDuration;
	            if (fadeTotal > MaxTime)
	            {
	                float scale = MaxTime / fadeTotal;
	                fadeInDuration *= scale;
	                fadeOutDuration *= scale;
	            }

	            float strength = 1f;
	            if (fadeInDuration > 0f && elapsed < fadeInDuration)
	            {
	                float progress = elapsed / fadeInDuration;
	                strength = HitStopFades.FadeInCurve != null && HitStopFades.FadeInCurve.length > 0
	                    ? Mathf.Clamp01(HitStopFades.FadeInCurve.Evaluate(progress)) : progress;
	            }
	            if (fadeOutDuration > 0f && elapsed >= MaxTime - fadeOutDuration)
	            {
	                float progress = (elapsed - (MaxTime - fadeOutDuration)) / fadeOutDuration;
	                float outgoingStrength = HitStopFades.FadeOutCurve != null && HitStopFades.FadeOutCurve.length > 0
	                    ? Mathf.Clamp01(HitStopFades.FadeOutCurve.Evaluate(progress)) : 1f - progress;
	                strength = Mathf.Min(strength, outgoingStrength);
	            }
	            return Mathf.Lerp(1f, Mathf.Clamp01(SpeedScale), strength);
	        }
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
	        if (_pendingHitStopFrames > 0 && Time.frameCount >= _hitStopStartFrame)
	        {
	            StartHitStop(_pendingHitStopFrames, _pendingHitStopSpeed, _pendingHitStopFades);
	            _pendingHitStopFrames = 0;
	        }
	        _combatController._animator.speed = GetCurrentSpeedModifier() *
	            GlobalHitStopRuntime.CurrentVisualSpeed;
	    }
	
	    public List<CharacterAnimSpeedModifier> _animSpeedModifiers = new List<CharacterAnimSpeedModifier>();
	    private CharacterAnimSpeedModifier _hitSpeedModifier;
	    private CharacterAnimSpeedModifier _hitStopModifier;
	    private int _pendingHitStopFrames;
	    private float _pendingHitStopSpeed;
	    private HitStopFadeSettings _pendingHitStopFades;
	    private int _hitStopStartFrame;

	    public float CurrentHitStopSpeedScale => _hitStopModifier != null &&
	        _hitStopModifier.CurrentTime - _hitStopModifier.StartTime < _hitStopModifier.MaxTime
	            ? _hitStopModifier.EvaluateSpeedScale()
	            : 1f;
	    public bool IsHitStopActive => _pendingHitStopFrames > 0 ||
	        _hitStopModifier != null &&
	        _hitStopModifier.CurrentTime - _hitStopModifier.StartTime < _hitStopModifier.MaxTime;

	    public void PlayHitStop(int frames, bool delayOneFrame = false, float animationSpeed = 0f,
	        HitStopFadeSettings fades = default)
	    {
	        if (frames <= 0)
	            return;
	        animationSpeed = Mathf.Clamp01(animationSpeed);

	        if (delayOneFrame)
	        {
	            // Repeated hits refresh the duration without postponing the pending start.
	            if (_pendingHitStopFrames == 0)
	                _hitStopStartFrame = Time.frameCount + 1;
	            _pendingHitStopFrames = frames;
	            _pendingHitStopSpeed = animationSpeed;
	            _pendingHitStopFades = fades;
	            return;
	        }

	        _pendingHitStopFrames = 0;
	        StartHitStop(frames, animationSpeed, fades);
	        Execute();
	    }

	    private void StartHitStop(int frames, float animationSpeed, HitStopFadeSettings fades)
	    {
	        if (_hitStopModifier != null)
	            _animSpeedModifiers.Remove(_hitStopModifier);

	        // Count real time independently of the frozen animation and game time scale.
	        _hitStopModifier = new CharacterAnimSpeedModifier(animationSpeed,
	            frames / CombatTimeline.FramesPerSecond)
	        {
	            UseUnscaledTime = true,
	            StartTime = Time.unscaledTime,
	            HitStopFades = fades
	        };
	        _animSpeedModifiers.Add(_hitStopModifier);
	    }
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
	                if (modifier == _hitStopModifier)
	                    _hitStopModifier = null;
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
