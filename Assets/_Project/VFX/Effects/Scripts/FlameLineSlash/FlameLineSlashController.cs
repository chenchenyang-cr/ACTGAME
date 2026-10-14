using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class FlameLineSlashController : MonoBehaviour
{
    [Header("统一时间（秒）")]
    [Min(0.01f)] public float growthTime=0.15f;
    [Min(0)] public float holdTime=0.25f;
    [Min(0.01f)] public float dissolveTime=0.6f;
    [Header("生长：横轴时间比例，纵轴长度比例")]
    public AnimationCurve growthCurve=new AnimationCurve(new Keyframe(0,0,1.2f,1.2f),new Keyframe(1,1,0.8f,0.8f));
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
        public ParticleSystem.MinMaxCurve lifetime, rate, radiusSpeed;
        public ParticleSystem.Burst[] bursts;
        readonly bool controlsLifetime, controlsGrowth, controlsDissolve;
        static ParticleSystem.MinMaxCurve CopyCurve(ParticleSystem.MinMaxCurve value) {
            if(value.curveMin!=null)value.curveMin=new AnimationCurve(value.curveMin.keys){preWrapMode=value.curveMin.preWrapMode,postWrapMode=value.curveMin.postWrapMode};
            if(value.curveMax!=null)value.curveMax=new AnimationCurve(value.curveMax.keys){preWrapMode=value.curveMax.preWrapMode,postWrapMode=value.curveMax.postWrapMode};
            return value;
        }
        public ParticleState(ParticleSystem ps) {
            system=ps;
            controlsLifetime=ps.main.maxParticles==1;
            controlsGrowth=ps.name=="04_GrowthParticles";
            controlsDissolve=ps.name=="05_DissolveParticles";
            var main=ps.main;duration=main.duration;loop=main.loop;playOnAwake=main.playOnAwake;lifetime=CopyCurve(main.startLifetime);
            var emission=ps.emission;rate=CopyCurve(emission.rateOverTime);bursts=new ParticleSystem.Burst[emission.burstCount];emission.GetBursts(bursts);radiusSpeed=CopyCurve(ps.shape.radiusSpeed);
        }
        public void Restore() {
            if(!system)return;
            var main=system.main;main.duration=duration;main.loop=loop;main.playOnAwake=playOnAwake;
            if(controlsLifetime)main.startLifetime=lifetime;
            // Restore only fields actually controlled here. Never deserialize a whole system:
            // users may have edited size, color, velocity or spark lifetime while previewing.
            var emission=system.emission;
            if(controlsGrowth) {emission.rateOverTime=rate;var shape=system.shape;shape.radiusSpeed=radiusSpeed;}
            if(controlsGrowth||controlsDissolve)emission.SetBursts(bursts);
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
        Advance(Time.deltaTime * CombatEditor.GlobalHitStopRuntime.GetVisualSpeed(this));
    }
    void Advance(float delta) {
        elapsed+=delta;
        if(elapsed>=TotalTime) {if(loop)Play();else Stop();return;}
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
        float previous=-0.001f;
        for(int i=0;i<=Samples;i++) {
            float v=growthCurve.Evaluate(i/(float)Samples);
            if(!float.IsFinite(v)||v<previous-0.0001f||v<0||v>1.001f)return "生长曲线需在 0～1 内单调上升。";previous=v;
            if(brightnessCurve==null||!float.IsFinite(brightnessCurve.Evaluate(i/(float)Samples)))return "亮度曲线无效。";
        }
        return null;
    }
    public void ApplySettings() {
        string error=ValidateSettings();if(error!=null)throw new ArgumentException(error);
        if(authoredState!=null)Stop();
        Cache();running=false;root.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        authoredState=new ParticleState[systems.Length];for(int i=0;i<systems.Length;i++)authoredState[i]=new ParticleState(systems[i]);
        float authoredLifetime=root.main.startLifetime.constant;
        var ribbonMaterial=root.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        float authoredGrowth=Mathf.Max(0.001f,ribbonMaterial.GetFloat("_GrowthFraction")*authoredLifetime);
        float authoredDissolveStart=ribbonMaterial.GetFloat("_DissolveStart")*authoredLifetime;
        float authoredDissolveLength=Mathf.Max(0.001f,authoredLifetime-authoredDissolveStart);
        if(!lookup)lookup=new Texture2D(1024,1,TextureFormat.RFloat,false,true){name="Slash Instance Growth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[1024];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(growthCurve.Evaluate(i/1023f),0,0,1);
        lookup.SetPixels(pixels);lookup.Apply(false,false);
        foreach(var ps in systems) {
            var main=ps.main;main.duration=TotalTime;main.loop=false;main.playOnAwake=false;
            if(main.maxParticles==1) main.startLifetime=TotalTime;
        }
        var growth=transform.Find("04_GrowthParticles").GetComponent<ParticleSystem>();
        ParticleState growthState=Array.Find(authoredState,x=>x.system==growth);
        var speedKeys=new List<Keyframe>();float end=growthTime/TotalTime;
        for(int i=0;i<Samples;i++) {
            float slope=(growthCurve.Evaluate((i+1)/(float)Samples)-growthCurve.Evaluate(i/(float)Samples))*Samples;
            speedKeys.Add(new Keyframe(i/(float)Samples*end,slope*growth.shape.radius/growthTime,0,float.PositiveInfinity));
        }
        speedKeys.Add(new Keyframe(end,0));speedKeys.Add(new Keyframe(1,0));
        var shape=growth.shape;shape.radiusSpeed=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(speedKeys.ToArray()));
        var emission=growth.emission;
        // Keep the authored rate and burst counts; only remap their time window.
        var minimum=new List<Keyframe>();var maximum=new List<Keyframe>();
        for(int i=0;i<Samples;i++) {
            float u=i/(float)Samples;
            float authoredTime=u*authoredGrowth/growthState.duration;
            minimum.Add(new Keyframe(u*end,growthState.rate.Evaluate(authoredTime,0)));
            maximum.Add(new Keyframe(u*end,growthState.rate.Evaluate(authoredTime,1)));
        }
        var lo=minimum[minimum.Count-1];lo.outTangent=float.PositiveInfinity;minimum[minimum.Count-1]=lo;
        var hi=maximum[maximum.Count-1];hi.outTangent=float.PositiveInfinity;maximum[maximum.Count-1]=hi;
        minimum.Add(new Keyframe(end,0,float.PositiveInfinity,0));minimum.Add(new Keyframe(1,0));
        maximum.Add(new Keyframe(end,0,float.PositiveInfinity,0));maximum.Add(new Keyframe(1,0));
        bool randomRate=growthState.rate.mode==ParticleSystemCurveMode.TwoConstants||growthState.rate.mode==ParticleSystemCurveMode.TwoCurves;
        emission.rateOverTime=randomRate?new ParticleSystem.MinMaxCurve(1,new AnimationCurve(minimum.ToArray()),new AnimationCurve(maximum.ToArray())):new ParticleSystem.MinMaxCurve(1,new AnimationCurve(maximum.ToArray()));
        var growthBursts=(ParticleSystem.Burst[])growthState.bursts.Clone();
        for(int i=0;i<growthBursts.Length;i++) {growthBursts[i].time*=growthTime/authoredGrowth;growthBursts[i].repeatInterval*=growthTime/authoredGrowth;}
        emission.SetBursts(growthBursts);
        var sparks=transform.Find("05_DissolveParticles").GetComponent<ParticleSystem>();
        var sparkState=Array.Find(authoredState,x=>x.system==sparks);
        var dissolveBursts=(ParticleSystem.Burst[])sparkState.bursts.Clone();
        for(int i=0;i<dissolveBursts.Length;i++) {
            float phase=Mathf.Clamp01((dissolveBursts[i].time-authoredDissolveStart)/authoredDissolveLength);
            dissolveBursts[i].time=growthTime+holdTime+phase*dissolveTime;
            dissolveBursts[i].repeatInterval*=dissolveTime/authoredDissolveLength;
        }
        var burst=sparks.emission;burst.SetBursts(dissolveBursts);
        // Applying settings alone must not leave native Particle System preview black.
        elapsed=0;SetVisuals(0,1);
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
        root.Simulate(elapsed,true,true,false);SetVisuals(elapsed);
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
