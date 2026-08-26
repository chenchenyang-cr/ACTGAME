Shader "Hidden/Combat/PostFX"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Combat Post FX"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CombatFxLens0;
            float4 _CombatFxVignette;
            float4 _CombatFxStyle;
            float4 _CombatFxGlitch;
            float4 _CombatFxSpeedLines;
            float4 _CombatFxSpeedRangeGrain;
            float _CombatFxGrainSpeed;
            float4 _CombatFxFlashColor;
            float4 _CombatFxVignetteColor;
            float4 _CombatFxTintColor;
            float4 _CombatFxSpeedLineColor;
            float2 _CombatFxCenter;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            half3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 center = _CombatFxCenter;
                float2 aspectOffset = uv - center;
                aspectOffset.x *= _ScreenParams.x / _ScreenParams.y;
                float radius = length(aspectOffset);

                // Sparse horizontal displacement and channel tearing.
                float row = floor(uv.y * _CombatFxGlitch.y);
                float tick = floor(_Time.y * _CombatFxGlitch.x);
                float glitchGate = step(0.78, Hash21(float2(row, tick)));
                float glitchOffset = (Hash21(float2(row + 9.1, tick)) - 0.5) *
                                     _CombatFxGlitch.z * _CombatFxStyle.w * glitchGate;
                uv.x += glitchOffset;

                // Six taps are enough for a short combat impulse and keep the pass mobile-friendly.
                float2 radialStep = (center - uv) *
                                    (0.018 * _CombatFxLens0.x * _CombatFxLens0.y);
                half3 color = 0;
                color += SampleScene(uv);
                color += SampleScene(uv + radialStep);
                color += SampleScene(uv + radialStep * 2.0);
                color += SampleScene(uv + radialStep * 3.0);
                color += SampleScene(uv + radialStep * 4.0);
                color += SampleScene(uv + radialStep * 5.0);
                color *= (1.0 / 6.0);

                float2 chromaDirection = normalize(aspectOffset + float2(0.0001, 0.0001));
                chromaDirection.x /= max(_ScreenParams.x / _ScreenParams.y, 0.0001);
                float chromaAmount = (0.0015 + radius * 0.006) *
                                     _CombatFxLens0.z * _CombatFxLens0.w;
                float glitchSplit = _CombatFxGlitch.w * _CombatFxStyle.w * glitchGate;
                color.r = SampleScene(uv + chromaDirection * chromaAmount + float2(glitchSplit, 0)).r;
                color.b = SampleScene(uv - chromaDirection * chromaAmount - float2(glitchSplit, 0)).b;

                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luminance.xxx, _CombatFxStyle.y);
                color = lerp(color, color * _CombatFxTintColor.rgb, _CombatFxStyle.z);

                float vignetteMask = smoothstep(_CombatFxVignette.y, _CombatFxVignette.z, radius) *
                                     _CombatFxVignette.x;
                color = lerp(color, _CombatFxVignetteColor.rgb, saturate(vignetteMask));

                // Procedural anime speed lines, concentrated away from the focal point.
                float angle = atan2(aspectOffset.y, aspectOffset.x) * _CombatFxSpeedLines.y +
                              _Time.y * _CombatFxSpeedLines.w;
                float lineNoise = Hash21(float2(floor(angle), floor(_Time.y * 18.0)));
                float lineShape = pow(saturate(sin(angle * 3.14159265) * 0.5 + 0.5),
                                      _CombatFxSpeedLines.z);
                float lineMask = lineShape * step(0.38, lineNoise) *
                                 smoothstep(_CombatFxSpeedRangeGrain.x,
                                            _CombatFxSpeedRangeGrain.x + 0.12, radius) *
                                 (1.0 - smoothstep(_CombatFxSpeedRangeGrain.y * 0.72,
                                                   _CombatFxSpeedRangeGrain.y, radius));
                color += lineMask * _CombatFxSpeedLines.x *
                         _CombatFxSpeedLineColor.rgb * (0.7 + color) * 0.65;

                float grain = Hash21(input.positionCS.xy / max(_CombatFxSpeedRangeGrain.w, 0.01) +
                                     floor(_Time.y * _CombatFxGrainSpeed) * 17.0) - 0.5;
                color += grain * (0.12 * _CombatFxSpeedRangeGrain.z + 0.08 * _CombatFxStyle.w);

                color = lerp(color, _CombatFxFlashColor.rgb, saturate(_CombatFxStyle.x));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
