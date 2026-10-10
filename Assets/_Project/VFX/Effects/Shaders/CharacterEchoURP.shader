Shader "Project/VFX/Character Echo URP"
{
    Properties
    {
        [Enum(Additive,1,AlphaBlend,10)] _Dst("Blend Mode", Float) = 1
        _ScreenTex("Screen Texture", 2D) = "white" {}
        [HDR] _ScreenTexColor("Screen Texture Color", Color) = (0.2,1.4,2.5,1)
        _ScreenTex_Scale_Offset("Screen Scale / Offset", Vector) = (1,1,0,0)
        _ScreenTex_Panner("Screen Pan XY / Speed Z / Strength W", Vector) = (0.1,0.05,1,1)
        [HDR] _MainColorDiSE("Fresnel Main Color", Color) = (0.1,0.8,2,1)
        _MainColorMaskPower("Main Fresnel Power", Float) = 1
        _MainColorMaskSacle("Main Fresnel Scale", Float) = 1
        [HDR] _FreColor("Edge Fresnel Color", Color) = (0.2,1,2,1)
        _FrePower("Edge Fresnel Power", Float) = 1
        _FreScale("Edge Fresnel Scale", Float) = 1
        [KeywordEnum(X,Y,Z)] _Offset_Direction("Edge Mask Axis", Float) = 0
        _FreVPmaskOffset("Edge Mask Offset", Float) = 0
        _MaskSmoothMin("Edge Mask Min", Float) = 0
        _MaskSmoothMax("Edge Mask Max", Float) = 1
        [HDR] _Color0("Fresnel Line Color", Color) = (0.4,1.5,2.5,1)
        _SmoothMaxP("Fresnel Line Strength", Float) = 1
        _SmoothMin("Fresnel Line Min", Float) = 0
        _SmoothMax("Fresnel Line Max", Float) = 1
        _AlphaFrepower("Center Alpha Power", Float) = 1
        _AlphaFreScale("Center Alpha Scale", Float) = 0.5
        _MainAlphaAdd("Alpha Add", Float) = 0
        _Main_Alpha("Main Alpha", Float) = 1
        _DissovleTex("Dissolve Texture", 2D) = "white" {}
        [Toggle] _use_disslove("Use Dissolve", Float) = 0
        [Enum(R,0,A,1)] _DissovleA_R("Dissolve Channel", Float) = 0
        [Enum(Off,0,On,1)] _DissSC("Dissolve Switch", Float) = 0
        _DissovleTex_Scale_Offset("Dissolve Scale / Offset", Vector) = (1,1,0,0)
        _DissovleTex_Panner("Dissolve Pan XY / Speed Z / Strength W", Vector) = (0,0,0,1)
        _smooth("Dissolve Smooth", Range(0.5,1)) = 0.5
        _Disspower("Dissolve Power", Range(0,1)) = 0
        [HideInInspector] _EffectAlpha("Ghost Lifetime Fade", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "LTYSurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha [_Dst]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _OFFSET_DIRECTION_X _OFFSET_DIRECTION_Y _OFFSET_DIRECTION_Z
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ScreenTex); SAMPLER(sampler_ScreenTex);
            TEXTURE2D(_DissovleTex); SAMPLER(sampler_DissovleTex);
            CBUFFER_START(UnityPerMaterial)
                float _Dst;
                float4 _ScreenTexColor;
                float4 _ScreenTex_Scale_Offset;
                float4 _ScreenTex_Panner;
                float4 _MainColorDiSE;
                float _MainColorMaskPower;
                float _MainColorMaskSacle;
                float4 _FreColor;
                float _FrePower;
                float _FreScale;
                float _FreVPmaskOffset;
                float _MaskSmoothMin;
                float _MaskSmoothMax;
                float4 _Color0;
                float _SmoothMaxP;
                float _SmoothMin;
                float _SmoothMax;
                float _AlphaFrepower;
                float _AlphaFreScale;
                float _MainAlphaAdd;
                float _Main_Alpha;
                float _use_disslove;
                float _DissovleA_R;
                float _DissSC;
                float4 _DissovleTex_Scale_Offset;
                float4 _DissovleTex_Panner;
                float _smooth;
                float _Disspower;
                float _EffectAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float2 screenTexUV = screenUV * _ScreenTex_Scale_Offset.xy +
                    _ScreenTex_Scale_Offset.zw + _Time.y * _ScreenTex_Panner.z * _ScreenTex_Panner.xy;
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float facing = saturate(dot(normalize(input.normalWS), viewDir));
                float fresnel = 1.0 - facing;
                float mainMask = pow(fresnel, _MainColorMaskPower) * _MainColorMaskSacle;
                float4 screenColor = SAMPLE_TEXTURE2D(_ScreenTex, sampler_ScreenTex, screenTexUV) *
                    _ScreenTex_Panner.w * _ScreenTexColor;
                float4 color = lerp(screenColor, _MainColorDiSE, mainMask);
                float maskAxis = input.positionOS.x;
                #if defined(_OFFSET_DIRECTION_Y)
                    maskAxis = input.positionOS.y;
                #elif defined(_OFFSET_DIRECTION_Z)
                    maskAxis = input.positionOS.z;
                #endif
                float spatialMask = smoothstep(_MaskSmoothMin, _MaskSmoothMax,
                    maskAxis + _FreVPmaskOffset);
                color += pow(fresnel, _FrePower) * _FreScale * _FreColor * spatialMask;
                color += smoothstep(_SmoothMin, _SmoothMax, fresnel) * _SmoothMaxP * _Color0;
                float alpha = (mainMask + pow(facing, _AlphaFrepower) * _AlphaFreScale +
                    _MainAlphaAdd) * _Main_Alpha;
                if (_use_disslove > 0.5)
                {
                    float2 dissolveUV = screenUV * _DissovleTex_Scale_Offset.xy +
                        _DissovleTex_Scale_Offset.zw + _Time.y * _DissovleTex_Panner.z *
                        _DissovleTex_Panner.xy;
                // Sample the noise in world space so UV islands cannot turn the
                // dissolve into a bottom-to-top wipe on the character mesh.
                float3 noiseWeights = pow(abs(normalize(input.normalWS)), 4.0);
                noiseWeights /= max(noiseWeights.x + noiseWeights.y + noiseWeights.z, 0.0001);
                float3 noisePosition = input.positionWS * 1.5;
                float2 noiseScale = max(abs(_DissovleTex_Scale_Offset.xy), float2(0.001, 0.001));
                float2 noiseOffset = _DissovleTex_Scale_Offset.zw;
                float4 noiseX = SAMPLE_TEXTURE2D(_DissovleTex, sampler_DissovleTex, noisePosition.zy * noiseScale + noiseOffset);
                float4 noiseY = SAMPLE_TEXTURE2D(_DissovleTex, sampler_DissovleTex, noisePosition.xz * noiseScale + noiseOffset);
                float4 noiseZ = SAMPLE_TEXTURE2D(_DissovleTex, sampler_DissovleTex, noisePosition.xy * noiseScale + noiseOffset);
                float4 noiseValue = noiseX * noiseWeights.x + noiseY * noiseWeights.y + noiseZ * noiseWeights.z;
                float channel = lerp(noiseValue.r, noiseValue.a, _DissovleA_R);
                    float power = lerp(_Disspower, 0.0, _DissSC);
                float dissolve = saturate(channel * _DissovleTex_Panner.w + 1.0 - power * 2.0);
                float dissolveOpacity = smoothstep(1.0 - _smooth, max(_smooth, 0.5001), dissolve);
                clip(dissolveOpacity - 0.001);
                alpha *= dissolveOpacity;
                }
                return half4(color.rgb, saturate(alpha * _EffectAlpha));
            }
            ENDHLSL
        }
    }
    CustomEditor "CombatEditor.CharacterEchoShaderGUI"
}
