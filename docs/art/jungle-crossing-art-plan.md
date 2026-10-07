# Art plan: a wild kid crossing a living jungle

Direction: owner decision 2026-10-07 (design/DECISIONS.md, newest row). Style stays "Inkbound Pulp" (design/STYLE_GUIDE.md).
Data and prompts: `tools/assetgen/jungle_crossing_prompts.py` (no API calls). Nothing has been generated yet; no credits spent.
Status of everything below is a proposal. Looks the owner has not seen are `[ASSUMED]` until the owner reviews a slice.

## 1. Visual language of natural swings

**Scale (Pista = 1.6 m, Duko wingspan 1.0 m).**
- Giant tree trunk: radius 1.5 m (3 m across, about 2x Pista's height), buttress roots splay to 3.5 m. Trunks run out of frame; the camera sees 6 to 18 m of them.
- Swing branch: thick and horizontal, 0.5 to 1.0 m thick (a branch thicker than Pista's torso), 6 to 10 m long, gentle 0.4 m upward arc, underside flat so the vine reads as hanging from it.
- Branch height: normal swing anchor 7 to 9 m above the path; high swing 10 to 14 m; low "hop" vine anchors 4.5 to 5.5 m.
- Vine length 5 to 7 m, so the grab point (lowest point, where the tuft sits) is 1.8 to 2.4 m above the path: just over Pista's raised hand.
- Silhouette rule: the tree reads as "thick column + one dominant horizontal bar". Never more than one dominant branch per screen height band, so the eye finds the anchor in under 0.3 s. Canopy mass sits above the branch, never behind the vine.

**Vine types** (same in every world; only tint and material change).
| Type | Thickness | Role | Read |
|---|---|---|---|
| Thick liana | 12 to 16 cm | The only vine you can grab | Smooth, outlined, 2 slow S-curves, leaf tuft + flower at the grab point |
| Thin creeper | 4 to 6 cm | Decor, depth | Lighter green, sways faster, no tuft, no glow |
| Root-vine | 8 to 20 cm, tapered | Cliff/branch curtains, obstacle dressing | Brown, in clusters of 3 to 5, no tuft |

**What reads as "grab me".** Only the live liana has all of: (1) highest contrast on screen at its end: sun-gold `#FFC43D` / orange flower `#F28C28` / light green tuft against deep-teal back foliage; (2) the slowest, most regular sway (pendulum 3 to 5 degrees at 0.5 Hz; creepers flutter faster so motion separates them); (3) the tuft at the grab point, 0.45 m, 5 broad leaves + 1 flower; (4) the existing white-core glow and 2 Hz ring (style guide 7.5, never changes per world); (5) Duko calls "Vine!" about 1.5 s before reach and flies a beat ahead toward it; (6) dim other vines within 12 m by about 25% value. No other asset may carry gold or orange flowers in the upper-middle screen band. Hazard red stays off all trees, trunks, roots and vines.

## 2. Asset list

Material approach: flat colour with 2 to 3 hard bands, no painted outlines (the shader draws ink). One shared atlas per world, 1024 px max; per-world palettes are tints/atlas swaps (see `PALETTES` in the prompts file), not re-models. Triangles: small props <= 1.5k, hero pieces <= 3k. Export Y up, 1 unit = 1 m, axes baked (the lying-trees bug of 2026-10-07). "Blender" = procedural bpy script, deterministic seeds, like `tools/blender/build_jungle_kit.py`. "Meshy" = text-to-3D, meshy-6-lite, 15 credits per preview+refine pair.

| Asset | Role | Size (m) / pivot / fit | Tris | Route |
|---|---|---|---|---|
| Tree_TrunkSeg (A/B/C) | modular giant trunk | 3 x 6 x 3, base centre; top ring matches next seg; sockets Socket_Branch_L/R at y=3 | 900 | Blender |
| Tree_Buttress | trunk base with roots | 7 x 4 x 7, base centre, blends to r=1.5 at y=4 | 1.8k | Meshy hero (Blender fallback) |
| Branch_Thick | swing branch | 8 x 1.8 x 1, runs +X from trunk end, Anchor_0..2 empties every 2.5 m | 700 | Blender |
| Branch_Platform | canopy platform to run on | 4.2 x 1.2 x 10, top surface y=0, runs +Z | 1.1k | Blender |
| Tree_Crown | far/mid crown silhouette (25 to 60 m, fogged) | 18 x 9 x 18, trunk-top centre | 2.5k | Reuse Foliage_CanopyTree |
| Vine_Liana + Vine_Tuft | grab vine, 10-bone chain | 0.5 x 6, anchor at top y=0, hangs -Y; tuft 0.45 m | 420 + 160 | Blender |
| Vine_Creeper | thin decor | 0.3 x 3.5, top anchor | 120 | Blender |
| Vine_RootCurtain | root-vine cluster | 2 x 5 x 0.8, top centre | 500 | Blender |
| Root_Arch | path-spanning arch / duck dressing | 5 x 3.2 x 1.6, opening >= 2.4 x 2.0 m | 1.8k | Meshy |
| Trunk_Fallen | jump/slide obstacle, bridge dressing | 1 x 1.1 x 6, long axis Z, base y=0 | 1.4k | Meshy |
| Rock_Ledge | step for rising path, verge | 4 x 1.5 x 3 (0.75/1.5/3 m variants), front-bottom pivot | 500 | Blender |
| Bridge_PlankSet | rope bridge | 3.2 x 0.4 x 6, top y=0, +Z; 12 planks, rails at 1.0 m; sway in shader | 900 | Blender |
| Strip_Stream | stream/mud crossing | 10.8 x 0.1 x 4, UV-scroll water, foam line | 200 | Blender |
| Strip_PathEdge (3) | roots and stones along path edge | 1.6 x 0.5 x 6, tiles every 6 m, mirrored | 600 | Blender |
| Leaf_Cluster (3) / Leaf_Billboard | foliage bunches / far cards | 2 x 1.4 x 2; 6 x 3 quad | 250 / 2 | Blender |
| Cliff_Wall / Cliff_Overhang | rising path, River/Mountains verge, vine anchors under lip | 6 x 8 x 3; 5 x 3 x 4 | 2.2k / 1.8k | Blender / Meshy |
| Ruin_Pillar / Ruin_Arch / Ruin_Wall | Ruins world | 1.2x5x1.2; 6x6.5x1.5; 6x3x1 | 1.2k / 2.4k / 600 | Meshy / Meshy / Blender |

Assembly: trunks are built by code (TrunkSeg stack + Branch_Thick at a socket + Leaf_Clusters scattered on a seeded layout), so one tree is about 3 to 6k triangles but only 3 to 4 draw calls (share the atlas; static-batch). Keep visible trees per screen <= 3 near + crowns/billboards for depth.

**Per-world variants** (re-tint plus one swap piece each, no new tree rig):
- River: teal leaves `#2FA38A`, wetter darker bark, Strip_Stream widened, Bridge_PlankSet as main set, creepers as river weed.
- Mountains: conifer-like narrower trunks (scale X 0.7), rope instead of liana (liana tint `#B89A6A`), Cliff_* dominant, cool shadow `#6B6A9A`, never lavender sky.
- Ruins: Ruin_Arch / Ruin_Pillar replace trunks as anchors, chains/roots from keystone, terracotta `#C4673A`, dull gold `#B8892E` only.
- Dusk Jungle: palette swap only (`dusk` in prompts file): navy shadows, warm rim, fireflies. Grab tuft stays `#FFC43D` so it still pops.

## 3. Credit and time budget, production order (Jungle first slice)

Owner rules: real balance **465** credits (verify via `/v1/balance`); **hard ceiling 300** for the whole slice; **reserve >= 150**. Procedural Blender first, one trunk+branch module reused with variants and recolours, reuse the 17 existing props (Foliage_CanopyTree becomes the far crown, BigLeaf/FernClump/RockCluster dress the floor). Meshy only for hero pieces procedural cannot do: one meshy-6-lite preview+refine pair (15) each, **no re-rolls without owner approval, contact sheet (previews only) before any variant spend**.

| Asset | Method | Credits |
|---|---|---|
| Tree_TrunkSeg, Branch_Thick, Branch_Platform, Vine_Liana/Tuft/Creeper/RootCurtain, Leaf_*, Strip_*, Rock_Ledge, Bridge_PlankSet | Blender procedural | 0 |
| Tree_Crown (far depth) | Reuse existing Foliage_CanopyTree, scaled/recoloured | 0 |
| Tree_Buttress | Meshy pair (Blender root-flare fallback if rejected) | 15 |
| Root_Arch | Meshy pair | 15 |
| Trunk_Fallen | Meshy pair | 15 |
| **Jungle slice total** | | **45** |
| Cliff_Overhang, Ruin_Pillar, Ruin_Arch (later worlds) | Meshy pairs | 45 |
| Cliff_Wall, Ruin_Wall | Blender procedural | 0 |
| **Planned total** | | **90** |
| Balance after planned spend | 465 - 90 | 375 |
| Headroom under the 300 ceiling | spendable only with owner approval (re-rolls, variants) | 210 |
| Reserve floor | never go below | 150 |

| Step | Work | Credits |
|---|---|---|
| 1 | `build_crossing_kit.py`: TrunkSeg, Branch_Thick, Vine_Liana+Tuft, Strip_PathEdge, Leaf_Cluster; Blender mock render (run_mock.py style) | 0 |
| 2 | Assemble one tree + swing vine from the module with existing props; art-director review (section 5). This is the believable slice, at zero credits | 0 |
| 3 | Meshy Tree_Buttress preview only (contact sheet to owner), then refine if approved | 15 |
| 4 | Root_Arch, Trunk_Fallen (previews first) | 30 |
| 5 | Blender Rock_Ledge, Bridge_PlankSet, Strip_Stream, Creeper, RootCurtain, Billboard | 0 |
| 6 | Unity integration by asset-pipeline, Duko call-out + glow VFX tuning | 0 |

Meshy commercial licence is still UNVERIFIED (docs/STATUS.md); verify before shipping. Record tool/plan/licence per asset in `docs/LICENSES.md`.

## 4. Light and mood
- Canopy light shafts: 3 to 5 soft god-ray cards per 60 m, additive, sun gold `#FFC43D` at 12 to 18% alpha, angled to match the key light (jungle: yaw -25, pitch 25, from ahead), 1.5 to 3 m wide, 10 to 15 m long, slow 0.1 Hz drift. Never over the grab point or a hazard (they must not wash out the tuft or the red-and-ink stripes). Cap overdraw: max 4 on screen, fade by distance.
- Forest floor: darker and cooler than the path: shadow tint `#2E5B57`, path stays cream `#F3DFB2` as the brightest big shape. Dappled blob shadows (baked, no shadow maps) from canopy leaves across path edges at about 20% opacity, never across the lane centre.
- Layers by depth: forest floor (dark teal, 0 to 3 m), trunks + vines (mid values, the swing layer), canopy ceiling (warm, bright holes), far crowns (fogged `#E9C98A`). Upper third stays warm/light for Duko.
- Fog 45 to 90 m as in the style guide; distant crowns lose ink line weight with distance.

## 5. QA checklist (art-director review)
1. Squint test: at phone size, the live vine's tuft is the brightest saturated spot in the mid-upper band; the path is the largest cream shape.
2. Anchor legibility: vine visibly attaches to a branch; the branch reads as horizontal bar; no trunk/leaf mass hides the anchor.
3. Only graspable vines have tuft + glow; creepers and root-vines are never confused with them.
4. Scale: trunk r 1.5 m, branch 0.5 to 1.0 m thick, grab point 1.8 to 2.4 m above path; Pista (1.6 m) beside it looks tiny, not equal.
5. No hazard red on scenery; hazards still carry red + ink stripes and are not hidden by foliage or light shafts.
6. Coins and power-ups still pop against the new denser background (check value contrast, not just hue).
7. Pivots and orientation correct: no lying trees, models face the right way, Y up, 1 unit = 1 m.
8. Triangles <= 1.5k small / 3k hero; atlas <= 1024; draw-call count for one screen within docs/ARCHITECTURE.md budgets.
9. Style: flat colour with 2 to 3 hard bands, no painted outlines (shader only), palette from the style guide, texture seams invisible at run speed.
10. Originality: no recognisable Tarzan/Mowgli/Jungle Book/film imagery or brand marks; no pose or prop that copies a known character.
11. Per-world: swap is by tint plus the named variant piece; macaw sky/upper-third rule holds; no lavender sky.
12. Licence logged in `docs/LICENSES.md`; Meshy licence status noted.

## Open questions for owner
1. **Tree construction.** (a) Modular Blender trunks + Meshy only for hero base/crown (recommended: consistent, cheap, controllable); (b) all trees via Meshy (richer bark, over the credit ceiling, not recommended); (c) Blender only, no Meshy trees.
2. **Grab cue strength.** (a) Tuft + flower + glow ring + Duko call-out + dim others (recommended); (b) glow ring only (cleaner, harder to see in dense jungle); (c) add a brief gold sparkle trail on approach.
3. **Spend now?** Approve 45 credits (Buttress, Root_Arch, Trunk_Fallen; previews first as a contact sheet) after the zero-credit Blender slice? (a) Yes, after you see the mock (recommended); (b) Blender only, no Meshy; (c) also allow the 45 for Cliff/Ruins now.
4. **Canopy openness.** (a) Open canopy with light holes and a warm sky strip (recommended, keeps Duko readable); (b) closed canopy, darker, more "deep jungle" but harder on the macaw rule.

## Update 2026-10-07 (spec 004, fixed-pivot swing)

The swing is a pendulum about one fixed branch tip, so the overhead span and the sliding knot are dropped: no span art, no `Vine_Knot` slot.
Each vine needs one tapered limb growing out of its anchor tree (0.8 m thick at the trunk, 0.35 m at the tip, tip exactly at the pivot 17.0 m above the lane),
a leafy knot at the pivot, and the 14 m `Vine_Liana` rope. The chasm is 16 m long (rim 4 m before the pivot, far edge 12 m past it).
