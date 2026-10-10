using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class Slash270Controller : MonoBehaviour
{
    [Header("统一时间（秒）")]
    [Min(0.01f)] public float growthTime=0.10f;
    [Min(0)] public float holdTime=0.12f;
    [Min(0.01f)] public float dissolveTime=0.28f;
    [Header("生长：横轴时间比例，纵轴整体显现比例")]
    public AnimationCurve growthCurve=new AnimationCurve(new Keyframe(0,0,1.2f,1.2f),new Keyframe(1,1,0.8f,0.8f));
    [Header("旋转速度：横轴为粒子生命周期比例，纵轴为原有速度的倍数")]
    public AnimationCurve rotationSpeedCurve=AnimationCurve.Linear(0,1,1,1);
    [Header("亮度：横轴为整个效果的时间比例")]
    public AnimationCurve brightnessCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(0.06f,1),new Keyframe(0.4f,1.15f),new Keyframe(0.8f,0.7f),new Keyframe(1,0));
    [Min(0)] public float brightness=1;
    [Range(0,1)] public float lineWidth=1;
    public bool playOnEnable=true;
    public bool loop;
    public float TotalTime=>growthTime+holdTime+dissolveTime;
    public bool IsPlaying=>running;
    public float Elapsed=>elapsed;

    ParticleSystem root;
    ParticleSystem[] systems;
    ParticleSystemRenderer[] renderers;
    Texture2D lookup;
    MaterialPropertyBlock block;
    sealed class ParticleState
    {
        public ParticleSystem system;
        public float duration;
        public bool loop, playOnAwake;
        public ParticleSystem.MinMaxCurve lifetime;
        public float rotationYMultiplier;
        public bool controlsRotation;
        readonly bool controlsLifetime;
        static ParticleSystem.MinMaxCurve CopyCurve(ParticleSystem.MinMaxCurve value) {
            if(value.curveMin!=null)value.curveMin=new AnimationCurve(value.curveMin.keys){preWrapMode=value.curveMin.preWrapMode,postWrapMode=value.curveMin.postWrapMode};
            if(value.curveMax!=null)value.curveMax=new AnimationCurve(value.curveMax.keys){preWrapMode=value.curveMax.preWrapMode,postWrapMode=value.curveMax.postWrapMode};
            return value;
        }
        public ParticleState(ParticleSystem ps) {
            system=ps;
            controlsLifetime=ps.main.maxParticles==1;
            var main=ps.main;duration=main.duration;loop=main.loop;playOnAwake=main.playOnAwake;lifetime=CopyCurve(main.startLifetime);
            rotationYMultiplier=ps.rotationOverLifetime.yMultiplier;

        }
        public void Restore() {
            if(!system)return;
            var main=system.main;main.duration=duration;main.loop=loop;main.playOnAwake=playOnAwake;
            if(controlsLifetime)main.startLifetime=lifetime;
            if(controlsRotation) {var rotation=system.rotationOverLifetime;rotation.yMultiplier=rotationYMultiplier;}
            // Restore only fields actually controlled here. Never deserialize a whole system:
            // users may have edited size, color, velocity or other authored parameters while previewing.

        }
    }
    ParticleState[] authoredState;
    bool running;
    float elapsed;
    const int Samples=128;

    void OnEnable() {if(Application.isPlaying&&playOnEnable)Play();}
    void OnDisable() {Stop();Release();}
    void OnDestroy() {Release();}
    void OnValidate() {
        growthTime=Mathf.Max(0.01f,growthTime);holdTime=Mathf.Max(0,holdTime);
        dissolveTime=Mathf.Max(0.01f,dissolveTime);brightness=Mathf.Max(0,brightness);lineWidth=Mathf.Clamp01(lineWidth);
    }
    void Update() {
        if(!Application.isPlaying||!running)return;
        Advance(Time.deltaTime);
    }
    void Advance(float delta) {
        elapsed+=delta;
        if(elapsed>=TotalTime) {if(loop)Play();else Stop();return;}
        SetRotationSpeed(elapsed);
        SetVisuals(elapsed);
    }
    void Cache() {
        if(block==null)block=new MaterialPropertyBlock();
        if(root)return;
        root=GetComponent<ParticleSystem>();systems=GetComponentsInChildren<ParticleSystem>(true);
        renderers=GetComponentsInChildren<ParticleSystemRenderer>(true);
        ClearRenderingOverrides();
    }
    public string ValidateSettings() {
        if(!float.IsFinite(TotalTime)||growthTime<=0||holdTime<0||dissolveTime<=0)return "时间必须有效，生长与消散时间需大于 0。";
        if(growthCurve==null||growthCurve.length<2)return "请设置从 (0,0) 到 (1,1) 的生长曲线。";
        if(Mathf.Abs(growthCurve.Evaluate(0))>0.001f||Mathf.Abs(growthCurve.Evaluate(1)-1)>0.001f)return "生长曲线必须从 (0,0) 到 (1,1)。";
        if(rotationSpeedCurve==null||rotationSpeedCurve.length==0)return "请设置旋转速度曲线。";
        float previous=-0.001f;
        for(int i=0;i<=Samples;i++) {
            float t=i/(float)Samples;
            float v=growthCurve.Evaluate(t);
            if(!float.IsFinite(v)||v<previous-0.0001f||v<0||v>1.001f)return "生长曲线需在 0～1 内单调上升。";previous=v;
            if(brightnessCurve==null||!float.IsFinite(brightnessCurve.Evaluate(t)))return "亮度曲线无效。";
            float speed=rotationSpeedCurve.Evaluate(t);
            if(!float.IsFinite(speed)||speed<0)return "旋转速度曲线需为非负的有效数值。";
        }
        return null;
    }
    public void ApplySettings() {
        string error=ValidateSettings();if(error!=null)throw new ArgumentException(error);
        if(authoredState!=null)Stop();
        Cache();running=false;root.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        authoredState=new ParticleState[systems.Length];for(int i=0;i<systems.Length;i++)authoredState[i]=new ParticleState(systems[i]);
        if(!lookup)lookup=new Texture2D(1024,1,TextureFormat.RFloat,false,true){name="Slash Instance Growth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[1024];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(growthCurve.Evaluate(i/1023f),0,0,1);
        lookup.SetPixels(pixels);lookup.Apply(false,false);
        foreach(var ps in systems) {
            var main=ps.main;main.duration=TotalTime;main.loop=false;main.playOnAwake=false;
            if(main.maxParticles==1) main.startLifetime=TotalTime;
        }
        if(!HasDefaultRotationSpeed())for(int i=0;i<systems.Length;i++)
            authoredState[i].controlsRotation=systems[i].rotationOverLifetime.enabled;
        SetRotationSpeed(0);
        // Applying settings alone must not leave native Particle System preview black.
        elapsed=0;SetVisuals(0,1);
    }
    bool HasDefaultRotationSpeed() {
        if(rotationSpeedCurve.length!=2)return false;
        var first=rotationSpeedCurve.keys[0];var last=rotationSpeedCurve.keys[1];
        return first.time==0&&last.time==1&&first.value==1&&last.value==1
            &&first.outTangent==0&&last.inTangent==0;
    }
    void SetRotationSpeed(float seconds) {
        if(authoredState==null)return;
        float speed=rotationSpeedCurve.Evaluate(Mathf.Clamp01(seconds/TotalTime));
        for(int i=0;i<systems.Length;i++) {
            if(!authoredState[i].controlsRotation)continue;
            var rotation=systems[i].rotationOverLifetime;
            rotation.yMultiplier=authoredState[i].rotationYMultiplier*speed;
        }
    }
    public void Play() {ApplySettings();elapsed=0;running=true;root.Play(true);SetVisuals(0);}
    public void Stop() {
        running=false;elapsed=0;if(root)root.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(authoredState!=null) {foreach(var state in authoredState)state.Restore();authoredState=null;}
        ClearRenderingOverrides();
    }
    public void ClearRenderingOverrides() {
        // These renderers belong to this effect. Never restore legacy black tint overrides.
        var targets=renderers??GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach(var renderer in targets)if(renderer)renderer.SetPropertyBlock(null);
    }
    // Used by the editor preview and deterministic verification; no global scene timing changes.
    public void PreviewAt(float seconds) {
        if(authoredState==null)ApplySettings();
        Cache();elapsed=Mathf.Clamp(seconds,0,TotalTime);running=false;
        if(HasDefaultRotationSpeed())root.Simulate(elapsed,true,true,false);
        else {
            root.Simulate(0,true,true,false);
            float simulated=0;
            while(simulated<elapsed) {
                float step=Mathf.Min(1f/120f,elapsed-simulated);
                SetRotationSpeed(simulated+step*0.5f);
                root.Simulate(step,true,false,false);
                simulated+=step;
            }
            SetRotationSpeed(elapsed);
        }
        SetVisuals(elapsed);
        if(seconds>=TotalTime)Stop();
    }
    public void RefreshLineWidth() {
        Cache();
        foreach(var renderer in renderers) {
            if(!renderer||!renderer.sharedMaterial||!renderer.sharedMaterial.HasProperty("_ControllerLineWidth"))continue;
            renderer.GetPropertyBlock(block);block.SetFloat("_ControllerLineWidth",lineWidth);renderer.SetPropertyBlock(block);
        }
    }
    void SetVisuals(float seconds,float fixedIntensity=-1) {
        float intensity=fixedIntensity>=0?fixedIntensity:Mathf.Max(0,brightnessCurve.Evaluate(Mathf.Clamp01(seconds/TotalTime)))*brightness;
        foreach(var renderer in renderers) {
            if(!renderer||!renderer.sharedMaterial)continue;
            block.Clear();
            block.SetTexture("_GrowthCurve",lookup);block.SetFloat("_GrowthFraction",growthTime/TotalTime);
            block.SetFloat("_DissolveStart",(growthTime+holdTime)/TotalTime);block.SetFloat("_DissolveEnd",1);
            block.SetFloat("_ControllerBrightness",intensity);
            block.SetFloat("_ControllerLineWidth",lineWidth);
            renderer.SetPropertyBlock(block);
        }
    }
    void Release() {
        ClearRenderingOverrides();
        if(lookup) {if(Application.isPlaying)Destroy(lookup);else DestroyImmediate(lookup);lookup=null;}
        root=null;systems=null;renderers=null;
    }
}
