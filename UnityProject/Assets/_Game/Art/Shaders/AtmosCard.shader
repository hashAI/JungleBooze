// AURELIA look test v2 (ENVIRONMENT_STRATEGY 4.4): light shafts and mist as merged mesh cards.
// Each card is a quad whose four vertices carry the same centre (POSITION), the card axis (NORMAL), the corner
// (TEXCOORD0: x in -0.5..0.5, y in 0..1) and its size (TEXCOORD1: width, length, seed). The vertex shader turns the
// card around its axis to face the camera, so many cards of a segment merge into one mesh and one draw call.
// TEXCOORD1.w is the card kind for mist: 0 = mist billow, 1 = waterfall spray (brighter, rising, more broken).
// Beyond that range it is a density gain: w > 1 = spray with (w) x opacity, w < 0 = billow with (1 - w) x opacity
// (dense plumes at the foot of a big fall).
// Soft intersections without a depth texture (ADR 0008): mist cards are pulled toward the camera by part of their
// width (they stand in front of the rock or water they hug), their base fades out (no hard line where a card
// meets the ground or a pool), and they fade when seen edge-on or from too close.
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
        _SunScatter("Mist: sun in-scatter share (1 = full warm glow toward the sun)", Range(0, 1)) = 1
        _ScatterNeutral("Mist: neutral (white) share of the sun in-scatter (spray reads white, not gold)", Range(0, 1)) = 0
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
                half _SunScatter;
                half _ScatterNeutral;
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
                half kind : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 center = TransformObjectToWorld(input.positionOS.xyz);
                float3 axis = normalize(TransformObjectToWorldDir(input.normalOS));
                float3 toCameraRaw = _WorldSpaceCameraPos - center;
                float cameraDistance = length(toCameraRaw);
                float3 toCamera = toCameraRaw / max(cameraDistance, 1e-4);
            #if defined(_MIST)
                // Stand in front of whatever the card hugs: pull toward the camera by part of its width.
                center += toCamera * min(input.size.x * 0.35, cameraDistance * 0.4);
            #endif
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
                output.kind = input.size.w;
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
                half spray = saturate(input.kind);
                half density = 1.0h + max(input.kind - 1.0h, 0.0h) + max(-input.kind, 0.0h);
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u + 0.5, h)).a;
                float rise = t * lerp(0.006, 0.05, spray);
                half noise = SAMPLE_TEXTURE2D(_JBDappleTex, sampler_JBDappleTex, float2(u * lerp(0.7, 1.3, spray) + input.seed + t * 0.01, h * lerp(0.5, 0.9, spray) - rise)).r;
                half noise2 = SAMPLE_TEXTURE2D(_JBDappleTex, sampler_JBDappleTex, float2(u * 0.35 - input.seed * 0.7, h * 0.3 - rise * 0.6 + 0.37)).r;
                half billow = saturate(noise * 0.6h + noise2 * 0.6h - 0.1h);
                half shape = lerp(0.45h + 0.55h * billow, billow * billow * 1.6h, spray);
                half base = smoothstep(0.0h, lerp(0.35h, 0.15h, spray), h);
                half alpha = mask * shape * base * _JBMistColor.a * _Intensity * distanceFade * input.fade * lerp(1.0h, 1.35h, spray) * density;
                Light mainLight = GetMainLight();
                half glow = pow(toSun, 3.0h);
                half3 lit = _JBMistColor.rgb * (SampleSH(half3(0.0h, 1.0h, 0.0h)) * lerp(1.3h, 1.6h, spray) + lerp(mainLight.color, dot(mainLight.color, half3(0.3333h, 0.3333h, 0.3333h)).xxx, _ScatterNeutral) * (0.15h + 0.75h * glow) * lerp(1.0h, 1.3h, spray) * _SunScatter);
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
