"""Jungle-crossing asset library (data only, no API calls, no imports of network code).

Direction: owner's 'wild kid crossing a living jungle' (design/DECISIONS.md 2026-10-07). Plan: docs/art/jungle-crossing-art-plan.md.
Style: Inkbound Pulp. Flat colour, 2-3 hard bands, ink (#1E1A24) outlines come from the shader, so the model itself carries
NO painted outlines. Hazard red #D7263D never appears on scenery. Vine grab glow (white core + #FFC43D) is a runtime VFX, not baked.
Scale reference: Pista 1.6 m, Duko wingspan 1.0 m. Unity: Y up, 1 unit = 1 m, FBX exported with baked axes.

ASSETS: every asset, with build route ('blender' = procedural bpy script, 'meshy' = text/image-to-3D, 'both' = Blender base + Meshy detail).
MESHY_PROMPTS: ready prompts for the Meshy route; same shape as tools/assetgen/env_prompts.ENV_PROMPTS (prompt + texture) plus polycount/ai_model.
PALETTES: per-world colour overrides applied by shader tint / atlas swap, never by re-modelling.
Use: from jungle_crossing_prompts import ASSETS, MESHY_PROMPTS, PALETTES, CREDIT_PLAN
"""

MESHY_MODEL = "meshy-6-lite"          # ~15 credits per preview+refine pair
CREDITS_PER_PAIR = 15
CREDITS_AVAILABLE = 465               # real balance per owner (2026-10-07); re-check /v1/balance before spending
SLICE_CEILING = 300                   # HARD ceiling for the whole jungle-crossing slice (owner rule)
MIN_RESERVE = 150                     # never go below this balance
# Rules: one preview+refine pair per asset, NO re-rolls without owner approval, contact sheet (previews) before any variant spend.
# Reuse first: the 17 existing props (Foliage_CanopyTree, TreeA/B, BigLeaf, FernClump, RockCluster, Prop_Signpost...) and one trunk module with recolours.
TRI_SMALL, TRI_HERO = 1500, 3000

STYLE = ("Inkbound Pulp style: stylized low-poly game prop, bold flat colors, two or three hard shading bands, "
         "hand-painted, no painted outlines, no text, no logo, single object, neutral background.")

# --- Palettes (sRGB hex). Jungle values come from design/STYLE_GUIDE.md 2.2 and 5. -------------------------------------
PALETTES = {
    "jungle": dict(bark="#6B4A32", bark_shadow="#3E2A22", leaf="#3A8C3F", leaf_light="#8DC63F", leaf_shadow="#1B4D4A",
                   liana="#3A8C3F", creeper="#8DC63F", root_vine="#7A5638", grab_tuft="#FFC43D", flower="#F28C28",
                   stone="#8A7B6A", moss="#5FA845", mud="#6A4A33", water="#1FA2C7", path="#F3DFB2"),
    "dusk": dict(bark="#5A3E3A", bark_shadow="#2B2238", leaf="#2F6B5A", leaf_light="#B0607A", leaf_shadow="#2B3A67",
                 liana="#2F6B5A", creeper="#7FB07A", root_vine="#5A3E3A", grab_tuft="#FFC43D", flower="#FF9A5A",
                 stone="#7A6A78", moss="#4F8F6A", mud="#4A3636", water="#2B3A67", path="#E7C9A0"),
    "river": dict(bark="#7A5E45", bark_shadow="#3E3A32", leaf="#2FA38A", leaf_light="#A8E0C8", leaf_shadow="#1F5A6B",
                  liana="#2FA38A", creeper="#A8E0C8", root_vine="#7A5E45", grab_tuft="#FFC43D", flower="#FFE6B0",
                  stone="#9AA39A", moss="#6FBF8A", mud="#8A7048", water="#1FA2C7", path="#E9D3A6"),
    "mountains": dict(bark="#6E6A78", bark_shadow="#3E3C52", leaf="#5F8F6A", leaf_light="#C8E0C0", leaf_shadow="#6B6A9A",
                      liana="#B89A6A", creeper="#C8E0C0", root_vine="#6E6A78", grab_tuft="#FFC43D", flower="#FFF1DC",
                      stone="#8E8DAA", moss="#8FB89A", mud="#7A7488", water="#8FD3E6", path="#F4F1F8"),  # no lavender sky (macaw rule)
    "ruins": dict(bark="#6B4A32", bark_shadow="#3A2A2E", leaf="#3F7A3A", leaf_light="#B8C64A", leaf_shadow="#5A2E3A",
                  liana="#3F7A3A", creeper="#B8C64A", root_vine="#5A3E2A", grab_tuft="#FFC43D", flower="#F28C28",
                  stone="#C4673A", moss="#6FA040", mud="#6A4A33", water="#2B7A6B", path="#EBCB9E"),
}

