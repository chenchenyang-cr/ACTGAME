Shader "Ellen/ZZZToon"
{
    Properties
    {
        [Header(Maps)]
        [MainTexture] _BaseMap("Diffuse (_D)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _NormalMap("Normal (_N RG)", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 0.45
        [NoScaleOffset] _MaterialMap("Material (_M)", 2D) = "white" {}
        [NoScaleOffset] _LightMap("Light / Face Mask (_A)", 2D) = "white" {}

        [Header(Cel Shade)]
        _CelMid("Cel Mid", Range(-1, 1)) = 0.05
        _CelSoft("Cel Soft", Range(0.001, 0.5)) = 0.06
        _ShadowColor("Shadow Color", Color) = (0.72, 0.68, 0.82, 1)
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.55
        _ReceiveShadows("Receive Shadows", Range(0, 1)) = 0.35

        [Header(Face)]
        [Toggle] _IsFace("Is Face", Float) = 0
        _FaceShadowSoft("Face Shadow Soft", Range(0.01, 0.5)) = 0.12
        _FaceLightOffset("Face Light Offset", Range(-1, 1)) = 0

        [Header(Specular Rim)]
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularIntensity("Specular Intensity", Range(0, 4)) = 0.85
        _SpecularSize("Specular Size", Range(0.01, 1)) = 0.18
        _RimColor("Rim Color", Color) = (0.75, 0.9, 1, 1)
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.35
        _RimPower("Rim Power", Range(1, 8)) = 3.5

        [Header(Outline)]
        _OutlineWidth("Outline Width", Range(0, 4)) = 0.9
        _OutlineColor("Outline Color", Color) = (0.08, 0.07, 0.1, 1)

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaterialMap);    SAMPLER(sampler_MaterialMap);
            TEXTURE2D(_LightMap);       SAMPLER(sampler_LightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _CelMid;
                half _CelSoft;
                half4 _ShadowColor;
                half _ShadowStrength;
                half _ReceiveShadows;
                half _IsFace;
                half _FaceShadowSoft;
                half _FaceLightOffset;
                half4 _SpecularColor;
                half _SpecularIntensity;
                half _SpecularSize;
                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
                half _OutlineWidth;
                half4 _OutlineColor;
                half _Cutoff;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half3 UnpackZZZNormal(half4 packed, half scale)
            {
                half2 nxy = packed.rg * 2.0h - 1.0h;
                nxy *= scale;
                half nz = sqrt(saturate(1.0h - dot(nxy, nxy)));
                return normalize(half3(nxy, nz));
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = nrm.normalWS;
                output.tangentWS = float4(nrm.tangentWS, input.tangentOS.w);
                output.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
                #endif

                half4 packedN = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half4 matMap = SAMPLE_TEXTURE2D(_MaterialMap, sampler_MaterialMap, input.uv);
                half4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, input.uv);

                float3 nWS = normalize(input.normalWS);
                float3 tWS = normalize(input.tangentWS.xyz);
                float3 bWS = normalize(cross(nWS, tWS) * input.tangentWS.w);
                half3 nTS = UnpackZZZNormal(packedN, _NormalScale);
                nWS = normalize(nTS.x * tWS + nTS.y * bWS + nTS.z * nWS);

                float3 vWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NoL = dot(nWS, mainLight.direction);
                half cel = smoothstep(_CelMid - _CelSoft, _CelMid + _CelSoft, NoL);

                if (_IsFace > 0.5h)
                {
                    float3 f = TransformObjectToWorldDir(float3(0, 0, 1));
                    float3 r = TransformObjectToWorldDir(float3(1, 0, 0));
                    float2 lightXZ = normalize(float2(dot(mainLight.direction, r), dot(mainLight.direction, f)));
                    half faceLit = saturate(lightXZ.y * 0.5h + 0.5h + _FaceLightOffset);
                    half faceMask = lightMap.r;
                    cel = smoothstep(faceLit - _FaceShadowSoft, faceLit + _FaceShadowSoft, faceMask);
                    cel = max(cel, 0.35h);
                }

                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadows);
                half lit = saturate(cel * shadow);
                half3 shade = lerp(_ShadowColor.rgb, 1.0h, lit);
                shade = lerp(1.0h, shade, _ShadowStrength);

                half metallic = matMap.g;
                half gloss = matMap.b;
                half specMask = lightMap.b;
                half3 h = normalize(mainLight.direction + vWS);
                half NoH = saturate(dot(nWS, h));
                half spec = pow(NoH, lerp(16.0h, 96.0h, gloss)) * specMask;
                spec *= smoothstep(0.0h, _SpecularSize, spec) * _SpecularIntensity * lit;

                half rim = pow(1.0h - saturate(dot(nWS, vWS)), _RimPower) * _RimIntensity * lit;

                half3 lighting = mainLight.color * shade;
                half3 gi = SampleSH(nWS) * 0.35h;
                half3 color = albedo.rgb * (lighting + gi);
                color += _SpecularColor.rgb * spec * lerp(1.0h, albedo.rgb, metallic);
                color += _RimColor.rgb * rim * albedo.rgb;
                color = MixFog(color, input.fogCoord);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaterialMap); SAMPLER(sampler_MaterialMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _CelMid;
                half _CelSoft;
                half4 _ShadowColor;
                half _ShadowStrength;
                half _ReceiveShadows;
                half _IsFace;
                half _FaceShadowSoft;
                half _FaceLightOffset;
                half4 _SpecularColor;
                half _SpecularIntensity;
                half _SpecularSize;
                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
                half _OutlineWidth;
                half4 _OutlineColor;
                half _Cutoff;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vertOutline(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                float dist = distance(GetCameraPositionWS(), posWS);
                posWS += nWS * (_OutlineWidth * 0.002 * dist);
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                half outlineMask = SAMPLE_TEXTURE2D(_MaterialMap, sampler_MaterialMap, input.uv).r;
                clip(outlineMask - 0.02h);
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                posWS = ApplyShadowBias(posWS, nWS, lightDir);
                return TransformWorldToHClip(posWS);
            }

            float4 ShadowPassVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return GetShadowPositionHClip(input);
            }

            half4 ShadowPassFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
