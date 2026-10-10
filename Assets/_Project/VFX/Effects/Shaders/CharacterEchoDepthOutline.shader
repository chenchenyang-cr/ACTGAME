Shader "Hidden/CharacterEcho/DepthOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Character Depth"
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float eyeDepth : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }
            float4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return float4(input.eyeDepth, 0, 0, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outer Depth Outline"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_CharacterEchoDepth);
            float4 _CharacterEchoColor;
            float _CharacterEchoWidth;
            float _CharacterEchoOpacity;

            float ReadCharacterDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_CharacterEchoDepth, sampler_PointClamp, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (ReadCharacterDepth(uv) < 999999.0)
                    return scene;

                float2 texel = rcp(GetScaledScreenParams().xy);
                float nearest = 1000000.0;
                int steps = min(12, (int)ceil(_CharacterEchoWidth));
                const float2 directions[8] = {
                    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                    float2(0.707107, 0.707107), float2(-0.707107, 0.707107),
                    float2(0.707107, -0.707107), float2(-0.707107, -0.707107)
                };
                for (int radius = 1; radius <= steps; radius++)
                {
                    float distancePixels = min((float)radius, _CharacterEchoWidth);
                    [unroll] for (int direction = 0; direction < 8; direction++)
                    {
                        float2 sampleUv = uv + directions[direction] * texel * distancePixels;
                        if (all(sampleUv >= 0.0) && all(sampleUv <= 1.0))
                            nearest = min(nearest, ReadCharacterDepth(sampleUv));
                    }
                }
                if (nearest >= 999999.0)
                    return scene;

                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                // A missing camera depth texture can read as the near plane on some
                // renderer configurations. In that case keep the isolated outline visible.
                if (sceneEyeDepth > _ProjectionParams.y + 0.05 &&
                    nearest > sceneEyeDepth + 0.02)
                    return scene;

                scene.rgb += _CharacterEchoColor.rgb * saturate(_CharacterEchoOpacity);
                return scene;
            }
            ENDHLSL
        }
    }
}
