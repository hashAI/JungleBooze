// AURELIA painterly style trial (ADR 0009, ART_DIRECTION_PAINTERLY s9): Pista's light restyle by shading only.
// Reads the same textures as her URP/Lit material (base, OpenGL normal, metallic R + smoothness A, occlusion G), so
// the model, UVs and textures are unchanged; the hero scene builder gives her scene instance this material.
// - Smooth skin / softer hair / simpler cloth: the normal map is read blurred (mip bias) and weaker, so only big
//   folds and pocket edges remain; the albedo is softened slightly.
// - Wrapped diffuse 0.5 with teal-shifted shadows and painted (warm) occlusion (JBPainterly.hlsl).
// - Skin: a soft mask from the albedo's hue (warm, mid-saturated, light) adds a warm subsurface tint (#E8A07A) in
//   the terminator band. A heuristic, not a painted mask: it may touch light-tan leather a little.
// - Rim: always on (0.6), stronger on the edges facing the sun, so she reads against the arch and water.
// - Warm sun-side fill and ground bounce (both off by default; the hero basin sets them): she reads warmly lit.
// Replaces the realistic Rim Overlay slot (one draw instead of two). Shadow caster and depth passes included.
Shader "JungleBooze/Character Painterly"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal (OpenGL)", 2D) = "bump" {}
        _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _OcclusionMap("Occlusion (G)", 2D) = "white" {}
        _AlbedoBlur("Albedo Softening (mip bias)", Range(0, 3)) = 0.5
        _NormalBlur("Normal Softening (mip bias)", Range(0, 5)) = 2.5
        _NormalStrength("Normal Strength", Range(0, 1)) = 0.45
        _Saturation("Saturation", Range(0, 2)) = 1.1
        _Wrap("Diffuse Wrap", Range(0, 1)) = 0.5
        _RampSoftness("Ramp Softness", Range(0.02, 0.5)) = 0.25
        _Ramp("Ramp Amount", Range(0, 1)) = 0.35
        _ShadowTint("Shadow Tint (rgb, a = shift)", Color) = (0.243, 0.361, 0.4, 0.3)
        _Terminator("Terminator Band (rgb, a = amount)", Color) = (0.878, 0.541, 0.227, 0.15)
        _AOTint("Painted AO Colour", Color) = (0.302, 0.227, 0.133, 1)
        _OcclusionStrength("AO Strength", Range(0, 1)) = 0.7
        _Specular("Soft Specular", Range(0, 1)) = 0.2
        _SpecPower("Specular Power", Range(2, 64)) = 16
        _SkinTint("Skin Subsurface (rgb, a = amount)", Color) = (0.91, 0.627, 0.478, 0.5)
        _RimColor("Rim Colour", Color) = (1, 0.84, 0.6, 1)
        _RimIntensity("Rim Intensity", Range(0, 3)) = 0.6
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _RimSunFacing("Rim Sun-Facing Bias", Range(0, 1)) = 0.6
        _SunFill("Warm Sun-Side Fill (rgb, a = amount)", Color) = (1, 0.72, 0.42, 0)
        _FillDirection("Fill Direction (world, toward the light; zero = the visible sun)", Vector) = (0, 0, 0, 0)
        _Bounce("Warm Ground Bounce (rgb, a = amount)", Color) = (0.85, 0.62, 0.35, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _AlbedoBlur;
            half _NormalBlur;
            half _NormalStrength;
            half _Saturation;
            half _Wrap;
            half _RampSoftness;
            half _Ramp;
            half4 _ShadowTint;
            half4 _Terminator;
            half4 _AOTint;
            half _OcclusionStrength;
            half _Specular;
            half _SpecPower;
            half4 _SkinTint;
            half4 _RimColor;
            half _RimIntensity;
            half _RimPower;
            half _RimSunFacing;
            half4 _SunFill;
            float4 _FillDirection;
            half4 _Bounce;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"
            #include "JBPainterly.hlsl"

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
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, input.uv, _AlbedoBlur).rgb * _BaseColor.rgb;
                albedo = JBSaturation(albedo, _Saturation);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D_BIAS(_BumpMap, sampler_BumpMap, input.uv, _NormalBlur), _NormalStrength);
                half smoothness = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).a;
                half occlusion = lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);

                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half nDotL = dot(normalWS, mainLight.direction);
                half lit = JBPaintDiffuse(nDotL, _Wrap, _RampSoftness, _Ramp) * mainLight.shadowAttenuation;

                // Skin mask from the albedo's hue: warm (r > g > b), moderately saturated and light.
                half rg = albedo.r - albedo.g;
                half gb = albedo.g - albedo.b;
                half skin = smoothstep(0.04h, 0.1h, rg) * (1.0h - smoothstep(0.24h, 0.32h, rg)) * smoothstep(0.02h, 0.06h, gb)
                    * smoothstep(0.32h, 0.45h, albedo.r);

                JBPaintParams paint;
                paint.shadowTint = _ShadowTint.rgb;
                paint.shadowShift = _ShadowTint.a;
                paint.terminator = lerp(_Terminator.rgb, _SkinTint.rgb, skin);
                paint.terminatorAmount = lerp(_Terminator.a, _SkinTint.a, skin);
                paint.aoTint = _AOTint.rgb;
                paint.specular = _Specular * (1.0h - 0.5h * skin);
                paint.specPower = _SpecPower;
                half3 ambient = SampleSH(normalWS);
                half3 color = JBPaintShade(albedo, lit, mainLight.color, ambient, occlusion, normalWS, viewWS, mainLight.direction, smoothness, paint);

                // Warm fill from the visible sun's side (F4_f: the low sun wraps her shoulder, arm and hip) and a warm
                // bounce from the sunlit ground on the faces turned down; both under the painted occlusion.
                float3 fillDir = dot(_FillDirection.xyz, _FillDirection.xyz) > 1e-4 ? normalize(_FillDirection.xyz) : _JBSunDirection.xyz;
                half sunWrap = saturate(dot(normalWS, (half3)fillDir) * 0.5h + 0.5h);
                half below = saturate(-normalWS.y * 0.5h + 0.5h);
                color += albedo * occlusion * (_SunFill.rgb * (_SunFill.a * sunWrap * sunWrap) + _Bounce.rgb * (_Bounce.a * below));
                // Rim: always on, stronger on the edges facing the sun.
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                half3 toSun = normalize(_JBSunDirection.xyz + half3(0.0h, 0.3h, 0.0h));
                half facing = lerp(1.0h, saturate(dot(normalWS, toSun) * 0.75h + 0.35h), _RimSunFacing);
                color += _RimColor.rgb * (_RimIntensity * fresnel * facing * lerp(0.5h, 1.0h, mainLight.shadowAttenuation));

                color = JBApplyFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