# --- Vine types (shared by all worlds; world only changes material/tint, never glow or ring) ---------------------------
VINES = {
    "liana":   dict(role="main swing vine", thickness_m=(0.12, 0.16), length_m=(5.0, 7.0), segments=10, tris=420,
                    read="thick, smooth, outlined, 2 slow S-curves, bright leaf tuft at the grab point"),
    "creeper": dict(role="decor + short hop vine, never a swing grab", thickness_m=(0.04, 0.06), length_m=(2.0, 5.0), segments=6, tris=120,
                    read="thin, lighter green, sways faster, NO tuft, NO glow (so it is never mistaken for a grab)"),
    "root":    dict(role="hanging root curtain / obstacle dressing", thickness_m=(0.08, 0.2), length_m=(3.0, 9.0), segments=8, tris=300,
                    read="brown not green, tapered, clusters of 3-5, never carries a tuft"),
}
GRAB_RULES = dict(
    grab_height_above_path_m=(1.8, 2.4),   # lowest point of the vine = where Pista's hands go; tuft sits here
    anchor_height_m=(7.0, 9.0),            # branch underside for normal swings; 10-14 for 'high' swings
    swing_arc_forward_m=(5.0, 8.0),
    tuft=dict(shape="cluster of 5 broad leaves + 1 flower, 0.45 m across", colors=("#FFC43D", "#F28C28", "#8DC63F"),
              sway="vine pendulum 3-5 deg, 0.5 Hz; tuft leaves flutter extra +-8 deg at 1.5 Hz"),
    runtime_cues=["white-core ring glow pulsing 2 Hz (style guide 7.5)", "Duko call-out 'Vine!' ~1.5 s before reach",
                  "dim every other vine within 12 m by 25% value so the live one wins"],
)

