Shader "CombatEditor/AirBlurWeaponTrailURP"
{
    Properties
    {
        _MainTex ("Distortion Mask", 2D) = "white" {}
        [HDR] _TintColor ("Air Edge Tint", Color) = (0.55, 0.85, 1, 0.5)
        _Intensity ("Edge Brightness", Range(0, 12)) = 1.5
        _BlurRadius ("Blur Radius (Pixels)", Range(0, 32)) = 8
        _BlurStrength ("Blur Strength", Range(0, 2)) = 1
        _DistortionStrength ("Distortion (Pixels)", Range(0, 30)) = 8
        _NoiseFrequency ("Air Wave Frequency", Range(0.1, 30)) = 9
        _UVTiling ("Mask Tiling", Vector) = (1, 1, 0, 0)
        _UVScrollSpeed ("Air Flow Speed", Float) = 0.8
        _TailFade ("Tail Fade", Range(0.01, 1)) = 0.25
        _Alpha ("Blur Opacity", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "WeaponTrail"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float2 _UVTiling;
                float _UVScrollSpeed;
                float _TailFade;
                float _Intensity;
                float _Alpha;
                float _BlurRadius;
                float _BlurStrength;
                float _DistortionStrength;
                float _NoiseFrequency;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 textureUV = input.uv * _UVTiling;
                textureUV.x -= _Time.y * _UVScrollSpeed;
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV).a;
                half tail = smoothstep(0.0, max(_TailFade, 0.0001), input.uv.x);
                half sideFade = smoothstep(0.0, 0.14, input.uv.y) *
                                (1.0 - smoothstep(0.86, 1.0, input.uv.y));

                float time = _Time.y * _UVScrollSpeed;
                float waveA = sin((input.uv.x * _NoiseFrequency + time) * 6.2831853);
                float waveB = cos((input.uv.x * (_NoiseFrequency * 0.63) - time * 1.37 + input.uv.y) * 6.2831853);
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float2 texel = rcp(_ScreenParams.xy);
                float2 distortion = float2(waveA, waveB) * (_DistortionStrength * texel) * mask;
                float2 blurStep = texel * _BlurRadius;
                float2 sampleUV = saturate(screenUV + distortion);

                half3 sourceColor = SampleSceneColor(sampleUV);
                half3 blurredColor = sourceColor * 0.08h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2( blurStep.x, 0))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(-blurStep.x, 0))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0,  blurStep.y))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0, -blurStep.y))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + blurStep)) * 0.04h;
                blurredColor += SampleSceneColor(saturate(sampleUV - blurStep)) * 0.04h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(blurStep.x, -blurStep.y))) * 0.04h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(-blurStep.x, blurStep.y))) * 0.04h;

                float2 outerBlurStep = blurStep * 2.0;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2( outerBlurStep.x, 0))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(-outerBlurStep.x, 0))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0,  outerBlurStep.y))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0, -outerBlurStep.y))) * 0.06h;
                blurredColor += SampleSceneColor(saturate(sampleUV + outerBlurStep)) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV - outerBlurStep)) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(outerBlurStep.x, -outerBlurStep.y))) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(-outerBlurStep.x, outerBlurStep.y))) * 0.035h;

                float2 farBlurStep = blurStep * 3.0;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2( farBlurStep.x, 0))) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(-farBlurStep.x, 0))) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0,  farBlurStep.y))) * 0.035h;
                blurredColor += SampleSceneColor(saturate(sampleUV + float2(0, -farBlurStep.y))) * 0.035h;

                half3 sceneColor = saturate(
                    sourceColor + (blurredColor - sourceColor) * _BlurStrength);

                half edge = saturate(1.0h - sideFade) * sideFade + abs(waveA - waveB) * 0.035h;
                half3 edgeColor = _TintColor.rgb * (_Intensity * 0.12h) * edge;
                half alpha = mask * tail * sideFade * _Alpha;
                return half4(sceneColor + edgeColor, alpha);
            }
            ENDHLSL
        }
    }
}
