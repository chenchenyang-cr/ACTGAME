Shader "CombatEditor/DynamicTrailGlowURP"
{
    Properties
    {
        _MainTex ("Flow Mask", 2D) = "white" {}
        [HDR] _TintColor ("Glow Tint", Color) = (0.25, 1.2, 3, 0.55)
        _Intensity ("Emission Intensity", Range(0, 12)) = 2.5
        _Alpha ("Opacity", Range(0, 1)) = 0.65
        _UVTiling ("Texture Tiling", Vector) = (1, 1, 0, 0)
        _UVScrollSpeed ("Flow Speed", Float) = 0.8
        _TailFade ("Tail Fade", Range(0.01, 1)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.18
        _CoreWidth ("Core Width", Range(0.01, 1)) = 0.22
        _CoreStrength ("Core Strength", Range(0, 8)) = 2.2
        _FlowContrast ("Flow Contrast", Range(0.1, 4)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DynamicTrailGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _UVTiling;
                float _Intensity;
                float _Alpha;
                float _UVScrollSpeed;
                float _TailFade;
                float _EdgeSoftness;
                float _CoreWidth;
                float _CoreStrength;
                float _FlowContrast;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flowUV = input.uv * _UVTiling.xy;
                flowUV.x -= _Time.y * _UVScrollSpeed;

                half4 textureSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV);
                half textureMask = saturate(textureSample.a);
                textureMask = pow(max(textureMask, 0.0001h), _FlowContrast);

                half tail = smoothstep(0.0h, max(_TailFade, 0.0001h), input.uv.x);
                half distanceFromCenter = abs(input.uv.y * 2.0h - 1.0h);
                half sideFade = 1.0h - smoothstep(
                    1.0h - _EdgeSoftness, 1.0h, distanceFromCenter);
                half core = 1.0h - smoothstep(
                    _CoreWidth, min(1.0h, _CoreWidth + _EdgeSoftness), distanceFromCenter);

                half alpha = saturate(textureMask * tail * sideFade *
                    _TintColor.a * _Alpha);
                half emissionShape = 1.0h + core * _CoreStrength;
                half3 emission = _TintColor.rgb * _Intensity * emissionShape;

                // Premultiplied output keeps the trail translucent while preserving HDR bloom.
                return half4(emission * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