# --- Asset list ----------------------------------------------------------------------------------------------------------
# fit = pivot/orientation rule; m = size in metres (W x H x D, X lateral, Y up, Z forward); tris = budget.
ASSETS = {
 # Giant trees (modular: code stacks trunk segments, attaches a branch at a socket, scatters leaf clusters)
 "Tree_TrunkSeg":   dict(role="giant trunk module", route="blender", m=(3.0, 6.0, 3.0), tris=900, pivot="base centre, y=0; top ring at y=6, matches next seg",
                         fit="radius 1.5 m (+-20% by scale), sockets named Socket_Branch_L/R at y=3, bark bands vertical, 3 variants (A straight, B buttress base, C top taper)"),
 "Tree_Buttress":   dict(role="trunk base with buttress roots", route="meshy (hero, 15 cr; Blender flare-root fallback if rejected, no re-roll)", m=(7.0, 4.0, 7.0), tris=1800, pivot="base centre",
                         fit="blends into Tree_TrunkSeg radius 1.5 at y=4; roots splay to 3.5 m; hero piece, one per tree start"),
 "Branch_Thick":    dict(role="swing branch (the main anchor)", route="blender", m=(8.0, 1.8, 1.0), tris=700, pivot="trunk end, local x=0, branch runs +X",
                         fit="1.0 m dia at trunk tapering to 0.5 m, slight upward arc 0.4 m, 2 side stubs, underside flat enough for vine anchor empties Anchor_0..2 every 2.5 m"),
 "Branch_Platform": dict(role="canopy platform (wide branch the path can run on in sky sections)", route="blender", m=(4.2, 1.2, 10.0), tris=1100, pivot="start centre top surface y=0, runs +Z",
                         fit="top 3.0 m walkable width w/ flat bark strip, roots/leaf tufts on edges only, matches path half-width 4.2 for edge dressing"),
 "Tree_Crown":      dict(role="far/mid crown silhouette", route="blender (reuse existing Foliage_CanopyTree, scaled/recoloured; Meshy only if owner rejects)", m=(18.0, 9.0, 18.0), tris=2500, pivot="trunk-top centre, y=0",
                         fit="umbrella crown wider than tall; used at 25-60 m distance only, fogged"),
 "Vine_Liana":      dict(role="main grab vine", route="blender", m=(0.5, 6.0, 0.5), tris=420, pivot="top (anchor) at y=0, hangs -Y; chain of 10 bones for sway",
                         fit="see VINES['liana'], tuft mesh child Vine_Tuft (0.45 m) at the bottom"),
 "Vine_Tuft":       dict(role="grab-point leaf cluster + flower", route="blender", m=(0.45, 0.4, 0.45), tris=160, pivot="top centre (attaches to vine end)", fit="bright, tinted per world only via flower colour"),
 "Vine_Creeper":    dict(role="thin decor vine", route="blender", m=(0.3, 3.5, 0.3), tris=120, pivot="top anchor", fit="see VINES['creeper']"),
 "Vine_RootCurtain":dict(role="root-vine cluster hanging from cliffs/branches", route="blender", m=(2.0, 5.0, 0.8), tris=500, pivot="top centre", fit="3-5 tapered roots, brown"),
 "Root_Arch":       dict(role="path-crossing root arch (duck/frame, dressing)", route="meshy", m=(5.0, 3.2, 1.6), tris=1800, pivot="ground centre, y=0, opening along Z",
                         fit="clear opening >= 2.4 m wide x 2.0 m high when it is a duck obstacle (unit-cube hitbox owned by sim); dressing version spans path at 5 m height"),
 "Trunk_Fallen":    dict(role="fallen trunk (jump/slide obstacle + bridge dressing)", route="meshy", m=(1.0, 1.1, 6.0), tris=1400, pivot="centre, base y=0, long axis Z",
                         fit="NO red; hazard language comes from the sim's marker, shape must read 'jump me': round, mossy top, cut end visible; also a 2.4 m wide variant by scaling X"),
 "Rock_Ledge":      dict(role="rock ledge / step for rising path and cliff verge", route="blender", m=(4.0, 1.5, 3.0), tris=500, pivot="front-bottom centre, top flat at y=1.5",
                         fit="3 stackable heights 0.75/1.5/3.0 m, chunky faceted, moss cap"),
 "Bridge_PlankSet": dict(role="rope bridge segment", route="blender", m=(3.2, 0.4, 6.0), tris=900, pivot="start centre y=0 top surface, +Z",
                         fit="12 planks 0.5 m, 2 rope rails at 1.0 m, post pair at ends; planks as separate verts for sway shader; gap-plank variant (2 missing) = hazard dressing only"),
 "Strip_Stream":    dict(role="stream / mud strip crossing the path", route="blender", m=(10.8, 0.1, 4.0), tris=200, pivot="centre, y=0", fit="flat strip w/ UV scroll; water #1FA2C7 bands + white foam line, mud banks both sides; walkable decor, not a hazard"),
 "Strip_PathEdge":  dict(role="path edge with roots and stones", route="blender", m=(1.6, 0.5, 6.0), tris=600, pivot="path-side bottom edge, x=0, extends +X, +-Z 3 m",
                         fit="3 variants (roots, stones, mixed) tile every 6 m, hides trail/jungle seam, left/right by mirror"),
 "Leaf_Cluster":    dict(role="foliage bunch for branches/crowns", route="blender", m=(2.0, 1.4, 2.0), tris=250, pivot="base centre", fit="5-7 crossed leaf cards (alpha-cut, 256 px), 3 variants, scattered by code"),
 "Leaf_Billboard":  dict(role="far canopy cards", route="blender", m=(6.0, 3.0, 0.0), tris=2, pivot="bottom centre", fit="quad, alpha-cut, dark-teal silhouette for depth layers"),
 "Cliff_Wall":      dict(role="cliff face verge (rising/falling path, River/Mountains)", route="both", m=(6.0, 8.0, 3.0), tris=2200, pivot="path-side bottom x=0, +X away", fit="12 m tile via 2 pieces, layered strata bands"),
 "Cliff_Overhang":  dict(role="cliff lip with root curtain anchor", route="meshy", m=(5.0, 3.0, 4.0), tris=1800, pivot="lip front-bottom", fit="anchor empties under the lip for vines"),
 "Ruin_Pillar":     dict(role="broken stone pillar", route="meshy", m=(1.2, 5.0, 1.2), tris=1200, pivot="base centre", fit="vine-wrapped, terracotta, dull gold trim #B8892E only"),
 "Ruin_Arch":       dict(role="ruined arch / vine anchor", route="meshy", m=(6.0, 6.5, 1.5), tris=2400, pivot="ground centre", fit="vines hang from the keystone"),
 "Ruin_Wall":       dict(role="ruin wall run", route="blender", m=(6.0, 3.0, 1.0), tris=600, pivot="base centre", fit="modular, 2 damage variants"),
}

