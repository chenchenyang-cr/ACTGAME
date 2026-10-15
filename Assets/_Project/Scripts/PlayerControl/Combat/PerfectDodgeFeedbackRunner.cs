using System.Collections.Generic;
using CombatEditor;
using UnityEngine;

public sealed class PerfectDodgeFeedbackRunner
{
    private readonly List<TrackEntry> entries = new();
    private readonly CombatController controller;
    private float lastTickTime;
    private float elapsedSeconds;
    private float feedbackStart;
    private float clipLength;
    private float endTime;
    private Vector3 hitPoint;
    private Vector3 attackDirection;
    private bool playing;
    private bool sampledStart;

    public PerfectDodgeFeedbackRunner(CombatController controller) => this.controller = controller;

    public void Play(AbilityScriptableObject ability, Vector3 point, Vector3 direction)
    {
        Stop();
        if (ability == null || ability.Clip == null || controller == null) return;
        clipLength = Mathf.Max(0.01f, ability.Clip.length);
        endTime = 0f;
        feedbackStart = float.PositiveInfinity;
        elapsedSeconds = 0f;
        sampledStart = false;
        lastTickTime = Time.unscaledTime;
        hitPoint = point;
        attackDirection = direction;
        foreach (AbilityEvent timelineEvent in ability.events)
        {
            if (timelineEvent?.Obj is not IPerfectDodgeFeedbackTrack feedback ||
                !feedback.IsPerfectDodgeFeedback || !timelineEvent.Obj.IsActive) continue;
            if (timelineEvent.Obj is not AbilityEventObj_PostFxTrack &&
                timelineEvent.Obj is not AbilityEventObj_Particles &&
                timelineEvent.Obj is not AbilityEventObj_CameraShake &&
                timelineEvent.Obj is not AbilityEventObj_CameraShot &&
                timelineEvent.Obj is not AbilityEventObj_GlobalHitStop &&
                timelineEvent.Obj is not AbilityEventObj_CharacterFade &&
                timelineEvent.Obj is not AbilityEventObj_CharacterEcho &&
                timelineEvent.Obj is not AbilityEventObj_CharacterAfterimage) continue;
            AbilityEventEffect effect = timelineEvent.Obj.Initialize();
            effect.eve = timelineEvent;
            effect.AnimObj = ability;
            effect._combatController = controller;
            if (effect is AbilityEventEffect_Particles particleEffect)
            {
                particleEffect.HasPerfectDodgeHitPoint = true;
                particleEffect.PerfectDodgeHitPoint = hitPoint;
                particleEffect.PerfectDodgeAttackDirection = attackDirection;
            }
            if (effect is AbilityEventEffect_CameraShake shakeEffect)
                shakeEffect.ForceDirect = true;
            entries.Add(new TrackEntry(timelineEvent, effect));
            feedbackStart = Mathf.Min(feedbackStart, timelineEvent.GetEventStartTime());
            float eventEnd = timelineEvent.Obj is AbilityEventObj_CameraShot shot
                ? timelineEvent.GetEventStartTime() + shot.Duration / clipLength
                : timelineEvent.Obj is AbilityEventObj_GlobalHitStop globalStop
                    ? timelineEvent.GetEventStartTime() +
                      globalStop.DurationFrames / (CombatTimeline.FramesPerSecond * clipLength)
                : timelineEvent.Obj is AbilityEventObj_CharacterAfterimage afterimage
                    ? timelineEvent.GetEventStartTime() + afterimage.Lifetime / clipLength
                : timelineEvent.Obj is AbilityEventObj_Particles particles &&
                  particles.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime
                    ? timelineEvent.GetEventStartTime() + 0.01f / clipLength
                    : timelineEvent.GetEventEndTime();
            endTime = Mathf.Max(endTime, eventEnd);
        }
        if (entries.Count == 0) return;
        playing = true;
        Tick();
    }

    public void Tick()
    {
        if (!playing) return;
        float now = Time.unscaledTime;
        // A long frame must not skip an entire short feedback range before it renders.
        if (sampledStart)
            elapsedSeconds += Mathf.Min(Mathf.Max(0f, now - lastTickTime), 1f / 30f);
        else sampledStart = true;
        lastTickTime = now;
        float time = feedbackStart + elapsedSeconds / clipLength;
        foreach (TrackEntry entry in entries)
        {
            float start = entry.Event.GetEventStartTime();
            float end = entry.Event.GetEventEndTime();
            bool pointEvent = entry.Event.Obj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime;
            if (pointEvent)
            {
                if (!entry.Fired && time >= start && start <= 1f)
                {
                    entry.Fired = true;
                    entry.Effect.StartEffect();
                }
                if (entry.Fired && entry.Effect.IsRunning)
                {
                    entry.Effect.EffectRunning();
                    entry.Effect.EffectRunning(time);
                }
                continue;
            }

            bool inside = time >= start && time < end;
            if (inside && !entry.Effect.IsRunning) entry.Effect.StartEffect();
            if (inside)
            {
                entry.Effect.EffectRunning();
                entry.Effect.EffectRunning(time);
            }
            else if (entry.Effect.IsRunning)
            {
                if (entry.Effect is AbilityEventEffect_Particles particleEffect &&
                    entry.Event.Obj is AbilityEventObj_Particles particles &&
                    particles.LeaveParticlesInWorld)
                    particleEffect.ReleaseOnAnimationChange();
                else entry.Effect.EndEffect();
            }
        }
        if (time >= endTime && elapsedSeconds > 0f) Stop();
    }

    public void Stop()
    {
        playing = false;
        foreach (TrackEntry entry in entries)
            if (entry.Effect.IsRunning)
            {
                if (entry.Effect is AbilityEventEffect_Particles particleEffect &&
                    (entry.Event.Obj.GetEventTimeType() == AbilityEventObj.EventTimeType.EventTime ||
                     ((AbilityEventObj_Particles)entry.Event.Obj).LeaveParticlesInWorld))
                    particleEffect.ReleaseOnAnimationChange();
                else entry.Effect.EndEffect();
            }
        entries.Clear();
    }

    private sealed class TrackEntry
    {
        public readonly AbilityEvent Event;
        public readonly AbilityEventEffect Effect;
        public bool Fired;
        public TrackEntry(AbilityEvent timelineEvent, AbilityEventEffect effect)
        {
            Event = timelineEvent;
            Effect = effect;
        }
    }
}
