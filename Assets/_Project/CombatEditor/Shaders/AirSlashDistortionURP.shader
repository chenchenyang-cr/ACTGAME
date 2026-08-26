Shader "CombatEditor/AirSlashDistortionURP"
{
    Properties
    {
        _MainTex ("Slash Mask", 2D) = "white" {}
        _DistortionPixels ("Distortion (Pixels)", Range(0, 160)) = 85
        _BlurPixels ("Blur (Pixels)", Range(0, 48)) = 18
        _ThicknessPixels ("Air Volume Thickness (Pixels)", Range(0, 40)) = 14
        _Opacity ("Opacity", Range(0, 1)) = 0.95
        _RimStrength ("Air Compression Rim", Range(0, 2)) = 0.35
        _FlowSpeed ("Flow Speed", Range(0, 8)) = 2.4
        _NoiseScale ("Flow Frequency", Range(1, 30)) = 12
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _DistortionPixels;
                float _BlurPixels;
                float _ThicknessPixels;
                float _Opacity;
                float _RimStrength;
                float _FlowSpeed;
                float _NoiseScale;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 maskSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half mask = dot(maskSample.rgb, half3(0.299h, 0.587h, 0.114h));
                mask = saturate(mask * 1.8h);

                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float2 texel = rcp(_ScreenParams.xy);
                // Reconstruct the screen-space direction of the slash texture's U axis.
                // This keeps both refraction and blur attached to the actual slash motion
                // instead of using a radial/noisy direction that swims across the particle.
                float2 screenDx = ddx(screenUV);
                float2 screenDy = ddy(screenUV);
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float uvDeterminant = uvDx.x * uvDy.y - uvDy.x * uvDx.y;
                float safeDeterminant = abs(uvDeterminant) > 1e-7
                    ? uvDeterminant
                    : (uvDeterminant < 0.0 ? -1e-7 : 1e-7);
                float2 slashScreenAxis = screenDx * uvDy.y - screenDy * uvDx.y;
                slashScreenAxis /= safeDeterminant;
                float2 slashDirection = normalize(
                    slashScreenAxis * _ScreenParams.xy + float2(1e-6, 0.0));
                float2 slashNormal = float2(-slashDirection.y, slashDirection.x);

                clip(mask * input.color.a - 0.01h);

                float time = _Time.y * _FlowSpeed;
                float directionalFlow = 0.78 +
                    sin((input.uv.x * _NoiseScale - time) * 6.2831853) * 0.22;
                float distortionEnvelope = smoothstep(0.02, 0.32, mask) * input.color.a;
                float2 distortion = slashDirection * (_DistortionPixels * texel) *
                                    directionalFlow * distortionEnvelope;
                float2 refractedUV = saturate(screenUV + distortion);

                // Nine taps along the slash direction produce directional motion blur.
                float2 blurStep = slashDirection * texel * _BlurPixels * 0.25;
                half3 blurred = SampleSceneColor(refractedUV) * 0.20h;
                blurred += SampleSceneColor(saturate(refractedUV + blurStep)) * 0.16h;
                blurred += SampleSceneColor(saturate(refractedUV - blurStep)) * 0.16h;
                blurred += SampleSceneColor(saturate(refractedUV + blurStep * 2.0)) * 0.12h;
                blurred += SampleSceneColor(saturate(refractedUV - blurStep * 2.0)) * 0.12h;
                blurred += SampleSceneColor(saturate(refractedUV + blurStep * 3.0)) * 0.08h;
                blurred += SampleSceneColor(saturate(refractedUV - blurStep * 3.0)) * 0.08h;
                blurred += SampleSceneColor(saturate(refractedUV + blurStep * 4.0)) * 0.04h;
                blurred += SampleSceneColor(saturate(refractedUV - blurStep * 4.0)) * 0.04h;

                // Keep every layer in the same clear-air visual language. Color
                // separation made the refraction look like a second energy material.
                half3 refracted = SampleSceneColor(refractedUV);

                // Build a transparent volume across the slash. The center, near
                // surface and far surface see the background through slightly
                // different offsets, which gives the air band a readable thickness.
                float thicknessProfile = smoothstep(0.04, 0.72, mask) * input.color.a;
                float2 thicknessStep = slashNormal * texel * _ThicknessPixels *
                                       thicknessProfile;
                half3 nearSurface = SampleSceneColor(
                    saturate(refractedUV + thicknessStep));
                half3 farSurface = SampleSceneColor(
                    saturate(refractedUV - thicknessStep));
                half3 nearInner = SampleSceneColor(
                    saturate(refractedUV + thicknessStep * 0.45));
                half3 farInner = SampleSceneColor(
                    saturate(refractedUV - thicknessStep * 0.45));
                half3 volumeRefraction = refracted * 0.30h +
                                         (nearInner + farInner) * 0.20h +
                                         (nearSurface + farSurface) * 0.15h;
                half3 sceneColor = lerp(volumeRefraction, blurred, 0.54h);

                half compressionBand = smoothstep(0.035h, 0.16h, mask) *
                                       (1.0h - smoothstep(0.32h, 0.82h, mask));
                // A tiny neutral compression highlight belongs to the same air
                // distortion; avoid the previous blue-white solid-looking rim.
                half localLuminance = dot(refracted, half3(0.299h, 0.587h, 0.114h));
                half3 compressionColor = localLuminance.xxx * compressionBand *
                                         (_RimStrength * 0.12h);
                half alpha = saturate(
                    smoothstep(0.015h, 0.48h, mask) * input.color.a * _Opacity);
                return half4(sceneColor + compressionColor, alpha);
            }
            ENDHLSL
        }
    }
}