# --- Meshy prompts (only for route meshy/both). Prompt text is the 'prompt' arg; texture is the refine texture prompt. ------
MESHY_PROMPTS = {
 "Tree_Crown": dict(polycount=2500, ai_model=MESHY_MODEL,
   prompt="A huge stylized rainforest canopy crown seen as one object: wide flat umbrella of big layered leaf clumps with two thick branches stubs below, wider than tall, nothing else, no trunk base, no ground.",
   texture="deep jungle green #2F7A3A with lighter yellow-green tops #8DC63F and deep teal #1B4D4A shadow bands, no red"),
 "Tree_Buttress": dict(polycount=1800, ai_model=MESHY_MODEL,
   prompt="The base of a giant ancient jungle tree: thick round trunk stub about as tall as wide with big flared buttress roots spreading on the ground, flat top cut, chunky and simple, no leaves, no canopy.",
   texture="warm brown bark #6B4A32 with vertical dark ink-brown bands #3E2A22, a few green moss patches, no red"),
 "Root_Arch": dict(polycount=1800, ai_model=MESHY_MODEL,
   prompt="A natural arch made of thick intertwined jungle tree roots, tall rounded opening in the middle, symmetrical, chunky, standing on the ground, nothing else, no tree on top, no leaves.",
   texture="brown roots #7A5638 with dark ink-brown shading bands, small green moss patches, no red"),
 "Trunk_Fallen": dict(polycount=1400, ai_model=MESHY_MODEL,
   prompt="A fallen thick jungle tree trunk lying on the ground, long round log with a cut broken end, moss on top, a few short branch stubs, lying along its length, nothing else, no ground base.",
   texture="warm brown bark with ink-dark bands, bright green moss on top, pale cream cut end rings, no red"),
 "Cliff_Overhang": dict(polycount=1800, ai_model=MESHY_MODEL,
   prompt="A jungle cliff lip overhang: a chunky layered rock ledge jutting out with roots and moss hanging off its underside, flat top, one rocky face, nothing else.",
   texture="orange-brown rock #A06A3A with hard flat bands, green moss tops, brown roots, ink-dark cracks, no red"),
 "Ruin_Pillar": dict(polycount=1200, ai_model=MESHY_MODEL,
   prompt="A broken ancient stone pillar wrapped with jungle vines, square chunky column with a cracked top, carved band near the top, standing on the ground, nothing else.",
   texture="terracotta stone #C4673A with hard shading bands, green vines, dull gold #B8892E carved band, ink-dark cracks, no red warning stripes"),
 "Ruin_Arch": dict(polycount=2400, ai_model=MESHY_MODEL,
   prompt="A ruined ancient stone temple archway covered in moss and hanging vines, chunky blocks, one side partly collapsed, symmetrical opening, standing on the ground, nothing else.",
   texture="terracotta and grey stone with hard shading bands, green moss, dull gold #B8892E trim, no red warning stripes"),
 "Cliff_Wall": dict(polycount=2200, ai_model=MESHY_MODEL,
   prompt="A tall jungle cliff wall section: flat back, layered chunky rock strata with ferns and roots on ledges, rectangular footprint, nothing else.",
   texture="orange-brown and grey-brown strata with hard bands, green moss, ink-dark cracks, no red"),
}

