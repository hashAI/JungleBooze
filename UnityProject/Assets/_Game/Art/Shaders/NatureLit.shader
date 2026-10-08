// AURELIA look test (ADR 0004): one URP lit shader for ground, rocks, bark and foliage.
// - Lighting: URP's own UniversalFragmentPBR (main light, cascaded/soft shadows, SH ambient, reflection probe), fog.
// - Poly Haven texture sets: albedo, OpenGL normal, ARM (R = ambient occlusion, G = roughness, B = metal).
// - _LAYERS_ON: three-layer height blend driven by vertex color (R = layer 2, G = layer 3), for generated ground.
// - _ALPHATEST_ON: alpha from a separate mask (R), clip + alpha-to-coverage (smooth edges with MSAA).
// - _WIND_ON: vertex sway; weight grows with height above the object's pivot, phase varies with world position.
// - Vertex color A is baked ambient occlusion on generated meshes (_VertexAO = 1); imported models use 0.
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
        _Layer2Map("Layer 2 Albedo (vertex R)", 2D) = "white" {}
        [Normal] _Layer2BumpMap("Layer 2 Normal", 2D) = "bump" {}
        _Layer2ArmMap("Layer 2 ARM", 2D) = "white" {}
        _Layer3Map("Layer 3 Albedo (vertex G)", 2D) = "white" {}
        [Normal] _Layer3BumpMap("Layer 3 Normal", 2D) = "bump" {}
        _Layer3ArmMap("Layer 3 ARM", 2D) = "white" {}
        _HeightBlend("Height Blend", Range(0, 1)) = 0.5
        _BlendDepth("Blend Depth", Range(0.01, 1)) = 0.2

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _AlphaMap("Alpha Mask (R)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Translucency("Leaf Translucency", Range(0, 1)) = 0

        [Header(Wind)]
        [Toggle(_WIND_ON)] _Wind("Wind", Float) = 0
        _WindStrength("Wind Strength (m)", Float) = 0.1
        _WindSpeed("Wind Speed", Float) = 1
        _WindHeightScale("Wind Weight per m Height", Float) = 0.15
        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.35, 0)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _AlphaToMask("Alpha To Mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_ArmMap); SAMPLER(sampler_ArmMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        TEXTURE2D(_Layer2Map);
        TEXTURE2D(_Layer2BumpMap);
        TEXTURE2D(_Layer2ArmMap);
        TEXTURE2D(_Layer3Map);
        TEXTURE2D(_Layer3BumpMap);
        TEXTURE2D(_Layer3ArmMap);

        // World-space sway. Weight: height above the object's pivot (meters) times _WindHeightScale, squared so the
        // base stays planted. Two sine octaves plus a small flutter; phase from world xz so neighbors differ.
        float3 ApplyWind(float3 positionWS)
        {
        #if defined(_WIND_ON)
            float3 pivotWS = GetObjectToWorldMatrix()._m03_m13_m23;
            float weight = saturate((positionWS.y - pivotWS.y) * _WindHeightScale);
            weight *= weight;
            float phase = dot(pivotWS.xz, float2(0.13, 0.17)) + _Time.y * _WindSpeed;
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
            return SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).r * _BaseColor.a;
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz));
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.normalWS = normals.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normals.tangentWS, sign);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv1 = TRANSFORM_TEX(input.uv, _BaseMap);
                half alpha = 1.0h;
            #if defined(_ALPHATEST_ON)
                alpha = SampleAlpha(uv1);
                clip(alpha - _Cutoff);
                // Sharpen for alpha-to-coverage: crisp but antialiased edges with MSAA.
                alpha = saturate((alpha - _Cutoff) / max(fwidth(alpha), 0.0001h) + 0.5h);
            #endif

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1), _BumpScale);
                half3 arm = SAMPLE_TEXTURE2D(_ArmMap, sampler_ArmMap, uv1).rgb;

            #if defined(_LAYERS_ON)
                float2 uv2 = TRANSFORM_TEX(input.uv, _Layer2Map);
                float2 uv3 = TRANSFORM_TEX(input.uv, _Layer3Map);
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

                albedo = albedo * weights.x + albedo2 * weights.y + albedo3 * weights.z;
                normalTS = normalize(normalTS * weights.x + normal2 * weights.y + normal3 * weights.z);
                arm = arm * weights.x + arm2 * weights.y + arm3 * weights.z;
            #endif

                half faceSign = IS_FRONT_VFACE(face, 1.0h, -1.0h);
                half3 normalVS = input.normalWS * faceSign;
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, normalVS);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));

                half vertexAO = lerp(1.0h, input.color.a, _VertexAO);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo * _BaseColor.rgb;
                surface.metallic = arm.b * _MetallicScale;
                surface.specular = half3(0.0h, 0.0h, 0.0h);
                surface.smoothness = saturate(1.0h - arm.g * _RoughnessScale);
                surface.normalTS = normalTS;
                surface.occlusion = lerp(1.0h, arm.r, _OcclusionStrength) * vertexAO;
                surface.alpha = alpha;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                half4 color = UniversalFragmentPBR(inputData, surface);

            #if defined(_ALPHATEST_ON)
                // Thin leaves: light from behind shines through (cheap wrap term, shadowed).
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half backLight = saturate(dot(-normalWS, mainLight.direction));
                color.rgb += surface.albedo * mainLight.color * backLight * mainLight.shadowAttenuation * _Translucency;
            #endif

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
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

                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz));
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
                output.positionCS = TransformWorldToHClip(ApplyWind(TransformObjectToWorld(input.positionOS.xyz)));
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
