// Editor-only measurement shader (HeroBasinOverdraw, ADR 0011). The hardware depth buffer is not used (it did not
// hold between command-buffer draws on the batch-mode Metal path): pass 0 writes the view depth as a colour with a
// Min blend (optional alpha clip from the source material's texture); pass 1 adds 1 per fragment whose own view
// depth is not behind that stored depth (a manual depth test; fragment count = shading work).
// _Card = 1 rebuilds the camera-facing Atmos Card quads (centre in POSITION, axis in NORMAL, corner in UV0, size in
// UV1) exactly as AtmosCard.shader does, so mist and shafts are counted with their real coverage.
Shader "Hidden/JungleBooze/Overdraw Count"
{
    Properties
    {
        _MainTex("Coverage (A)", 2D) = "white" {}
        _Cutoff("Cutoff", Float) = 0.5
        _Clip("Clip", Float) = 0
        _Card("Atmos Card", Float) = 0
        _MistPull("Mist Pull", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float _Cutoff;
    float _Clip;
    float _Card;
    float _MistPull;
    float4 _CountCameraPos;
    float4 _CountCameraFwd;
    float4 _CountScreen;
    sampler2D _CountDepth;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float4 size : TEXCOORD1;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float depth : TEXCOORD1;
    };

    v2f vert(appdata v)
    {
        v2f o;
        float3 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
        if (_Card > 0.5)
        {
            float3 axis = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
            float3 toCam = _CountCameraPos.xyz - world;
            float dist = length(toCam);
            toCam /= max(dist, 1e-4);
            world += toCam * min(v.size.x * 0.35, dist * 0.4) * _MistPull;
            float3 side = cross(axis, toCam);
            float sl = length(side);
            side = sl > 1e-3 ? side / sl : normalize(cross(axis, float3(0, 0, 1)));
            world = world + side * (v.uv.x * v.size.x) + axis * ((v.uv.y - 0.5) * v.size.y);
        }

        o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
        o.uv = v.uv;
        o.depth = dot(world - _CountCameraPos.xyz, _CountCameraFwd.xyz);
        return o;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            BlendOp Min
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                if (_Clip > 0.5)
                {
                    clip(tex2D(_MainTex, i.uv).a - _Cutoff);
                }

                return float4(i.depth, 0, 0, 0);
            }
            ENDCG
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float stored = tex2D(_CountDepth, i.pos.xy / _CountScreen.xy).r;
                clip(stored * 1.0005 + 0.02 - i.depth);
                return float4(1, 0, 0, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
