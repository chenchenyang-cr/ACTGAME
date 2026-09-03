Shader "CombatEditor/AirSlashDistortionURP"
{
    Properties
    {
        _MainTex ("Noise", 2D) = "gray" {}
        [HDR] _Color ("Overlay Color", Color) = (1, 1, 1, 0)
        [Enum(U, 0, V, 1)] _DistortionAxis ("Distortion Axis", Float) = 0
        _Distortion ("Distortion", Range(0, 1)) = 0.05
        [Header(Profile)]
        [Toggle] _DebugIntensity ("Debug Intensity", Float) = 0
        _CenterPosition ("Center Position", Range(0, 1)) = 0.5
        _CenterIntensity ("Center Intensity", Range(0, 2)) = 1
        _EdgeIntensity ("Edge Intensity", Range(0, 2)) = 0
        _Inner ("Inner", Range(0, 1)) = 0
        _Outer ("Outer", Range(0, 1)) = 1
        _Softness ("Softness", Range(0, 1)) = 1
        _NoiseScrollAngle ("Scroll Angle", Range(0, 360)) = 0
        _NoiseScrollSpeed ("Scroll Speed", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "AirSlashDistortion"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

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
                float2 noiseUV : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _DistortionAxis;
                float _Distortion;
                float _DebugIntensity;
                float _CenterPosition;
                float _CenterIntensity;
                float _EdgeIntensity;
                float _Inner;
                float _Outer;
                float _Softness;
                float _NoiseScrollAngle;
                float _NoiseScrollSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                float angle = _NoiseScrollAngle * 0.017453292;
                float2 scroll = float2(cos(angle), sin(angle)) * _Time.y * _NoiseScrollSpeed;
                output.noiseUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw + scroll;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;

                float2 screenDx = ddx(screenUV);
                float2 screenDy = ddy(screenUV);
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float uvDeterminant = uvDx.x * uvDy.y - uvDy.x * uvDx.y;
                float safeDeterminant = abs(uvDeterminant) > 1e-7
                    ? uvDeterminant
                    : (uvDeterminant < 0.0 ? -1e-7 : 1e-7);

                float2 axisU = (screenDx * uvDy.y - screenDy * uvDx.y) / safeDeterminant;
                float2 axisV = (screenDy * uvDx.x - screenDx * uvDy.x) / safeDeterminant;
                float2 chosenAxis = _DistortionAxis > 0.5 ? axisV : axisU;
                float2 distortionDir = normalize(
                    chosenAxis * _ScreenParams.xy + float2(1e-6, 0.0));

                float perpendicularUV = _DistortionAxis > 0.5 ? input.uv.x : input.uv.y;
                float offset = perpendicularUV - _CenterPosition;
                float extent = offset < 0.0
                    ? max(_CenterPosition, 1e-5)
                    : max(1.0 - _CenterPosition, 1e-5);
                float edgeDistance = saturate(abs(offset) / extent);
                float outer = max(_Outer, _Inner + 1e-5);
                float linearT = saturate((edgeDistance - _Inner) / (outer - _Inner));
                float halfWidth = lerp(1e-3, 0.5, saturate(_Softness));
                float profile = lerp(
                    _CenterIntensity,
                    _EdgeIntensity,
                    smoothstep(0.5 - halfWidth, 0.5 + halfWidth, linearT));

                if (_DebugIntensity > 0.5)
                {
                    return half4(profile.xxx, 1.0h);
                }

                half noise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.noiseUV).r;
                float signedNoise = noise * 2.0h - 1.0h;
                float2 distortedUV = saturate(
                    screenUV + distortionDir * _Distortion * signedNoise * profile);

                half3 sceneColor = SampleSceneColor(distortedUV);
                half3 finalColor = lerp(sceneColor, _Color.rgb, saturate(_Color.a * profile));
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
