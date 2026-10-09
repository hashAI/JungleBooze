// AURELIA (ENVIRONMENT_STRATEGY 4.2 L5, ADR 0008): matte-painted backdrop layers as large cards.
// Unlit (the painting carries its own light and haze), alpha-blended, no depth write. Only part of the project
// fog is applied (_FogAmount) so the layers still sit in the same atmosphere as the 3D world; the card's side
// edges fade so a turn never shows a hard border. One draw per layer.
// _PAINTERLY (ADR 0009): the painting is read softened (mip bias), hue-flattened and saturated, then drifts toward
// a warm golden haze (aerial perspective of the painterly target, not the cool blue of the realistic one).
// Hero basin: the sky layer adds an HDR sun core and halo toward the sun (_SunGlow; zero on every other layer).
Shader "JungleBooze/Backdrop Card"
{
    Properties
    {
        [MainTexture] _MainTex("Layer (RGBA)", 2D) = "white" {}
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure", Range(0, 4)) = 1
        _FogAmount("Project Fog Share", Range(0, 1)) = 0.35
        _EdgeFade("Side Edge Fade (fraction of width)", Range(0, 0.5)) = 0.12
        _SunGlow("Sun Glow (x core gain, y core power, z halo gain, w halo power)", Vector) = (0, 400, 0, 12)
        _SunGlowColor("Sun Glow Color", Color) = (1, 0.86, 0.62, 1)
        [Toggle(_PAINTERLY)] _Painterly("Painterly", Float) = 0
        _PaintBlur("Softening (mip bias)", Range(0, 4)) = 1
        _PaintFlatten("Hue Flattening", Range(0, 1)) = 0.3
        _PaintSaturation("Saturation", Range(0, 2)) = 1.2
        _PaintHaze("Warm Haze (rgb, a = amount)", Color) = (0.84, 0.7, 0.45, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _PAINTERLY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "JBAtmosphere.hlsl"
            #include "JBPainterly.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Exposure;
                half _FogAmount;
                half _EdgeFade;
                float4 _SunGlow;
                half4 _SunGlowColor;
                half _Painterly;
                half _PaintBlur;
                half _PaintFlatten;
                half _PaintSaturation;
                half4 _PaintHaze;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
            #if defined(_PAINTERLY)
                half4 c = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, input.uv, _PaintBlur) * _Tint;
                half3 broad = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv, 5.0).rgb;
                c.rgb = JBSaturation(JBFlatten(c.rgb, broad, _PaintFlatten), _PaintSaturation);
                c.rgb = lerp(c.rgb, _PaintHaze.rgb * max(JBLuma(c.rgb) * 1.3h, 0.5h), _PaintHaze.a);
            #else
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
            #endif
                c.rgb *= _Exposure;
                // Painted sky only (_SunGlow = 0 elsewhere): lift the painted sun into HDR so bloom makes the flare.
                float3 viewDir = normalize(input.positionWS - _WorldSpaceCameraPos);
                half toSun = saturate(dot(viewDir, _JBSunDirection.xyz));
                c.rgb += _SunGlowColor.rgb * (_SunGlow.x * pow(toSun, _SunGlow.y) + _SunGlow.z * pow(toSun, _SunGlow.w));
                c.rgb = lerp(c.rgb, JBApplyFog(c.rgb, input.positionWS), _FogAmount);
                half side = min(input.uv.x, 1.0 - input.uv.x);
                c.a *= smoothstep(0.0h, max(_EdgeFade, 1e-3h), side);
                return c;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
