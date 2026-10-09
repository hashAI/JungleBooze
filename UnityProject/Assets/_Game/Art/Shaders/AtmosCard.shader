// AURELIA look test v2 (ENVIRONMENT_STRATEGY 4.4): light shafts and mist as merged mesh cards.
// Each card is a quad whose four vertices carry the same centre (POSITION), the card axis (NORMAL), the corner
// (TEXCOORD0: x in -0.5..0.5, y in 0..1) and its size (TEXCOORD1: width, length, seed). The vertex shader turns the
// card around its axis to face the camera, so many cards of a segment merge into one mesh and one draw call.
// Shafts (default): additive gold, brightest when looking toward the sun, faded when seen along their axis, near
//   the camera (never across the near trail) and in fog.
// Mist (_MIST): alpha-blended soft billows lit by the sky ambient and a warm in-scatter toward the sun.
Shader "JungleBooze/Atmos Card"
{
    Properties
    {
        [Toggle(_MIST)] _Mist("Mist (alpha blended)", Float) = 0
        _MainTex("Soft Mask (A)", 2D) = "white" {}
        _Intensity("Intensity", Range(0, 4)) = 1
        _NearFade("Near Fade Start, End (m)", Vector) = (6, 16, 0, 0)
        _FarFade("Far Fade Start, End (m)", Vector) = (400, 900, 0, 0)
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _MIST

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                float4 _NearFade;
                float4 _FarFade;
                half _Mist;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 corner : TEXCOORD0;
                float4 size : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fade : TEXCOORD2;
                half seed : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(input.positionOS.xyz);
                float3 axis = normalize(TransformObjectToWorldDir(input.normalOS));
                float3 toCamera = normalize(_WorldSpaceCameraPos - center);
                float3 side = cross(axis, toCamera);
                float sideLength = length(side);
                side = sideLength > 1e-3 ? side / sideLength : normalize(cross(axis, float3(0.0, 0.0, 1.0)));
                float3 positionWS = center + side * (input.corner.x * input.size.x) + axis * ((input.corner.y - 0.5) * input.size.y);

                // Seen along the axis the card is a thin sliver: fade it out.
                float along = abs(dot(toCamera, axis));
                output.fade = 1.0h - smoothstep(0.7, 0.97, along);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.corner = input.corner;
                output.seed = input.size.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 v = input.positionWS - _WorldSpaceCameraPos;
                float dist = length(v);
                float3 viewDir = v / max(dist, 1e-4);
                half distanceFade = smoothstep(_NearFade.x, _NearFade.y, dist) * (1.0h - smoothstep(_FarFade.x, _FarFade.y, dist));
                half toSun = max(saturate(dot(viewDir, _JBSunDirection.xyz)), 1e-4h);
                float u = input.corner.x;
                float h = input.corner.y;
                float t = _Time.y;

            #if defined(_MIST)
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u + 0.5, h)).a;
                half noise = SAMPLE_TEXTURE2D(_JBDappleTex, sampler_JBDappleTex, float2(u * 0.7 + input.seed + t * 0.01, h * 0.5 - t * 0.006)).r;
                half alpha = mask * (0.55h + 0.45h * noise) * _JBMistColor.a * _Intensity * distanceFade * input.fade;
                Light mainLight = GetMainLight();
                half3 lit = _JBMistColor.rgb * (SampleSH(half3(0.0h, 1.0h, 0.0h)) * 1.3h + mainLight.color * (0.12h + 0.6h * pow(toSun, 3.0h)));
                lit = JBApplyFog(lit, input.positionWS);
                return half4(lit, saturate(alpha));
            #else
                half edge = saturate(1.0h - abs(u) * 2.0h);
                edge *= edge;
                half ends = smoothstep(0.0h, 0.35h, h) * (1.0h - smoothstep(0.7h, 1.0h, h));
                half streaks = 0.55h + 0.45h * sin(u * 19.0 + input.seed * 6.3) * sin(u * 7.3 + input.seed * 2.9 + h * 1.7);
                half dust = SAMPLE_TEXTURE2D(_JBDappleTex, sampler_JBDappleTex, float2(u * 0.5 + input.seed, h * 0.35 - t * 0.02)).r;
                half phase = lerp(0.25h, 1.6h, pow(toSun, 4.0h));
                half amount = edge * ends * streaks * (0.6h + 0.4h * dust) * phase * distanceFade * input.fade * JBFogKeep(input.positionWS);
                return half4(_JBShaftColor.rgb * (_Intensity * amount), 0.0h);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
