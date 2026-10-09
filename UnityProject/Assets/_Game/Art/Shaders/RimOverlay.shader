// AURELIA hero basin (ADR 0008): backlit rim on a character, as an extra additive material slot on its renderer.
// The project's URP asset has additional lights disabled (mobile budget), so a rim light cannot be a point light.
// An extra material slot past the last submesh draws that submesh again: this pass adds a warm Fresnel rim on the
// silhouette edges that face the sun (_JBSunDirection, set by the atmosphere), the keyframe's backlit edge on the
// hair, shoulders and arms. One extra draw, no textures, skinning comes free with the renderer.
Shader "JungleBooze/Rim Overlay"
{
    Properties
    {
        _RimColor("Rim Color (HDR)", Color) = (1.0, 0.8, 0.55, 1)
        _RimIntensity("Rim Intensity", Range(0, 8)) = 2
        _RimPower("Rim Power (edge width)", Range(0.5, 8)) = 3
        _SunFacing("Sun-Facing Bias (0 all edges, 1 only edges toward the sun)", Range(0, 1)) = 0.8
        _UpBias("Upward Bias (light from above)", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
                half _SunFacing;
                half _UpBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(n, v)), _RimPower);
                // Edges whose normal leans toward the sun (and up) catch the backlight; the others stay dark.
                half3 toSun = normalize(_JBSunDirection.xyz + half3(0.0h, _UpBias, 0.0h));
                half facing = lerp(1.0h, saturate(dot(n, toSun) * 0.75h + 0.35h), _SunFacing);
                half shadow = lerp(0.4h, 1.0h, GetMainLight(TransformWorldToShadowCoord(input.positionWS)).shadowAttenuation);
                half3 rim = _RimColor.rgb * (_RimIntensity * fresnel * facing * shadow);
                return half4(rim * JBFogKeep(input.positionWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
