// AURELIA look test (ADR 0004): one URP lit shader for ground, rocks, bark and foliage.
// - Lighting: URP's own UniversalFragmentPBR (main light, cascaded/soft shadows, SH ambient, reflection probe), fog.
// - Poly Haven texture sets: albedo, OpenGL normal, ARM (R = ambient occlusion, G = roughness, B = metal).
// - _LAYERS_ON: three-layer height blend driven by vertex color (R = layer 2, G = layer 3), for generated ground.
// - _ALPHATEST_ON: alpha from a separate mask (R), clip + alpha-to-coverage (smooth edges with MSAA).
// - _WIND_ON: vertex sway; weight grows with height above the object's pivot, phase varies with world position.
// - Vertex color A is baked ambient occlusion on generated meshes (_VertexAO = 1); imported models use 0.
// - Look test v2: segments are merged into one mesh per material (ENVIRONMENT_STRATEGY 4.3), so the wind weight
//   comes from vertex color R (_WindVertexColor = 1) instead of the height above the pivot, and vertex color B
//   carries the canopy cover baked by the builder (_CanopyCover = 1): dappled sun and darker, greener ambient
//   under the roof (JBAtmosphere.hlsl). Fog is the project's height fog with sun in-scatter, not Unity fog.
// - _DETAIL_ON: tiling detail normal on UV1 (environment kit pieces: UV1 = world box projection, 1 unit = 4 m;
//   ADR 0008), blended over the unique normal so large pieces stay crisp up close.
// - Look test v2 surface response (ADR 0008), all per material, no extra textures or passes:
//   moss on upward faces (_MossAmount, broken up by the albedo's luminance); wet rock near falls (vertex color G
//   when _WetFromVertexG = 1: darker and glossier); ground bounce for downward faces (_BounceColor, lifts the
//   undersides of roots and arches that the sky ambient leaves black); a rim sheen (_RimStrength: Fresnel edge lit
//   by the ambient and, toward the sun, by the dappled sun: the "subsurface-ish" glow on bark edges).
// - _PAINTERLY (ADR 0009, painterly style trial): wrapped/ramped diffuse with teal-shifted shadows, a warm terminator
//   band, painted (tinted) occlusion and one soft specular lobe instead of URP PBR; albedo softened (mip bias) and
//   flattened toward its broad colour, broad normals only; foliage gets a core-to-tip gradient from vertex color R.
//   Off by default: the realistic materials never compile or run this path.
// SRP Batcher compatible (all material values in UnityPerMaterial), GPU instancing on.
Shader "JungleBooze/Nature Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal (OpenGL)", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _ArmMap("AO (R) Roughness (G) Metal (B)", 2D) = "white" {}
        _OcclusionStrength("AO Strength", Range(0, 1)) = 1
        _RoughnessScale("Roughness Scale", Range(0, 2)) = 1
        _MetallicScale("Metal Scale (ARM B)", Range(0, 1)) = 0
        _VertexAO("Vertex AO (color A)", Range(0, 1)) = 0

        [Header(Layers by vertex color)]
        [Toggle(_LAYERS_ON)] _Layers("Blend Layers", Float) = 0
        _Layer2Color("Layer 2 Tint", Color) = (1, 1, 1, 1)
        _Layer2Map("Layer 2 Albedo (vertex R)", 2D) = "white" {}
        [Normal] _Layer2BumpMap("Layer 2 Normal", 2D) = "bump" {}
        _Layer2ArmMap("Layer 2 ARM", 2D) = "white" {}
        _Layer3Color("Layer 3 Tint", Color) = (1, 1, 1, 1)
        _Layer3Map("Layer 3 Albedo (vertex G)", 2D) = "white" {}
        [Normal] _Layer3BumpMap("Layer 3 Normal", 2D) = "bump" {}
        _Layer3ArmMap("Layer 3 ARM", 2D) = "white" {}
        _HeightBlend("Height Blend", Range(0, 1)) = 0.5
        _BlendDepth("Blend Depth", Range(0.01, 1)) = 0.2

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _AlphaMap("Alpha Mask (R)", 2D) = "white" {}
        _AlphaFromBaseA("Alpha from Albedo A (atlases)", Range(0, 1)) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Translucency("Leaf Translucency", Range(0, 1)) = 0

        [Header(Wind)]
        [Toggle(_WIND_ON)] _Wind("Wind", Float) = 0
        _WindStrength("Wind Strength (m)", Float) = 0.1
        _WindSpeed("Wind Speed", Float) = 1
        _WindHeightScale("Wind Weight per m Height", Float) = 0.15
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.35, 0)
        _WindVertexColor("Wind Weight from Vertex Color R", Range(0, 1)) = 0
        _CanopyCover("Canopy Cover from Vertex Color B", Range(0, 1)) = 0

        [Header(Detail normal on UV1)]
        [Toggle(_DETAIL_ON)] _Detail("Detail Normal (UV1)", Float) = 0
        [Normal][NoScaleOffset] _DetailNormalMap("Detail Normal", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Strength", Range(0, 2)) = 0.3
        _DetailTiling("Detail Repeats per UV1 Unit", Float) = 1

        [Header(Surface response)]
        _MossColor("Moss Color", Color) = (0.30, 0.40, 0.15, 1)
        _MossAmount("Moss on Upward Faces", Range(0, 1)) = 0
        _WetFromVertexG("Wetness from Vertex Color G", Range(0, 1)) = 0
        _WetDarken("Wet Darkening", Range(0, 1)) = 0.45
        _WetSmoothness("Wet Smoothness", Range(0, 1)) = 0.8
        _BounceColor("Ground Bounce (rgb, a = strength)", Color) = (0.45, 0.42, 0.30, 0)
        _RimStrength("Rim Sheen", Range(0, 2)) = 0

        [Header(Painterly style (ADR 0009))]
        [Toggle(_PAINTERLY)] _Painterly("Painterly", Float) = 0
        _PaintBlur("Albedo Softening (mip bias)", Range(0, 4)) = 1
        _PaintFlatten("Hue Flattening", Range(0, 1)) = 0.4
        _PaintFlattenMip("Broad Colour Mip", Range(2, 9)) = 6
        _PaintSaturation("Saturation", Range(0, 2)) = 1.15
        _PaintNormal("Normal Strength (broad forms)", Range(0, 1)) = 0.5
        _PaintWrap("Diffuse Wrap", Range(0, 1)) = 0.4
        _PaintRampSoftness("Ramp Softness", Range(0.02, 0.5)) = 0.22
        _PaintRamp("Ramp Amount", Range(0, 1)) = 0.5
        _PaintShadowTint("Shadow Tint (rgb, a = shift)", Color) = (0.243, 0.361, 0.4, 0.35)
        _PaintTerminator("Terminator Band (rgb, a = amount)", Color) = (0.878, 0.541, 0.227, 0.25)
        _PaintAOTint("Painted AO Colour", Color) = (0.302, 0.227, 0.133, 1)
        _PaintSpecular("Soft Specular", Range(0, 1)) = 0.15
        _PaintSpecPower("Specular Power", Range(2, 64)) = 12
        _PaintRim("Rim Toward Sun (rgb, a = strength)", Color) = (1, 0.84, 0.54, 0.35)
        _PaintCore("Foliage Core Tint (rgb, a = amount)", Color) = (0.45, 0.6, 0.4, 0)
        _PaintTip("Foliage Tip Tint (rgb, a = amount)", Color) = (1.25, 1.15, 0.6, 0)
        _PaintBackLight("Foliage Backlight Colour (rgb, a = strength)", Color) = (0.79, 0.82, 0.25, 0)
        _PaintTint("Painted Albedo Tint", Color) = (1, 1, 1, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _AlphaToMask("Alpha To Mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "JBAtmosphere.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _OcclusionStrength;
            half _RoughnessScale;
            half _MetallicScale;
            half _VertexAO;
            float4 _Layer2Map_ST;
            float4 _Layer3Map_ST;
            half4 _Layer2Color;
            half4 _Layer3Color;
            half _HeightBlend;
            half _BlendDepth;
            half _Cutoff;
            half _Translucency;
            float _WindStrength;
            float _WindSpeed;
            float _WindHeightScale;
            float4 _WindDirection;
            half _Layers;
            half _AlphaClip;
            half _Wind;
            half _Cull;
            half _AlphaToMask;
            half _WindVertexColor;
            half _CanopyCover;
            half _AlphaFromBaseA;
            half _Detail;
            half _DetailNormalScale;
            float _DetailTiling;
            half4 _MossColor;
            half _MossAmount;
            half _WetFromVertexG;
            half _WetDarken;
            half _WetSmoothness;
            half4 _BounceColor;
            half _RimStrength;
            half _Painterly;
            half _PaintBlur;
            half _PaintFlatten;
            half _PaintFlattenMip;
            half _PaintSaturation;
            half _PaintNormal;
            half _PaintWrap;
            half _PaintRampSoftness;
            half _PaintRamp;
            half4 _PaintShadowTint;
            half4 _PaintTerminator;
            half4 _PaintAOTint;
            half _PaintSpecular;
            half _PaintSpecPower;
            half4 _PaintRim;
            half4 _PaintCore;
            half4 _PaintTip;
            half4 _PaintBackLight;
            half4 _PaintTint;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_ArmMap); SAMPLER(sampler_ArmMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        TEXTURE2D(_DetailNormalMap); SAMPLER(sampler_DetailNormalMap);
        TEXTURE2D(_Layer2Map);
        TEXTURE2D(_Layer2BumpMap);
        TEXTURE2D(_Layer2ArmMap);
        TEXTURE2D(_Layer3Map);
        TEXTURE2D(_Layer3BumpMap);
        TEXTURE2D(_Layer3ArmMap);

        // World-space sway. Weight: height above the object's pivot (meters) times _WindHeightScale, squared so the
        // base stays planted. Two sine octaves plus a small flutter; phase from world xz so neighbors differ.
        float3 ApplyWind(float3 positionWS, half4 color)
        {
        #if defined(_WIND_ON)
            float3 pivotWS = GetObjectToWorldMatrix()._m03_m13_m23;
            float weight = saturate((positionWS.y - pivotWS.y) * _WindHeightScale);
            weight *= weight;
            weight = lerp(weight, color.r, _WindVertexColor);
            float2 phaseOrigin = lerp(pivotWS.xz, floor(positionWS.xz * 0.25) * 4.0, _WindVertexColor);
            float phase = dot(phaseOrigin, float2(0.13, 0.17)) + _Time.y * _WindSpeed;
            float sway = sin(phase) * 0.65 + sin(phase * 2.3 + 1.7) * 0.35;
            float flutter = sin(_Time.y * _WindSpeed * 6.0 + dot(positionWS, float3(1.3, 0.7, 1.1))) * 0.15;
            float2 dir = normalize(_WindDirection.xz + float2(1e-4, 0.0));
            positionWS.xz += dir * (sway + flutter) * _WindStrength * weight;
            positionWS.y -= abs(sway) * _WindStrength * weight * 0.2;
        #endif
            return positionWS;
        }

        half SampleAlpha(float2 uv)
        {
            half mask = _AlphaFromBaseA > 0.5h
                ? SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a
                : SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).r;
            return mask * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _LAYERS_ON
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON
            #pragma shader_feature_local_fragment _DETAIL_ON
            #pragma shader_feature_local_fragment _PAINTERLY
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBPainterly.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = float4(input.uv, input.uv1 * _DetailTiling);
                output.normalWS = normals.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normals.tangentWS, sign);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv1 = TRANSFORM_TEX(input.uv.xy, _BaseMap);
                half alpha = 1.0h;
            #if defined(_ALPHATEST_ON)
                alpha = SampleAlpha(uv1);
                clip(alpha - _Cutoff);
                // Sharpen for alpha-to-coverage: crisp but antialiased edges with MSAA.
                alpha = saturate((alpha - _Cutoff) / max(fwidth(alpha), 0.0001h) + 0.5h);
            #endif

            #if defined(_PAINTERLY)
                // Painted surfaces: softened albedo (no photo micro-detail), hue flattened toward the broad colour,
                // broad normals only (the normal map is read blurred and weaker).
                half3 albedo = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv1, _PaintBlur).rgb;
                half3 broadColor = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv1, _PaintFlattenMip).rgb;
                albedo = JBSaturation(JBFlatten(albedo, broadColor, _PaintFlatten), _PaintSaturation) * _PaintTint.rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D_BIAS(_BumpMap, sampler_BumpMap, uv1, _PaintBlur + 1.0h), _BumpScale * _PaintNormal);
            #else
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1), _BumpScale);
            #endif
                half3 arm = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, uv1).rgb;

            #if defined(_LAYERS_ON)
                float2 uv2 = TRANSFORM_TEX(input.uv.xy, _Layer2Map);
                float2 uv3 = TRANSFORM_TEX(input.uv.xy, _Layer3Map);
                half3 albedo2 = SAMPLE_TEXTURE2D(_Layer2Map, sampler_BaseMap, uv2).rgb;
                half3 normal2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Layer2BumpMap, sampler_BumpMap, uv2), _BumpScale);
                half3 arm2 = SAMPLE_TEXTURE2D(_Layer2ArmMap, sampler_ArmMap, uv2).rgb;
                half3 albedo3 = SAMPLE_TEXTURE2D(_Layer3Map, sampler_BaseMap, uv3).rgb;
                half3 normal3 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Layer3BumpMap, sampler_BumpMap, uv3), _BumpScale);
                half3 arm3 = SAMPLE_TEXTURE2D(_Layer3ArmMap, sampler_ArmMap, uv3).rgb;

                // Height blend: the AO channel stands in for height (cavities are dark, tops are bright).
                half w2 = saturate(input.color.r);
                half w3 = saturate(input.color.g);
                half3 weights = half3(saturate(1.0h - w2 - w3), w2, w3);
                weights += half3(arm.r, arm2.r, arm3.r) * _HeightBlend;
                half top = max(weights.x, max(weights.y, weights.z)) - _BlendDepth;
                weights = max(weights - top, 0.0h);
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);

                // Each layer has its own tint; _BaseColor tints layer 1 (applied below for every mode).
                half3 baseTint = max(_BaseColor.rgb, half3(0.001h, 0.001h, 0.001h));
                albedo = albedo * weights.x + (albedo2 * _Layer2Color.rgb / baseTint) * weights.y + (albedo3 * _Layer3Color.rgb / baseTint) * weights.z;
                normalTS = normalize(normalTS * weights.x + normal2 * weights.y + normal3 * weights.z);
                arm = arm * weights.x + arm2 * weights.y + arm3 * weights.z;
            #endif

            #if defined(_DETAIL_ON) && !defined(_PAINTERLY)
                half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.uv.zw), _DetailNormalScale);
                normalTS = normalize(half3(normalTS.xy + detail.xy, normalTS.z * detail.z));
            #endif

                half faceSign = IS_FRONT_VFACE(face, 1.0h, -1.0h);
                half3 normalVS = input.normalWS * faceSign;
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, normalVS);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));

                half vertexAO = lerp(1.0h, input.color.a, _VertexAO);
                half3 surfaceAlbedo = albedo * _BaseColor.rgb;
                half smoothness = saturate(1.0h - arm.g * _RoughnessScale);
            #if !defined(_LAYERS_ON)
                // Moss on upward faces, wet rock near falls (vertex G). The layered ground uses G for its third layer.
                half luminance = dot(albedo, half3(0.3h, 0.59h, 0.11h));
                half moss = saturate((normalWS.y - (1.0h - _MossAmount)) * 4.0h + (luminance - 0.35h) * 1.5h) * step(0.001h, _MossAmount);
                surfaceAlbedo = lerp(surfaceAlbedo, _MossColor.rgb * (0.65h + 0.7h * luminance), moss);
                smoothness = lerp(smoothness, 0.12h, moss);
                half wet = saturate(input.color.g) * _WetFromVertexG;
                surfaceAlbedo *= 1.0h - _WetDarken * wet;
                smoothness = lerp(smoothness, _WetSmoothness, wet);
            #endif

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = surfaceAlbedo;
                surface.metallic = arm.b * _MetallicScale;
                surface.specular = half3(0.0h, 0.0h, 0.0h);
                surface.smoothness = smoothness;
                surface.normalTS = normalTS;
                surface.occlusion = lerp(1.0h, arm.r, _OcclusionStrength) * vertexAO;
                surface.alpha = alpha;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                // URP PBR with the main light and ambient modulated by the canopy (JBAtmosphere.hlsl).
                half cover = saturate(input.color.b) * _CanopyCover;
                BRDFData brdfData;
                half brdfAlpha = surface.alpha;
                InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, brdfAlpha, brdfData);
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                half canopyLit = JBCanopyLight(inputData.positionWS, cover);
                mainLight.shadowAttenuation *= canopyLit;
                // Ground bounce: downward faces get the warm light bounced off the forest floor.
                half facingDown = saturate(0.5h - 0.5h * normalWS.y);
                half3 bounce = _BounceColor.rgb * (_BounceColor.a * facingDown);
                half3 bakedGI = (inputData.bakedGI + bounce) * JBCanopyAmbient(cover);
                half giOcclusion = surface.occlusion * lerp(1.0h, 0.55h, cover);
            #if defined(_PAINTERLY)
                // Foliage gradient: dark core at the base / hanging point to warm sunlit tips (vertex color R = wind
                // weight, 0 at the base, 1 at the tips). Amounts are 0 on stone.
                half tip = saturate(input.color.r);
                surface.albedo *= lerp(half3(1.0h, 1.0h, 1.0h), _PaintCore.rgb, _PaintCore.a * (1.0h - tip));
                surface.albedo *= lerp(half3(1.0h, 1.0h, 1.0h), _PaintTip.rgb, _PaintTip.a * tip);
                JBPaintParams paint;
                paint.shadowTint = _PaintShadowTint.rgb;
                paint.shadowShift = _PaintShadowTint.a;
                paint.terminator = _PaintTerminator.rgb;
                paint.terminatorAmount = _PaintTerminator.a;
                paint.aoTint = _PaintAOTint.rgb;
                paint.specular = _PaintSpecular;
                paint.specPower = _PaintSpecPower;
                half paintLit = JBPaintDiffuse(dot(normalWS, mainLight.direction), _PaintWrap, _PaintRampSoftness, _PaintRamp)
                    * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half4 color = half4(JBPaintShade(surface.albedo, paintLit, mainLight.color, bakedGI, giOcclusion, normalWS,
                    inputData.viewDirectionWS, mainLight.direction, max(smoothness, 0.25h), paint), alpha);
                // Rim toward the sun side: a painted warm edge on silhouettes facing the light.
                half paintEdge = 1.0h - saturate(dot(normalWS, inputData.viewDirectionWS));
                paintEdge *= paintEdge * paintEdge;
                half sunSide = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                color.rgb += _PaintRim.rgb * mainLight.color * (paintEdge * sunSide * _PaintRim.a * canopyLit);
            #else
                half4 color = half4(GlobalIllumination(brdfData, bakedGI, giOcclusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS), alpha);
                color.rgb += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);
            #endif

                // Rim sheen: a Fresnel edge lit by the ambient, and by the (dappled) sun when looking toward it.
                half edge = 1.0h - saturate(dot(normalWS, inputData.viewDirectionWS));
                edge = edge * edge * edge;
                half towardSun = saturate(dot(-inputData.viewDirectionWS, mainLight.direction));
                half3 rimLight = bakedGI * 0.9h + mainLight.color * (towardSun * towardSun * canopyLit * 0.6h);
                color.rgb += rimLight * lerp(surface.albedo, half3(0.5h, 0.5h, 0.45h), 0.35h) * (edge * _RimStrength * (0.5h + 0.5h * surface.occlusion));

            #if defined(_ALPHATEST_ON)
                // Thin leaves: light from behind shines through (cheap wrap term, shadowed and dappled).
                half backLight = saturate(dot(-normalWS, mainLight.direction));
                backLight = backLight * backLight;
                color.rgb += surface.albedo * mainLight.color * backLight * mainLight.shadowAttenuation * mainLight.distanceAttenuation * _Translucency;
            #if defined(_PAINTERLY)
                // The golden glow of backlit leaves (ART_DIRECTION_PAINTERLY s5): view toward the sun, power 4.
                half glow = saturate(dot(-inputData.viewDirectionWS, mainLight.direction));
                glow *= glow;
                glow *= glow;
                color.rgb += _PaintBackLight.rgb * mainLight.color * (glow * _PaintBackLight.a * (0.4h + 0.6h * tip) * mainLight.shadowAttenuation * canopyLit);
            #endif
            #endif

                color.rgb = JBApplyFog(color.rgb, inputData.positionWS);
                color.a = alpha;
                return color;
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP for the light being rendered (directional only in this project: ADR 0004).
            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(_ALPHATEST_ON)
                clip(SampleAlpha(input.uv) - _Cutoff);
            #endif
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
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_vertex _WIND_ON
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformWorldToHClip(ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(_ALPHATEST_ON)
                clip(SampleAlpha(input.uv) - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
