# Painterly / Stylized-Realistic Art Direction (TRIAL)

**Owner:** art-director | **Status:** Trial on the hero basin scene only (owner decision 2026-10-09, Option D) |
**Target image:** `design/aurelia/keyframes/F4_f_painterly_openai.jpg` (F4_f). Realistic reference for contrast:
`F4_e_openai_medium.jpg` (F4_e).

This file **overrides** `ART_DIRECTION.md` sections 2, 3.1, 4 and 10.2 for the trial. Everything else there still
applies unchanged: route-cue colors and carriers (s6), game-reserved colors (coin `#FFD23F`/`#2EC4B6`, hazard
`#9E2238`), value order (s3.4), camera (s9), originality bans (s5.3), Pista identity (s7.1–7.4).

## 1. The difference in one line

F4_e is a photo with lots of micro-detail (bark pits, lichen speckle, noisy moss, grey-white stone). F4_f keeps the
same light, depth and scale but **paints it**: big simple forms, warm sandstone instead of grey bark, clean leaf
clusters, saturated turquoise water, golden air. Rule: *real light on simplified, hand-painted forms.*

## 2. Shape language

- **Big-medium-small at 60/30/10.** Each asset reads as 1 big mass, 2–4 medium forms, few small details. No detail smaller than ~8 px at 1170 px wide on the phone.
- **Chunky braids.** Root-arch strands are thick, rounded tubes (strand diameter ~1/6–1/8 of arch thickness, 5–8 strands, not 20). Bevelled, pillowy edges; no cracks inside a strand, only clean seams between strands.
- **Pillars and cliffs:** stacked rounded slabs with flat sunlit tops carrying a moss/foliage cap. Silhouettes slightly exaggerated (taller, narrower) vs. real karst.
- **Rocks in water:** soft lozenges with mossy domes; 3 sizes, never gravel noise.
- **Silhouette test:** every hero asset must be identifiable as a flat black shape at 64 px tall.
- Gameplay shapes keep `ART_DIRECTION.md` rules: hazards angular and darker, path broad and flat.

## 3. Texture painting

- **Hand-painted albedo, baked lighting cues in.** Large gradients: stone goes warm light top `#D6B273` to cooler mid `#82785F` toward the base over the whole object; foliage dark core to bright tip.
- **Painted AO** in crevices and strand seams (multiply, warm-brown `#4D3A22`, not black), **painted edge highlights** on top-facing bevels (`#F7DFB2`, 1–3 texels wide at 512).
- **Noise budget:** high-frequency detail ≤10% contrast. No scanned photo textures. Allowed: soft brush strokes, broad mottling at 1/4–1/8 of object size.
- **Normal maps only for broad forms** (strand bulges, slab bevels). Detail normals off. Many props need no normal map at all (bake form into albedo).
- **Texel density:** 256 px/m near path, 128 px/m mid, far = vertex color or 64 px/m. Typical maps 512–1024; max 2048 for the arch.
- **Roughness:** mostly flat constants per material (stone 0.85, leaf 0.6, wet rock 0.35, water handled by shader). Metallic 0.

## 4. Shading model (custom URP lit, cheaper than full PBR)

- **Wrapped diffuse:** `NdotL` wrap 0.4 (`(NdotL + 0.4) / 1.4`), so terminators are soft.
- **Color-shifted shadows:** shadow tint toward teal-blue `#3E5C66` at 35% blend; lit side tinted by sun `#FFD58A`. Terminator band picks up a warm saturated orange `#E08A3A` (5–8% width) for the "painted" glow.
- **Ambient:** 3-color gradient (sky `#8FC3E0`, equator `#9C9A6A`, ground `#4A4326`), no realtime GI.
- **Rim / backlight:** fresnel rim power 3, intensity 0.35 on environment toward the sun side; characters 0.6 (always readable).
- **Specular:** single soft lobe, max intensity 0.25, except wet rock (0.5) and water. No sharp hotspots on stone or leaves.
- **Shadows:** one realtime sun shadow (characters + near props), soft, shadow color never darker than value 20%.
- **Post:** bloom (threshold 1.0, intensity 0.6, scatter 0.7), ACES-lite tonemap, saturation +15, warm lift. No outlines.

## 5. Foliage

- **Large clean clusters:** 5–12 big leaves per card cluster, readable shapes (palms, broad banana/philodendron, round canopy puffs). No needle/grass noise in the mid ground.
- **Gradient per cluster:** core `#1C2E0D` → mid `#414705`/`#5F600F` → sunlit tips `#9D831C`–`#BDAD47`. Painted in albedo + vertex AO toward the trunk.
- **Translucency:** back-lit leaves add `#C9D040` at 0.5 × (view·−sun) power 4. This is the main source of the golden glow in F4_f.
- **Normals:** spherical/bent normals per cluster (canopy reads as soft balls, not cards).
- **Wind:** vertex shader, 2 layers: trunk sway 0.5 Hz amp 2 cm; leaf flutter 2.5 Hz amp 1 cm, phase from vertex color. Near-path ferns +50%.
- **Hanging vines/moss curtains:** few thick strands, tapered, clumped in 3s.

## 6. Water, foam, falls

