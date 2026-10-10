Shader "Effects/Slash270Particle"
{
    Properties
    {
        [HDR] _Tint ("Tint / Brightness", Color) = (1,1,1,1)
        _Softness ("Edge Softness", Range(1,12)) = 4
        [Toggle] _InnerFill ("Fade Toward Center", Float) = 0
        _InwardFade ("Inward Fade Power", Range(0.5,4)) = 1.65
        _DistortionStrength ("Air Distortion Strength", Range(0,0.04)) = 0.012
        _BlurRadius ("Air Blur Radius (Pixels)", Range(0,6)) = 2.2
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
        _AirFlow ("Air Flow Detail", Range(0,1)) = 0.8
        _GrowthCurve ("Growth Curve", 2D) = "white" {}
        _GrowthFraction ("Growth Duration / Lifetime", Float) = 0.2
        _DissolveStart ("Dissolve Start", Float) = 0.44
        _DissolveEnd ("Dissolve End", Float) = 1
        _TailFadePower ("Tail Opacity Falloff", Range(0.1,4)) = 1.2
        _CenterWidth ("Layer Width Multiplier", Range(0,1)) = 1
        [HideInInspector] _ControllerLineWidth ("Controller Width", Float) = 1
        [HideInInspector] _ControllerBrightness ("Controller Brightness", Float) = 1
        _FlowSpeed ("Air Flow Speed", Range(0,3)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            // UV.xy + AgePercent.z come from the Particle System's vertex streams.
            struct Attributes { float4 positionOS : POSITION; float3 uvAge : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 uvAge : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_GrowthCurve); SAMPLER(sampler_GrowthCurve);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Softness;
                float _TailFadePower;
                float _InnerFill;
                float _InwardFade;
                float _AirFlow;
                float _DistortionStrength, _BlurRadius;
                float _FlowSpeed;
                float _GrowthFraction, _DissolveStart, _DissolveEnd;
                float _CenterWidth, _ControllerLineWidth, _ControllerBrightness;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uvAge = input.uvAge;
                output.color = input.color;
                return output;
            }
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uvAge.xy;
                float phase = saturate(input.uvAge.z/max(_GrowthFraction,0.001));
                float progress = SAMPLE_TEXTURE2D(_GrowthCurve,sampler_GrowthCurve,float2((phase*1023+0.5)/1024,0.5)).r;
                // Keep the complete arc visible: growth controls appearance, never angular clipping.
                float arcU = saturate(uv.x);
                // u=0 leads the positive Y particle rotation; u=1 is the trailing end.
                // Mesh defines the traced path and its head-to-tail thickness.
                float width = saturate(_CenterWidth * _ControllerLineWidth);
                float center = lerp(0.5,1.0,_InnerFill);
                float rawV = (uv.y-center)/max(width,0.0001)+center;
                float v = saturate(rawV);
                float ribbon = pow(saturate(4.0 * v * (1.0 - v)), _Softness);
                // Broad translucent air sheet: always fades to zero toward the center.
                // Age-driven flow restarts with each particle, including editor scrubbing.
                float age = input.uvAge.z * _FlowSpeed;
                float2 flowUV = float2(uv.x * 9.0 - age * 1.6, v * 2.5 + age * 0.7);
                float2 warp = float2(Noise(flowUV*0.65),Noise(flowUV*0.65+17.3))-0.5;
                float broad = Noise(flowUV + warp*1.4);
                float fine = Noise(flowUV*1.9 + warp + 8.1);
                float air = smoothstep(0.1,0.9,broad*0.75+fine*0.25);
                air = lerp(0.55,1.0,air);
                float fill = pow(v, _InwardFade) * (1.0 - smoothstep(0.965, 1.0, v));
                fill *= lerp(1.0, air, _AirFlow);
                float ends = smoothstep(0.0, 0.035, uv.x) * (1.0 - smoothstep(0.94, 1.0, uv.x));
                float alpha = lerp(ribbon, fill, _InnerFill) * ends;
                float reveal = saturate(progress);
                float burnout = saturate((input.uvAge.z-_DissolveStart)/max(_DissolveEnd-_DissolveStart,0.001));
                float erosion = Noise(float2(uv.x*37,v*3))+0.22*Noise(float2(uv.x*83,v*7));
                float dissolve = smoothstep(burnout*1.5-0.14,burnout*1.5+0.08,erosion);
                // Preserve the short leading tip and strongest section, then fade the tail.
                float tailOpacity = pow(1.0-smoothstep(0.05,1.0,arcU),_TailFadePower);
                alpha *= tailOpacity*reveal*dissolve*smoothstep(0,0.001,width);
                half3 color = _Tint.rgb * input.color.rgb * _ControllerBrightness;
                if (_InnerFill > 0.5)
                {
                    // Opaque scene refraction, softly composited within the tapered air sheet.
                    // A flat/no opaque texture has no detail to distort.
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float2 offset = float2(broad-0.5,Noise(flowUV+31.7)-0.5);
                    offset *= _DistortionStrength * _AirFlow * sin(v*3.14159265);
                    float2 pixel = _BlurRadius / _ScaledScreenParams.xy;
                    float2 sampleUV = saturate(screenUV+offset);
                    half3 background = SampleSceneColor(sampleUV)*0.4;
                    background += SampleSceneColor(saturate(sampleUV+float2(pixel.x,0)))*0.15;
                    background += SampleSceneColor(saturate(sampleUV-float2(pixel.x,0)))*0.15;
                    background += SampleSceneColor(saturate(sampleUV+float2(0,pixel.y)))*0.15;
                    background += SampleSceneColor(saturate(sampleUV-float2(0,pixel.y)))*0.15;
                    color += background;
                }
                return half4(color, alpha * _Tint.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
