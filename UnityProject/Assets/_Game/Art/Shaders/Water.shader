// AURELIA look test (ADR 0004, ADR 0008): river, ford and pool water for mobile.
// No refraction, no opaque or depth texture, no planar reflection. One alpha-blended pass:
// two scrolling normal maps -> Fresnel blend between a tinted body color and the sky reflection (reflection probe /
// baked skybox), sun specular with shadows. Vertex color: R = foam (shoreline, impact), G = depth 0..1 (baked by
// the builder: shallow water is turquoise and clear, deep water darker and more opaque; replaces a depth texture),
// A = soft edge. Foam is lacy: the water FX texture (G) thresholds it into lines that drift with the flow.
// Waterfalls use their own shader (Waterfall.shader).
Shader "JungleBooze/Water"
{
    Properties
    {
        [Normal][NoScaleOffset] _NormalMap("Normal Map (tileable)", 2D) = "bump" {}
        _NormalTiling("Normal Tiling (per m)", Float) = 0.35
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.6
        _FlowA("Flow A (uv per s)", Vector) = (0.02, 0.35, 0, 0)
        _FlowB("Flow B (uv per s)", Vector) = (-0.03, 0.22, 0, 0)
        _ShallowColor("Shallow Color", Color) = (0.20, 0.38, 0.33, 1)
        _DeepColor("Deep Color", Color) = (0.04, 0.12, 0.11, 1)
        _Opacity("Opacity (facing)", Range(0, 1)) = 0.72
        _FresnelPower("Fresnel Power", Range(1, 8)) = 4
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _SpecularStrength("Sun Specular", Range(0, 4)) = 1.5
        _FoamColor("Foam Color", Color) = (0.92, 0.95, 0.93, 1)
        _FoamStrength("Foam Strength", Range(0, 2)) = 1
        [NoScaleOffset] _FxTex("Water FX (G lacy foam)", 2D) = "gray" {}
        _FoamTiling("Foam Tiling (per m)", Float) = 0.45
        _ShallowOpacity("Opacity (shallow)", Range(0, 1)) = 0.45
        _ReflectionTint("Reflection Tint", Color) = (0.80, 0.95, 0.98, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NormalTiling;
                half _NormalStrength;
                float4 _FlowA;
                float4 _FlowB;
                half4 _ShallowColor;
                half4 _DeepColor;
                half _Opacity;
                half _FresnelPower;
                half _ReflectionStrength;
                half _Smoothness;
                half _SpecularStrength;
                half4 _FoamColor;
                half _FoamStrength;
                float _FoamTiling;
                half _ShallowOpacity;
                half4 _ReflectionTint;
                half _Cull;
            CBUFFER_END

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FxTex); SAMPLER(sampler_FxTex);

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
                float2 uvMeters : TEXCOORD5;
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
                output.uv = input.uv * _NormalTiling;
                output.uvMeters = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y;
                half4 sampleA = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv + t * _FlowA.xy);
                half4 sampleB = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * 1.7 + float2(0.37, 0.11) + t * _FlowB.xy);
                half3 nA = UnpackNormalScale(sampleA, _NormalStrength);
                half3 nB = UnpackNormalScale(sampleB, _NormalStrength);
                half3 normalTS = normalize(half3(nA.xy + nB.xy, nA.z * nB.z));

                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half facing = saturate(dot(normalWS, viewWS));
                half fresnel = pow(1.0h - facing, _FresnelPower);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;
                half nDotL = saturate(dot(normalWS, mainLight.direction));

                // Body: shallow (turquoise, clear) to deep by the baked depth (G), a little deeper at grazing angles.
                half depth = saturate(input.color.g);
                half3 ambient = SampleSH(normalWS);
                half bodyMix = saturate(depth * 0.85h + (1.0h - facing) * 0.25h);
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, bodyMix) * (ambient + mainLight.color * nDotL * shadow * 0.35h);

                half perceptualRoughness = 1.0h - _Smoothness;
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), perceptualRoughness, 1.0h) * _ReflectionTint.rgb;

                half3 halfDir = SafeNormalize(mainLight.direction + viewWS);
                half specPower = exp2(10.0h * _Smoothness + 1.0h);
                half3 specular = mainLight.color * pow(saturate(dot(normalWS, halfDir)), specPower) * _SpecularStrength * shadow;

                half3 color = lerp(body, reflection, saturate(fresnel * _ReflectionStrength)) + specular;

                // Foam: vertex color R thresholded by lacy foam lines that drift with the flow.
                float2 foamUv = input.uvMeters * _FoamTiling + t * _FlowA.xy * 0.35 / max(_NormalTiling, 1e-3) * _FoamTiling;
                half lace = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, foamUv).g;
                half lace2 = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, foamUv * 1.9 + float2(0.31, 0.17) - t * 0.02).g;
                half breakup = saturate((sampleA.r + sampleB.g) - 0.6h);
                half foamField = saturate(input.color.r * _FoamStrength);
                half foam = saturate(smoothstep(1.0h - foamField, 1.0h - foamField * 0.5h + 0.05h, max(lace, lace2 * 0.8h) + breakup * 0.3h) * foamField * 1.4h + foamField * foamField * 0.5h);
                half3 foamLit = _FoamColor.rgb * (ambient + mainLight.color * (0.4h + 0.6h * shadow));
                color = lerp(color, foamLit, foam);

                half alpha = saturate(lerp(lerp(_ShallowOpacity, _Opacity, depth), 1.0h, fresnel) + foam) * input.color.a;
                color = JBApplyFog(color, input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