- **Depth gradient (strong):** shallow/rim `#4EAE92` → mid `#278677` → deep `#0E5A50`. Saturated, not grey-green. Depth fade over 0.3–2.5 m.
- **Surface:** one scrolling normal pair at low strength (0.3), sky reflection faked by fresnel lerp to `#BEE3E8`. No SSR, no refraction pass (cheap screen-color skip on low tier).
- **Foam = shapes, not noise:** cream `#FCF0CB` foam in clean bands at rock contacts and fall bases, using a threshold on a flow-scrolled painted mask (hard edge, 1-step soft). Highlights `#FBEDCC`.
- **Waterfalls:** scrolling flow meshes with painted vertical streak texture (3–5 streak widths), warm white top `#EFD4B0`, shadowed `#BDAE99`; mist cards at base, additive, warm.
- **Cascades:** stepped lips with bright foam lips and turquoise pools between, as in F4_f.

## 7. Sky and atmosphere

- **Sky:** gradient zenith `#60A4D6` → horizon `#F7DFB2` → sun glow `#EBC257`/`#FCF7C8`. Painted cumulus card, warm undersides. Sun low, top-left, back-lighting the scene.
- **Aerial perspective:** far planes fade to warm haze `#D6B273` (not cool blue as in F4_e); 25% at 80 m, 60% at 250 m. Far pillars are flat-ish painted cards.
- **God rays:** 2–4 additive cards from the sun side, `#F1AF5D` at 0.25 alpha, gentle drift.
- **Particles:** golden pollen motes (low count, ≤40), mist at falls.

## 8. Palette (sampled from F4_f)

| Role | Hex |
|---|---|
| Sky zenith / horizon / sun glow | `#60A4D6` / `#F7DFB2` / `#EBC257` |
| Sunlight, rays | `#FFD58A`, `#F1AF5D` |
| Sandstone lit / mid / shade | `#D6B273` / `#AD914F` / `#655A3F` |
| Stone deep shade | `#283930` |
| Foliage core / mid / lit tip | `#1C2E0D` / `#5F600F` / `#BDAD47` |
| Emerald mid-shade | `#2A3E12` |
| Water shallow / mid / deep | `#4EAE92` / `#278677` / `#0E5A50` |
| Foam / falls | `#FCF0CB` / `#EFD4B0` |
| Path earth lit / shade | `#B99643` / `#5E4B18` |
| Bellcap orange (risky cue only) | `#E5A45A` lit, `#C28036` mid |
| Shadow tint | `#3E5C66` |

Note: F4_f leans olive-gold in the mid foliage. For gameplay chunks, keep the **safe route emerald** (`#2E8A57` lit)
readable against this warmer base; the arch scene can stay warm.

## 9. Pista light restyle (design unchanged)

Same face, ponytail, outfit, backpack, harness, colors and map patch (`ART_DIRECTION.md` s7). Changes are shading only:
- **Skin:** smooth gradient, wrapped diffuse 0.5, warm subsurface tint `#E8A07A` at the terminator; no pore detail, no detail normal. Soft cheek/nose warmth painted in.
- **Hair:** 6–10 painted clumps in the ponytail with a band highlight per clump (anisotropic-like, painted in albedo), not strand cards. Silhouette slightly fuller.
- **Cloth:** flat-ish fabric with painted folds and seam highlights; weave/noise removed; normal map only for big folds and pocket edges. Orange shoulder panel stays the brightest saturated patch on her (`#F08A2C`).
- **Gear:** backpack and boots keep worn-leather color variation as broad painted patches, not scratches.
- Rim 0.6 always on; she must win the value contrast against the arch and water (check in grayscale).
- Re-use the existing Meshy mesh and rig; only textures and material change. No new Meshy spend needed.

## 10. Mobile cost (vs. realistic)

Cheaper: no detail normals, fewer and smaller textures (most props 512, one 2048), constant roughness, fewer
foliage cards (bigger clusters), no SSR or refraction. Custom wrapped-light shader costs about the same as URP Lit
Simple. Target unchanged: 60 fps on the minimum device.

## 11. Checklist: judge each render against F4_f

1. Squint test: the frame reads as 3 planes (warm near, lush mid, hazy golden far) like F4_f.
2. The arch reads as a few chunky, rounded braids, warm sandstone, not grey/white bark (F4_e failure mode).
3. No visible high-frequency noise on stone, path or moss at phone size.
4. Shadows are teal-shifted, never grey/black; terminators soft with a warm band.
5. Backlit foliage glows gold-green at the tips; clusters are big and clean.
6. Water is clearly turquoise with a visible shallow→deep gradient; foam is shaped bands, cream not pure white.
7. Sky has the golden sun glow at top-left and warm haze on far pillars; 2–4 god rays.
8. Pista: same design, smooth skin, clumped hair, clean cloth; she pops in grayscale.
9. Orange used only on bellcaps and Pista's shoulder; coins and hazards follow reserved colors.
10. Eyedropper 5 spots (lit stone, foliage tip, mid water, sky zenith, path) land within ~10% of the hex in s8.
11. At arm's length on the phone it reads as "painted premium game", not photo and not cartoon.
12. Frame time on the test device is within budget.

## Assumptions

- `[ASSUMED]` Shadow tint, wrap values and bloom settings above are starting values for the technical artist; tune on device against F4_f.
- `[ASSUMED]` Warm golden haze replaces the cool `#CFE4EA` haze from `ART_DIRECTION.md` for the trial.
