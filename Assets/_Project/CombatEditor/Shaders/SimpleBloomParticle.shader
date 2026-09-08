Shader "CombatEditor/SimpleBloomParticle"
{
    Properties
    {
        [MainTexture] _MainTex ("Particle Texture", 2D) = "white" {}

        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)

        _EmissionIntensity ("Emission Intensity", Range(0, 20)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        // 普通透明混合
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ParticleForward"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                // Particle System 的 Start Color / Color over Lifetime
                float4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)

                float4 _MainTex_ST;
                float4 _EmissionColor;
                float _EmissionIntensity;

            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positionInputs.positionCS;

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                output.color = input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                );

                // 纹理 × Particle System颜色
                half4 finalColor = tex * input.color;

                // RGB 可以超过 1
                // Bloom 就是靠这个 HDR 亮度产生的
                finalColor.rgb *=
                    _EmissionColor.rgb *
                    _EmissionIntensity;

                // Alpha 单独处理，不让发光强度改变透明度
                finalColor.a *= _EmissionColor.a;

                return finalColor;
            }

            ENDHLSL
        }
    }
}