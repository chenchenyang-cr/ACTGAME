Shader "Custom/TransparentAirDistortion"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _Distortion ("Distortion Strength", Range(0, 0.1)) = 0.01
        _Speed ("Animation Speed", Range(0, 5)) = 1.0
        _Scale ("Noise Scale", Range(0.1, 10)) = 1.0
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.5 // 边缘淡出，增强透明感
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TransparentAirDistortion"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1; // 屏幕空间位置
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float _Distortion;
                float _Speed;
                float _Scale;
                float _EdgeFade;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                // 计算屏幕空间位置（用于采样背景纹理）
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);

                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 噪声动画
                float2 noiseUV = input.uv * _Scale;
                noiseUV += _Time.y * _Speed;
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);

                // 用噪声值偏移背景纹理的UV，实现扭曲
                float2 distortion = (noise.rg * 2 - 1) * _Distortion;

                // 计算屏幕UV（归一化到0-1范围）
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV += distortion; // 应用扭曲

                // 采样URP的Opaque Texture（背景纹理）
                half3 sceneColor = SampleSceneColor(screenUV);

                // 边缘淡出（让扰动层边缘更透明，模拟空气的扩散感）
                float2 centerDist = abs(input.uv - 0.5) * 2; // 计算到中心的距离（0~1）
                float edgeAlpha = 1 - smoothstep(1 - _EdgeFade, 1, max(centerDist.x, centerDist.y));

                return half4(sceneColor, edgeAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}