# --- Blender procedural specs (read by a future tools/blender/build_crossing_kit.py; mirror build_jungle_kit.py conventions) --
BLENDER_SPECS = {
 "Tree_TrunkSeg":  dict(method="cylinder 10 sides, 3 rings, radius noise +-8% seeded, vertical bark-band UV, 2 branch sockets (empties)"),
 "Branch_Thick":   dict(method="curve with taper -> mesh 7 sides, 6 rings, arc 0.4 m, 2 stubs, anchor empties"),
 "Branch_Platform":dict(method="flat-top tapered box w/ bevel, bark strip UV, edge bumps"),
 "Vine_Liana":     dict(method="10-bone chain, 6-sided tube, 2 S-curves, tuft child; tris ~420"),
 "Vine_Creeper":   dict(method="6-bone chain, 4-sided tube"),
 "Vine_RootCurtain": dict(method="3-5 tapered 5-sided roots with random length 3-9 m, seeded"),
 "Rock_Ledge":     dict(method="beveled box, vertex jitter 0.08 m, moss cap duplicate-top, decimate to budget"),
 "Bridge_PlankSet":dict(method="12 plank boxes, 2 rope cylinders with sag curve, 2 post pairs"),
 "Strip_Stream":   dict(method="subdivided plane, bank verts displaced, two UV sets (water scroll, bank)"),
 "Strip_PathEdge": dict(method="scattered roots (tapered tubes) + stones (icospheres 0.2-0.5 m, jittered), 3 seeds"),
 "Leaf_Cluster":   dict(method="5-7 crossed quads with leaf alpha card, tilt +-25 deg, seeded"),
 "Leaf_Billboard": dict(method="single quad, alpha atlas cell"),
 "Ruin_Wall":      dict(method="stacked jittered boxes, 2 damage seeds"),
}

# --- Credit plan (hard ceiling 300 for the slice; reserve >= 150; balance 465) ---------------------------------------------
# (asset, method, credits). One pair = 15. No re-rolls without owner approval.
CREDIT_PLAN = {
  "slice_jungle": [
     ("Tree_Buttress", "meshy-6-lite pair", 15),
     ("Root_Arch", "meshy-6-lite pair", 15),
     ("Trunk_Fallen", "meshy-6-lite pair", 15),
     ("Tree_TrunkSeg/Branch_Thick/Branch_Platform/Vine_*/Leaf_*/Strip_*/Rock_Ledge/Bridge_PlankSet/Tree_Crown(reuse)", "Blender procedural / existing props", 0),
  ],
  "slice_jungle_total": 45,
  "later_worlds": [("Cliff_Overhang", "meshy", 15), ("Ruin_Pillar", "meshy", 15), ("Ruin_Arch", "meshy", 15),
                   ("Cliff_Wall", "Blender (strata boxes) first; Meshy only if rejected", 0)],
  "later_total": 45,
  "planned_total": 90,
  "ceiling": 300,
  "balance_after_planned": 375,     # 465 - 90
  "reserve_floor": 150,
  "unallocated_headroom": 210,      # ceiling 300 - planned 90; spendable only with owner approval (re-rolls, extra variants)
}
