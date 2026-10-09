// AURELIA painterly style trial (ADR 0009, design/aurelia/ART_DIRECTION_PAINTERLY.md s4): shared lighting helpers for
// the _PAINTERLY variants of Nature Lit, Water, Waterfall, Backdrop Card and Character Painterly.
// "Real light on simplified, hand-painted forms": wrapped diffuse with a soft ramp, shadows shifted toward a cool
// teal instead of going grey, a warm saturated band at the terminator, painted (tinted, never black) occlusion,
// one soft specular lobe. Cheaper than URP PBR (no GGX, no environment BRDF): a few MADs and one pow.
// Include after URP Lighting.hlsl. Every value comes from the material (SRP Batcher) or the caller.
#ifndef JB_PAINTERLY_INCLUDED
#define JB_PAINTERLY_INCLUDED

half JBLuma(half3 c)
{
    return dot(c, half3(0.299h, 0.587h, 0.114h));
}

half3 JBSaturation(half3 c, half saturation)
{
    return max(lerp(JBLuma(c).xxx, c, saturation), 0.0h);
}

// Limited palette: pulls the local hue toward the texture's broad colour (a high mip) while keeping the value
// structure, so photo hue noise becomes broad painted mottling.
half3 JBFlatten(half3 albedo, half3 broad, half amount)
{
    half3 unified = broad * (JBLuma(albedo) / max(JBLuma(broad), 0.02h));
    return lerp(albedo, unified, amount);
}

// Wrapped diffuse ((N.L + w) / (1 + w)) shaped by a soft ramp around its middle. ramp 0 = plain wrap.
half JBPaintDiffuse(half nDotL, half wrap, half rampSoftness, half ramp)
{
    half wrapped = saturate((nDotL + wrap) / (1.0h + wrap));
    half banded = smoothstep(0.5h - rampSoftness, 0.5h + rampSoftness, wrapped);
    return lerp(wrapped, banded, ramp);
}

struct JBPaintParams
{
    half3 shadowTint;    // colour the unlit side drifts toward (ART_DIRECTION_PAINTERLY: #3E5C66)
    half shadowShift;    // 0..1 share of that drift
    half3 terminator;    // warm band at the light/shadow boundary (#E08A3A)
    half terminatorAmount;
    half3 aoTint;        // painted occlusion colour (warm brown, never black)
    half specular;       // soft lobe intensity (<= 0.25 on stone and leaves)
    half specPower;
};

// Diffuse + soft specular for one surface. lit = shaped diffuse * shadow (0..1), ambient = SH (already canopy- and
// bounce-adjusted by the caller), occlusion 0..1.
half3 JBPaintShade(half3 albedo, half lit, half3 sunColor, half3 ambient, half occlusion, half3 normalWS, half3 viewWS,
    half3 lightDir, half smoothness, JBPaintParams p)
{
    // Painted occlusion: crevices go toward a warm brown, not black.
    half3 ao = lerp(p.aoTint, half3(1.0h, 1.0h, 1.0h), occlusion);
    // Colour-shifted shadow: the ambient-only side drifts toward the cool tint at the same brightness.
    half3 cool = p.shadowTint * (JBLuma(ambient) / max(JBLuma(p.shadowTint), 0.02h));
    half3 shadowSide = lerp(ambient, cool, p.shadowShift * (1.0h - lit));
    half band = saturate(lit * (1.0h - lit) * 4.0h);
    band *= band;
    half3 light = shadowSide * ao + sunColor * lit + p.terminator * (band * p.terminatorAmount) * sunColor;
    half3 color = albedo * light;
    half3 h = SafeNormalize(lightDir + viewWS);
    half spec = pow(saturate(dot(normalWS, h)), p.specPower) * p.specular * smoothness * lit;
    return color + sunColor * spec;
}

#endif
