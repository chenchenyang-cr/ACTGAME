using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatEditor
{
    [AbilityEvent]
    [CreateAssetMenu(menuName = "AbilityEvents / SFX")]
    public class AbilityEventObj_SFX : AbilityEventObj
    {
        public List<AudioClip> clips;
        public float Volume = 1;
        [Range(0.1f, 3f)]
        public float PlaybackSpeed = 1f;
        public override EventTimeType GetEventTimeType()
        {
            return EventTimeType.EventTime;
        }
        public override AbilityEventEffect Initialize()
        {
            return new AbilityEventEffect_SFX(this);
        }
        public override AbilityEventPreview InitializePreview()
        {
            return new AbilityEventPreview_SFX(this);
        }
    }
    public class AbilityEventEffect_SFX : AbilityEventEffect
    {
        public AbilityEventEffect_SFX(AbilityEventObj Obj) : base(Obj)
        {
            _EventObj = Obj;
        }
        public override void StartEffect()
        {
            base.StartEffect();
            var sfx = (AbilityEventObj_SFX)_EventObj;
            var clip = sfx.clips[Random.Range(0, sfx.clips.Count)];
            clip.PlayClip(sfx.Volume, sfx.PlaybackSpeed);
        }
    }

    public static class AudioHelper
    {
        public static void PlayClip(this AudioClip clip, float Volume = 1, float playbackSpeed = 1f)
        {
            AudioSource source = new GameObject("AudioSource").AddComponent<AudioSource>();
            source.volume = Volume;
            source.pitch = Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            source.clip = clip;
            source.Play();
            GameObject.Destroy(source.gameObject, clip.length / source.pitch);
        }
    }

}
