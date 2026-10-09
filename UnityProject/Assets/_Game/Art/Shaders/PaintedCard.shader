// AURELIA painterly v4 (ADR 0011): painted matte cards and impostors (the hero arch card, distant pillars).
// Opaque-queue, depth-writing, alpha-to-coverage (MSAA) instead of alpha blending: everything behind a card is
// depth-rejected (no stacked transparent layers), and the painted silhouette stays soft at the MSAA sample rate.
// Without MSAA (Low tier) it degrades to a plain alpha test. Unlit: the painting carries its own light; part of the
// project fog is applied (_FogAmount) so the card sits in the same atmosphere as the 3D world.
// UV0 = atlas UV (the builder maps each card to its rectangle). Vertex color A fades the card (bottom mist fade).
// Painted water (_FlowSlide > 0, e.g. the far fall card): the bright, unsaturated pixels of the painting (its water)
// slide down in two cross-faded phases (a cheap flipbook along the flow); rock and foliage stay still.
Shader "JungleBooze/Painted Card"
{
    Properties
    {
        [MainTexture] _MainTex("Painting (RGBA, straight alpha)", 2D) = "white" {}
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure", Range(0, 4)) = 1
        _FogAmount("Project Fog Share", Range(0, 1)) = 0.2
        _Cutoff("Coverage Cutoff", Range(0.05, 0.95)) = 0.5
        _MipBias("Softening (mip bias)", Range(-1, 3)) = 0.25
        _FlowSpeed("Water Flow (cycles per s)", Float) = 0.5
        _FlowSlide("Water Slide (UV per cycle, 0 = off)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "JBAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Exposure;
                half _FogAmount;
                half _Cutoff;
                half _MipBias;
                float _FlowSpeed;
                float _FlowSlide;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fade : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.fade = input.color.a;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, input.uv, _MipBias);
                if (_FlowSlide > 0.0)
                {
                    half luma = dot(c.rgb, half3(0.299h, 0.587h, 0.114h));
                    half chroma = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
                    half water = saturate((luma - 0.5h) * 4.0h) * saturate(1.0h - chroma * 4.0h);
                    float cycle = _Time.y * _FlowSpeed;
                    float p0 = frac(cycle);
                    float p1 = frac(cycle + 0.5);
                    half w0 = 1.0h - abs(p0 * 2.0 - 1.0);
                    half3 f0 = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, input.uv + float2(0.0, p0 * _FlowSlide), _MipBias).rgb;
                    half3 f1 = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, input.uv + float2(0.0, p1 * _FlowSlide), _MipBias).rgb;
                    c.rgb = lerp(c.rgb, lerp(f1, f0, w0), water);
                }

                c *= _Tint;
                c.rgb *= _Exposure;
                c.rgb = lerp(c.rgb, JBApplyFog(c.rgb, input.positionWS), _FogAmount);
                // Sharpened coverage: a one-pixel ramp around the cutoff, so alpha-to-coverage gives a clean
                // anti-aliased edge (with MSAA) and a plain cutout without it.
                half a = c.a * input.fade;
                half ramp = max(fwidth(a), 1e-3h);
                a = saturate((a - _Cutoff) / ramp + 0.5h);
                clip(a - 0.01h);
                return half4(c.rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
