// AURELIA look test v2 (ENVIRONMENT_STRATEGY 4.5, ADR 0008): layered waterfall sheets for mobile.
// One alpha-blended pass, no depth or opaque texture. A fall is 3-4 merged sheets (core, front veil, side spray
// streaks) built by LookTestMeshFactory.Waterfall; all of them use this shader, so a segment's falls are one draw.
// - Streaks: two samples of the water FX texture (R) scrolling down at different speeds; foam cells (G) tumble
//   slower. Coordinates are in meters (UV0: x across, y height above the plunge point), so every fall has the
//   same texel density.
// - Vertex color: R = foam (lip and plunge), G = layer speed (0..1 -> 0.6..1.4 x), B = seed (pattern offset),
//   A = edge coverage. Ragged edges come from thresholding the coverage with the streaks, not from alpha cards.
// - _PAINTERLY (ADR 0009): bold white clumps (the white field is cut into 2 painted tones with crisp edges), warm
//   white in the light (#EFD4B0) and a cool shadowed white on the side away from it (#BDAE99), teal water between.
// - Light: wrapped sun + sky ambient, a backlit glow when the sun is behind the sheet, shadows; project fog.
Shader "JungleBooze/Waterfall"
{
    Properties
    {
        [NoScaleOffset] _FxTex("Water FX (R streaks, G foam, B breakup)", 2D) = "gray" {}
        _StreakTiling("Streak Tiling (x across per m, y down per m)", Vector) = (0.16, 0.035, 0, 0)
        _FlowSpeed("Flow Speed (m/s)", Float) = 9
        _WaterColor("Water Color", Color) = (0.52, 0.74, 0.74, 1)
        _FoamColor("Foam Color", Color) = (0.96, 0.97, 0.96, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.9
        _EdgeBreakup("Edge Breakup", Range(0, 1)) = 0.65
        _Translucency("Backlit Glow", Range(0, 2)) = 0.9
        _AmbientBoost("Ambient Boost", Range(0, 3)) = 1.25
        _WhiteBias("White Water Bias (aerated core)", Range(0, 1)) = 0.3
        _FoamGlow("Foam Glow (white water lit by scattered sky and spray)", Range(0, 2)) = 0
        [Header(Painterly style (ADR 0009))]
        [Toggle(_PAINTERLY)] _Painterly("Painterly", Float) = 0
        _PaintWhiteCut("White Clump Threshold", Range(0, 1)) = 0.45
        _PaintEdge("Clump Edge Softness", Range(0.01, 0.3)) = 0.06
        _PaintLitWhite("Lit White", Color) = (0.94, 0.86, 0.74, 1)
        _PaintShadeWhite("Shadowed White", Color) = (0.74, 0.72, 0.68, 1)
        _PaintStreakShade("Streak Shading (painted strokes)", Range(0, 1)) = 0.35
        _Clumping("Clumping (slow columns of denser and thinner water across the sheet)", Range(0, 1)) = 0
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
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma shader_feature_local_fragment _PAINTERLY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _StreakTiling;
                float _FlowSpeed;
                half4 _WaterColor;
                half4 _FoamColor;
                half _Opacity;
                half _EdgeBreakup;
                half _Translucency;
                half _AmbientBoost;
                half _WhiteBias;
                half _FoamGlow;
                half _Clumping;
                half _Painterly;
                half _PaintWhiteCut;
                half _PaintEdge;
                half4 _PaintLitWhite;
                half4 _PaintShadeWhite;
                half _PaintStreakShade;
            CBUFFER_END

            TEXTURE2D(_FxTex); SAMPLER(sampler_FxTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float speed = _FlowSpeed * lerp(0.6, 1.4, input.color.g);
                float seed = input.color.b * 7.31;
                float2 uv = input.uv;

                // Two streak layers falling at different speeds, and slower tumbling foam cells.
                half streakA = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, float2(uv.x * _StreakTiling.x + seed, (uv.y + t * speed) * _StreakTiling.y)).r;
                half streakB = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, float2(uv.x * _StreakTiling.x * 1.9 + seed * 0.37, (uv.y + t * speed * 1.35) * _StreakTiling.y * 1.7)).r;
                half4 cells = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, float2(uv.x * 0.22 + seed * 0.5, (uv.y + t * speed * 0.8) * 0.09));
                half streak = saturate(streakA * 0.65h + streakB * 0.55h - 0.1h);

                half foam = saturate(input.color.r * (0.7h + 0.6h * cells.g) + cells.g * 0.35h * cells.b);
                // White water where the streaks and foam are, blue-grey water between them and toward the edges.
                half white = saturate(_WhiteBias * input.color.a + streak * 0.75h - (1.0h - input.color.a) * 0.25h + foam);
                // Clumping: broad columns of dense white water with thinner, darker water between them, drifting slowly
                // (keyframe: a big plunge is ropes of white, never an even sheet).
                half columns = SAMPLE_TEXTURE2D(_FxTex, sampler_FxTex, float2(uv.x * 0.045 + seed * 0.21, (uv.y + t * speed * 0.5) * 0.006)).b;
                half clump = lerp(1.0h, saturate(0.35h + columns * 1.3h), _Clumping);
                white *= clump;
                half3 albedo = lerp(_WaterColor.rgb, _FoamColor.rgb, white);

                // Ragged edges: coverage thresholded by the streaks.
                half coverage = input.color.a * (1.0h - _EdgeBreakup + _EdgeBreakup * 1.8h * (streak * 0.7h + cells.b * 0.3h));
                half alpha = smoothstep(0.12h, 0.5h, coverage * lerp(1.0h, 0.6h + 0.6h * clump, _Clumping)) * _Opacity * saturate(0.8h + white);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 normalWS = normalize(input.normalWS);
                normalWS = dot(normalWS, viewWS) < 0.0h ? -normalWS : normalWS;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(0.35h, 1.0h, mainLight.shadowAttenuation);
                half wrap = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half back = pow(saturate(dot(-viewWS, mainLight.direction)), 4.0h) * _Translucency;
                half3 ambient = (SampleSH(normalWS) * 0.7h + SampleSH(half3(0.0h, 1.0h, 0.0h)) * 0.5h) * _AmbientBoost;
                half3 lit = albedo * (ambient + mainLight.color * (wrap * 0.55h + back * (0.5h + white)) * shadow);
                // Aerated water scatters light inside the spray: the white core stays bright even out of the sun.
                lit += _FoamColor.rgb * white * white * _FoamGlow * (ambient + mainLight.color * 0.25h);

            #if defined(_PAINTERLY)
                // Painted falls: the white field becomes bold clumps with crisp edges; each clump is warm white where
                // the light reaches it and a cool white on its shadow side (streaks act as painted strokes).
                half clumpP = smoothstep(_PaintWhiteCut - _PaintEdge, _PaintWhiteCut + _PaintEdge, white);
                half sunP = saturate(wrap * shadow * 1.4h + back * 0.5h) * lerp(1.0h, 0.55h + streak * 0.9h, _PaintStreakShade);
                half3 whiteP = lerp(_PaintShadeWhite.rgb * (ambient + 0.25h), _PaintLitWhite.rgb * (mainLight.color * 0.8h + ambient * 0.5h), saturate(sunP));
                half3 waterP = _WaterColor.rgb * (ambient + mainLight.color * wrap * 0.4h * shadow);
                lit = lerp(waterP, whiteP, clumpP) + _FoamColor.rgb * clumpP * _FoamGlow * 0.3h * ambient;
                alpha = max(alpha * lerp(0.75h, 1.0h, clumpP), clumpP * smoothstep(0.1h, 0.35h, coverage) * _Opacity);
            #endif
                lit = JBApplyFog(lit, input.positionWS);
                return half4(lit, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
