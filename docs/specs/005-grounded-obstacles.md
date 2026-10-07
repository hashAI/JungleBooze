# Spec 005: Grounded obstacles, hazards and pickups

**Owner:** game-designer | **Builders:** ui-engineer (views, rigs), gameplay-engineer (keep-out query, tuning asset, tools),
asset-pipeline (models), art-director (review), performance-engineer (budgets), qa-engineer (audits) |
**Milestone:** after spec 003 T3/T6 (routes and swing rig exist), before the per-world art passes (spec 003 T9/T10) |
**Status:** Ready to build (all `[ASSUMED]` numbers stand until the owner says otherwise) |
**Last updated:** 2026-10-07

**Binding owner direction (2026-10-07):** nothing may look like it appears "out of the blue" or floats. Every obstacle and
hazard is a real, physical-looking thing that belongs to the jungle and respects physics visually: it rests on the ground, is
supported or attached, has a believable origin, and moves in a believable way. The simulation stays deterministic and
path-based (no physics engine). This spec is **visual grounding that matches the unchanged hitboxes**, except where section 12
proposes (and quantifies) a sim change, none of which is applied by default.

**Source of truth:** spec 002 (kit, hitboxes, chunks, telegraph, sections 3.2, 5, 9), spec 003 (sections 9, 10, 11: fairness on
curves, terrain skins, scenery and the sight corridor), GDD 8 (obstacles), `design/STYLE_GUIDE.md` 4.1 and 7 (hazard language,
readability), `docs/art/jungle-crossing-art-plan.md`. Current code read for this spec: `ObstacleView`, `HazardView`, `GapView`,
`PowerUpView`, `CoinView`, `ObstacleKitConfig`, `HazardConfig`.

**What this spec does not change:** hitboxes, chunk layouts, tiers, speeds, strike timings (1.3 s warning, 0.6 s active, 3.4 s
cycle), mover speed (4.8 m/s), gap lengths, power-up rules, coin rules. AC-302 (golden traces byte-identical) must still hold.

---

## 1. Player story and purpose

"The jungle is a place, not a level. The log across my path is a real tree that fell there, with its roots torn out of the
bank. The branch I slide under hangs from a limb I can see. The boulder sat in a wallow with a stone wedged under it until it
let go. The ravine has a rim, hanging roots and mist. When I die, the thing that killed me was solid where it killed me."

Why this makes the game better:
- **Pillar 1 (readable, see why you died):** a real shape that fills its hitbox means no "invisible wall" deaths. Today the
  rolling boulder is a sphere inside a box: at the lateral edge the sphere surface is 0.8 m behind the lethal front face (3.2).
- **Pillar 3 (clip-worthy):** a 5-second clip with floating gray boxes never looks like a world. Grounded props, shadows, dust
  and believable motion are what make the clip look like a film.
- **Pillar 4/5 (respect):** coins and pickups that sit on natural lines read as rewards on a route, not as clutter.
- **Fairness is unchanged:** the telegraph (spec 002 section 9) is kept and strengthened by context (a limb above a hanging
  mat says "duck" before the stripes can be read).

---

## 2. Grounding rules (binding for every model, rig and effect)

| # | Rule | Check |
|---|---|---|
| **G1 Contact** | The lowest point of every grounded model is **0.03 to 0.12 m below** the ribbon surface (embed), so no gap shows on slopes, curves or crests. Nothing hovers: a ground-level object whose lowest vertex is above the ribbon surface by more than 0.02 m fails. | AC-501 |
| **G2 Contact decal** | Every ground object has a soft dark contact decal (baked-look blob, no shadow maps) 15 to 30 percent larger than its footprint, plus 4 to 10 debris pieces (pebbles, bark flakes, leaf litter) in 0.15 m or lower. Colors from the style palette, never hazard red. | AC-502 |
| **G3 Support** | Every elevated part (hanging mat, strike log, bridge-like root) shows what holds it: a limb, liana, root or ledge that visibly reaches it. A support is never implied. | AC-503 |
| **G4 Origin** | Every obstacle has an **origin story visible at its reading distance**: a stump or trunk it fell from, a ledge it broke off, a bank it sits on, roots it grows from. Origin pieces are "context pieces" (CP, section 3.3). | art review checklist 15.2 |
| **G5 Motion** | Anything that moves obeys the motion rules of its archetype (section 6, 8, 9 and 10): rolling is no-slip, falling accelerates under g = 9.81 m/s^2, recoil is fast and springy, nothing slides without a reason. Visual position **equals** the simulated position (box); it may lead it by 2 ticks at most and never trail it. | AC-510, AC-516 |
| **G6 Fill** | The model fills its hitbox. The **ghost distance** (3.2) of every lethal front-face point a hero box can touch is at most 0.15 m for static archetypes and 0.25 m for the mover. | AC-504 |
| **G7 Scale** | Natural objects keep real proportions: a log lies along its length, an arch has roots at both ends, a limb is thicker where it meets the trunk. Scale ranges are in each archetype table. | art review |
| **G8 Hazard red** | Hazard red `#D7263D` appears only on hazard objects and only as **red ochre trail marks**: painted stripes, handprints and tied cloth strips, always next to ink (style guide 4.1). Never on scenery, roots, trunks or vines. | AC-505 (extends AC-323) |
| **G9 No pop-in** | Objects and their context appear in the fog or behind the canopy edge, never inside the telegraph distance (section 11). | AC-512, AC-513 |
| **G10 Stateless variants** | Which model variant, mirror and context side a given obstacle gets is a pure function of `(run seed, Cosmetic stream, obstacle id)` (hash, like scenery 11.2). Never from the simulation random streams, never from frame time. Same seed gives the same jungle. | AC-506 |
| **G11 Shape first** | Silhouette says the answer (low/wide = jump, hanging with clearance = slide, tall = go around), color is secondary. | art review |
| **G12 One look per kind** | An archetype looks the same everywhere in a world so the player learns it once; variants differ in detail, never in silhouette or clearance. | art review |

---

## 3. Shared definitions

### 3.1 Placement frame

All pieces are placed through `PathPlacement` (spec 003 3.2). An obstacle's **rig** has a root at the front-face center on the
ground (`s = o.Z`, lane center or box center) and two child groups:
- **Body:** the fit-box model, scaled to the hitbox (as the art slots do today; models are authored to a named fit box, not to a unit cube).
- **Context:** unscaled context pieces and decals, placed relative to the root by the skin recipe.

Today's `ObstacleView` scales the whole art to the box. That stretches roots and trunks. The rig split (task G2) fixes it.

### 3.2 Ghost distance (the fill metric)

For one obstacle box and one hero box (standing 0.7 x 0.5 x 1.8 m, sliding 0.8 m high, airborne at any `y` the jump reaches):
sample every lethal contact point on the box's front face (and top face for the low barrier) that a hero box can touch under the
sim's contact rules (spec 002 5.2, edge forgiveness 0.6 of spec 001 9.5). **Ghost distance** = distance along the path
direction from that contact point to the nearest visible model surface (ray cast along `+T` from the point). A lethal point
whose ray hits nothing within 0.5 m counts as 0.5 m (a hard fail above the limit).

Targets: static archetypes <= 0.15 m; mover <= 0.25 m; strike (while Active) <= 0.15 m.
Tool: `FitBoxAudit` (editor menu and EditMode test), written once for all archetypes (task G5).

### 3.3 Context pieces (CP) and where they may stand

Spec 003 11.1 keeps a **sight corridor** (0 to 5.0 m, 7.0 m on the inner side of a bend, heights 0 to 4 m) free of scenery.
Obstacles need supports and origins. Rule [ASSUMED]:

| Zone | Allowed CP | Limits |
|---|---|---|
| Lane boxes (|x| <= 3.42) | The obstacle's own body, its contact decal and debris, hanging mat strands, strike rig parts | Only what the hitbox contains, plus decals <= 0.12 m high |
| Shoulder (3.6 to 4.2) | Roots, stubs and low moss only, <= 0.15 m high | Spec 003 11.1 unchanged |
| **CP zone** (4.2 to 7.0) | Trunk bases, stumps, root plates, rock ledges, limb roots, crown clusters | Height <= 2.5 m inside 4.2 to 5.0; free above 2.5 m only if the viewport probe (AC-522) passes. On the **inner** side of a bend only <= 1.2 m. CP side defaults to the **outer** side of the bend, or a hash pick when |kappa| < 1/400 |
| Overhead (limb, liana lines) | One limb per support at 4.6 to 6.0 m, thin liana lines | The limb is the obstacle's support, spans from the CP trunk; counts as part of the obstacle in the viewport probe |

All CP are part of the obstacle's rig: they spawn and despawn with it (section 11) and the scenery placer treats their footprints
as keep-outs (task G1: a read-only `TrackSimulation` query `GetKeepOuts(sMin, sMax, buffer)`; scenery cells stay stateless
because the track is committed 190 m ahead, more than the 95 m view window).

### 3.4 Embed table (G1)

| Surface (spec 003 5.2) | Embed | Notes |
|---|---|---|
| Trail, Ford, Mud | 0.06 to 0.12 m | Mud takes the deep end |
| Bough (canopy) | 0.04 to 0.08 m | Bark texture: models sit in a 0.08 m moss lip that overlaps the ribbon edge |
| Ledge, Planks | 0.03 to 0.06 m | Planks: model rests on the plank tops, no embed through planks |
| On a slope (Ascent/Descent up to 12 percent) | add `tan(slope) * footprintDepth / 2` | The rig's base follows `PathFrame` pitch; a rigid model never floats on its downhill side |

### 3.5 Hazard marks: red ochre trail marks [ASSUMED lore, Q1]

Hazard red and ink bands (style guide 4.1) are explained in the world as **red-ochre trail marks**: warning stripes and
handprints painted on wood and stone, and red cloth strips tied around ropes, by whoever crossed here before. This lets the
required bands sit on real surfaces. Rules: stripes follow the surface (wrap around the log, run along the underside of the
mat), at most one band group per object, always flanked by ink ticks, matte, no glow.

### 3.6 Variant selection

Each archetype has 2 to 3 **skins** per world (section 5 to 10). `SkinResolver` returns `(skinIndex, mirror, cpSide)` from a hash of
`(seed, StreamIds.Cosmetic, obstacleId)`. Rules: the same chunk repeated twice in a row never shows the same skin twice; the
first appearance of an archetype in a run (spec 002/GDD 8.3 teaching appearance) always uses skin 0, the clearest one.

---

## 4. Telegraph and reading rules (apply to every archetype)

1. All telegraph features of spec 002 5.3 stay: low barrier red band at the top edge, high barrier red-and-ink underside,
   full block red band at about 1 m, mover ink furrow plus red band, gap flags at the near edge. They are now **ochre marks on the
   real object** (3.5) at the same positions.
2. **Context telegraphs earlier than marks:** a limb above says "something hangs here", a root plate and stump say "a tree fell
   here", a wallow with a wedge says "this will roll". Context is visible from the spawn distance, marks from 40 m.
3. Reading times: the answer is readable at **2.0 s** at the planned speed and at 1.2 s at boost speed (spec 003 9.1). The route
   validator's viewport probe now includes CP and overhead limbs (AC-522).
4. Nothing grounded is ever hidden by its own context: the CP zone is beside the lane (4.2 and out), overhead pieces are above 4.6 m,
   and decals are flat.
5. Light shafts and mist never cover an obstacle's marks (art plan section 4).

---

## 5. Archetype A1: Low barrier (jump)  [priority 1]

**Hitbox:** 2.04 m wide per lane, 0.6 m deep, y 0 to 0.8 (spec 002 5.1). Masks of 1 to 3 lanes.

### 5.1 Real thing (Jungle, floor layer)

A **fallen trunk**: a mossy log that came down across the path.

| Part | Description | Size / fit |
|---|---|---|
| Log body | Squarish rot-flattened trunk (cross-section superellipse), bark peeling on top, moss on the shaded side | **0.6 m deep x 0.8 m high**, length = lanes x 2.4 - 0.36 (2.04, 4.44, 6.84 m) plus 0.15 m overhang per free end |
| Embed | The lower 0.08 m sinks into the ground with a displaced soil lip | G1 |
| Root-plate end (skins 0 and 1) | At one end beyond the lane span: the upturned root disc (2.2 to 3.0 m wide, 0.6 m thick, 1.4 to 2.5 m tall) with soil clumps, standing at the shoulder and CP zone | CP zone, <= 2.5 m high inside 4.2 to 5.0 m |
| Broken end | The other end: splintered break with white heartwood, a few branch stubs 0.25 m long | in the box |
| Stump source (skin 0) | The stump with a matching splinter scar 4.5 to 9 m from the log along its axis, on the root-plate side | CP zone |
| Crown (skin 1) | The fallen crown (leaf mass 1.6 m high) on the far side, lying on the verge | CP zone, <= 1.6 m |
| Single-lane log (2.04 m) | Both ends are splintered breaks; one end rests on a **mossy rock hump** (inside the box, <= 0.8 m high), the other on the ground; a `branch stub` supports it; a rolled-aside offcut and a drag scar (decal <= 0.12 m) lead to the nearest tree trunk 5 to 8 m away | no CP taller than 1.2 m |
| Ochre marks | Red band on the top ridge (wraps over the top front edge), ink ticks below | G8 |
| Contact | Decal under the log, leaf litter pile on the up-path side, 6 bark flakes | G2 |

**Hitbox mapping:** the log's cross-section is 0.6 x 0.8, so the top-front edge ghost distance <= 0.12 m. The visual top equals 0.8 m
(+0.08 m margin allowed by spec 003 AC-324). Where the log is rounder than the box, the soil lip and moss fill the lower-front
corner. Jumping hero stumbles from above (spec 002 5.2): the log top is flat enough (0.5 m wide plateau) to read as "I landed on it".

### 5.2 Skins

| Skin | Jungle floor | Jungle canopy (spec 003) | River | Mountains | Ruins |
|---|---|---|---|---|---|
| 0 (teaching) | Fallen trunk with root plate and stump | Branch fork stub on the bough: a limb fallen across the bough, bark burl, caught at its base on the bough edge | Driftwood log lodged on the bank (sun-bleached, rope-wrapped, a heron feather), a gravel bar under it | Snow drift: a wind-sculpted drift with a rock core (visible dark rock at the front-top edge) | Broken column drum: fluted stone drum lying on its side, a chipped capital next to it |
| 1 | Log with crown | Burl log | Beaver-gnawed log with chewed ends | Boulder row: three rounded stones touching, snow cap | Collapsed lintel piece |
| 2 | Single log on rock hump | n/a | n/a | n/a | n/a |

Per-skin contact: River logs sit in a gravel and mud lip with a tiny wet sheen at the base; Mountains drifts have a soft blue
shadow pocket and a ridge of blown snow on the up-path side; Ruins drums are set on a cracked paving slab.

Tris: <= 1,400 per lane-length (body) + CP <= 1,200 shared by two skins. Method: Meshy pair for the hero fallen trunk (art plan,
15 credits, already planned `Trunk_Fallen`); Blender procedural for rock hump, drift, drum, root plate disc.

---

## 6. Archetype A2: High barrier (slide)  [priority 2]

**Hitbox:** 2.04 m wide per lane, 0.5 m deep, y 1.1 to 3.0 (clearance 1.1 m under, 1.9 m tall mass). Masks of 1 to 3 lanes,
including the split mask `{0, 2}` (two mats, middle lane open).

### 6.1 Real thing (Jungle, floor layer)

A **root-and-liana curtain hanging from a limb** (spec 003 10.2: dull green, never glowing).

| Part | Description | Size / fit |
|---|---|---|
| Support limb | A thick limb (0.45 to 0.7 m thick) reaching over the path from a trunk in the CP zone, at **4.6 m** (underside), tapering from 0.7 to 0.35 m. The limb spans all lanes of the mask plus 0.5 m | CP zone trunk, limb in overhead zone |
| Ties | Per lane box: 2 liana ties (3 cm thick) from the limb down to the mat top at 3.0 m, one at each side of the box, with a knot wrap at the mat | G3 |
| Mat | A dense curtain of hanging root-vines (8 to 20 cm, brown, tapered) and leaf strips, **0.5 m deep, 2.04 m wide, from y = 1.1 up to 3.05**, layered in 3 staggered rows so no gap wider than 0.2 m runs through the depth | fills the box (G6) |
| Lowest band | Root tips end at y = 1.1 +- 0.05 on a ragged line; one bundle is wrapped with a **red-ochre and ink striped cloth** at y = 1.1 to 1.3 | G8, keeps "red underside = slide" |
| Sway | +-3 degrees at 0.4 Hz from the ties, amplitude fades to 0.5 degrees inside 12 m; the lowest tips never go below y = 1.05 (clearance protected) | AC-514 |
| Ground | A shadow pad (soft, 2.04 x 1.5 m) on the ground under the mat, offset toward the sun, and a few fallen leaves | G2 |

**Hitbox mapping:** the mat is the box. Hanging strands outside the box are shorter than 0.12 m. The limb and ties are above 3.0 m
and thin, so they never look like part of the lethal volume, but read as "the thing hangs from here". Because jump head height
(3.3 m) can touch the 3.0 to 3.3 band, the strands continue up to the limb; nothing in 3.0 to 3.3 is open sky.

### 6.2 Skins

| Skin | Jungle floor | Jungle canopy | River | Mountains | Ruins |
|---|---|---|---|---|---|
| 0 (teaching) | Root-and-liana curtain from a limb | Hanging vine curtain from a bough limb | Rope-and-reed fishing net hung from a limb, floats and a net knot, dripping | Overhanging ice ledge: a lip of the cliff wall (a rock shelf with icicles, underside at 1.1) | Stone beam: a fallen lintel wedged diagonally between two pillars (top ends rest on the pillar capitals) |
| 1 | Leafy bough: a snapped bough with its leaf mass caught in the lianas, hung by two roots | Low limb with a leaf mass | Low tree limb hung with creeper | Rock arch, root-ice cluster | Collapsed arch keystone hung by roots |
| 2 | **Plank-root arch** (only for masks of 2 to 3 lanes beside two big trunks): a flat buttress root 0.5 m thick, 1.9 m tall, underside flat at 1.1 across the span, curving to the ground at the CP zone | n/a | n/a | n/a | n/a |

Mountains skin 0: the ledge belongs to the cliff face (it merges into a Cliff_Wall piece in the CP zone); the single-lane mask
uses a **rock shelf pinned between two boulders** instead. Ruins skin 0: both pillar bases are CP (the beam rests, never floats);
the beam is 0.5 m deep and 1.9 m tall (a slab, not a log).

Tris: mat <= 1,200 per lane box, limb + ties <= 900 (shared across a row), plank-root arch <= 1,800. Method: Blender procedural
(`Vine_RootCurtain` from the art plan, `Branch_Thick`), plank-root arch from `Root_Arch` (Meshy pair, planned).

---

## 7. Archetype A3: Full block (change lane)  [priority 3]

**Hitbox:** 2.04 m wide per lane, **1.0 m deep**, y 0 to 3.0. Masks of 1 or 2 lanes (never 3, F5).

### 7.1 The ghost-depth problem

A round trunk 2.04 m wide would be 2.04 m deep. With the box 1.0 m deep, the trunk surface at the lane edge is 0.56 m behind the
box front, and a hero clipping the edge dies before touching anything visible. Rule: **the lethal front 1.0 m must be a flat-faced
mass.**

### 7.2 Real thing (Jungle)

A **buttress-root tree**: a giant tree whose flat plank-like buttress root wall faces the path, with the round trunk rising behind it.

| Part | Description | Size / fit |
|---|---|---|
| Buttress fin | A vertical wall of root wood with ridged, fluted face, moss in the cracks, root ridges splaying into the ground | **2.04 m wide, 1.0 m deep, 3.0 m tall** at the lane; a 3.0 to 4.0 m ragged top where it merges into the trunk |
| Trunk | Cylinder r = 0.9 to 1.1 rising from behind the fin up through the canopy (cropped by fog and leaves above 14 m) | sits **behind the fin** (center at box back + r), so its round face is hidden by the fin |
| Root web | Smaller roots crawl from the fin base 0.6 m into the lane gaps and the shoulder, <= 0.12 m high in the lane, up to 0.5 m in the CP zone | G1 |
| Two-lane mask | Two fins side by side with the 0.36 m crack between them filled by a **single trunk centered on the crack** (r = 1.2) and a root web bridging the base (<= 0.12 m) | one trunk, two fins |
| Marks | Red-ochre band around the fin at y = 1.0 (hip height), ink ticks | G8 |
| Contact | Wide leaf-litter apron on the up-path side, dark pad, debris | G2 |

**Hitbox mapping:** the fin covers the front face 100 percent (ghost <= 0.1 m); the trunk behind may extend past the box back by up to
2.2 m. That is acceptable because the validator already treats the box as an occluder up to 3.0 m (F7), and the added trunk depth
only adds hidden area behind the box. A stumbled hero (who ignores the box for the rest of the pass, spec 002 5.2) never visually
passes through the trunk: the stumble result pushes the hero sideways out of the lane box (spec 001 9.4); if a test shows a pass
through the fin, ghost-through is accepted for the fin's 1.0 m and a dust puff hides it [ASSUMED].

### 7.3 Skins

| Skin | Jungle floor | Jungle canopy | River | Mountains | Ruins |
|---|---|---|---|---|---|
| 0 (teaching) | Buttress-root tree | Thick trunk through the bough with a burl wall | River rock: a boulder with a flat-cut face from erosion, a mist of spray at its base | Rock pillar: a stack of rock plates, 1.0 m deep | Statue on a plinth (plinth fills the box; the statue is above 1.5 m) |
| 1 | **Standing stone slab**: a moss-covered upright slab 2.04 x 1.0 x 3.0, vine-wrapped, set in a ring of ferns (ancient marker; fits the treasure-hunt mood) | Large burl | Mid-stream rock with a flat shelf | Ice-fall pillar | Pillar |
| 2 | **Rock slab wedged between two trees** (outer lanes only): slab leans <= 1.5 degrees against a verge trunk, jam stones at its base | n/a | n/a | n/a | n/a |

Leaning is limited to 1.5 degrees so the top stays within 0.08 m of the box. Tris: <= 1,600 per fin + trunk shared with scenery
instancing (trunk uses the scenery tree-trunk mesh), standing stone 800, wedged slab 900.

---

## 8. Archetype A4: Mover (rolling boulder)  [priority 4]

**Hitbox:** 1.90 m wide, 1.6 m deep, y 0 to 1.9; moves 1 lane linearly at 4.8 m/s (30 ticks), triggered when the front is 1.4 s ahead
of the hero, never moves in z, settles in the end lane center (spec 002 5.5).

### 8.1 The sphere problem (quantified)

A 1.9 m sphere in the 1.9 x 1.6 x 1.9 box: it is 0.3 m deeper than the box, and at the box's front-face plane (0.8 m in front of the
sphere center) the sphere is a disc of radius **0.51 m = 0.82 m^2 of the 3.61 m^2 face = 23 percent**. A hero clipping the lateral edge
of the box dies where the sphere surface is **0.8 m behind** the lethal face (80 ms at 10 m/s). No sphere of any size fixes this:
a sphere touches the front face of an equal cube in one point.

### 8.2 Real thing: the barrel boulder (silhouette chosen to fit the box)

A **stone roller** (also a real thing: weathered, river-rounded barrel-shaped boulders exist; in the Mountains a *snow roller* is a real
natural cylinder; in the canopy a hollow log).

| Property | Value |
|---|---|
| Shape | Cylinder with the axis along the path (z): **radius 0.95 m, length 1.6 m**, end edges chamfered 0.15 m, lumpy +-0.04 m surface, a worn flat on one face |
| Front-plane fill | Disc radius 0.80 m = 2.01 m^2 = **56 percent**; at hero height the silhouette reaches the full lateral extent (x = +-0.95 at y = 0.95), so a standing hero overlapping the box in plan always meets visible stone within the 0.15 m chamfer |
| Ghost distance | <= 0.15 m for a standing hero; <= 0.25 m for a sliding hero at the lowest 0.35 m of the lateral edge (corner void: lateral sliver <= 0.18 m, inside edge forgiveness) |
| Fit box | 1.90 x 1.9 x 1.6 (authored directly to it; the 2.5x art-fit code in `ObstacleView.FitArtToBox` goes away) |
| Embed | 0.03 m below the ground at rest |
| Surface | 3 bands of lichen and exposed rock, 2 deep cracks; the red-ochre band wraps the middle (a 0.35 m wide ring with ink ticks) |

Optional sim alternative (section 12, P1): none needed; the barrel fixes the fit with the unchanged box.

### 8.3 Where it sits and what holds it (origin)

| Part | Description |
|---|---|
| Wallow (start) | A shallow sandy-soil hollow 0.10 m deep, 2.4 m wide x 2.2 m long, with a darker bowl and disturbed soil, decal plus rim <= 0.10 m |
| Chock | A wedge stone and a short stake jammed under the barrel on the **end-lane side**, plus 3 pebbles; visible from spawn |
| Furrow | A continuous groove (0.10 m deep, 0.5 m wide, scuffed gravel and bent grass) running from the wallow to the end lane (the spec 002 path stripe is now this furrow, ochre ticks along its edge every 1 m) |
| End sand bar (stop) | At the end lane center: a second loose-sand wallow (the "catch"), with fresh shallow impact marks already visible |
| Source CP | On the start side: a rock ledge or tree-root bank (CP zone) with a fresh scar and loose scree, which says where it rolled from; a trail of old pale roll marks (decal) lies behind it toward that bank |
| Cross-slope | The ground under the furrow is pitched <= 3 degrees toward the end lane (0.12 m over 2.4 m); the hero's feet remain on `y = 0` and the shadow pad hides it; slope decals only [ASSUMED] |

### 8.4 Motion rules

1. **Idle (spawn to trigger - 0.3 s):** the barrel rocks in place +-1.5 degrees at 0.7 Hz about the chock (center translation 0, within +-0.02 m). Pebbles tick down from the chock.
2. **Anticipation (trigger - 0.3 s to trigger):** rock amplitude rises to 5 degrees toward the end lane; dust sifts from the chock; camera shake none. No translation.
3. **Trigger tick (sim `MoverStarted`):** chock stone pops away (visual only, 0.4 s), a dust burst covers the contact for 0.3 s, and the barrel rolls. **Center x equals the sim `moverX` (interpolated).** Spin: no-slip, `omega = v / R = 4.8 / 0.95 = 5.05 rad/s` (0.80 turns/s); the surface point in contact with the ground is at rest relative to the ground within 0.1 m/s; the top moves at 2v. The roll direction follows the travel direction.
4. **Dust trail (while moving):** 20 puffs/s at the contact line, 0.4 to 0.7 m, 0.5 s life, ground-colored, max 20 live particles per mover, none with Reduce Motion beyond 6/s.
5. **Settle (sim `MoverSettled`):** the barrel enters the end sand bar: dust burst, a 0.10 m sink over 6 ticks (center y goes to -0.07 relative to rest), 3 ticks of squash 3 percent, then rocks +-3 degrees about the contact with damping 0.4 s, center translation <= 0.02 m. After that it sits still and stays lethal-looking.
6. **Sound/haptic cue:** a rumble ramps in at trigger (spec 002 telegraph, GDD 8.1 sound cue 1.2 s ahead is kept); none of this changes timing.

Limit [ASSUMED, section 12 P4]: the sim starts and stops at full speed in one tick, which a real rolling barrel would not. The visual hides the
launch with the chock pop and dust, and the stop with the sand bar. If a clip test shows it reads wrong, P4 proposes a sim ease-in and
ease-out (cost quantified there).

### 8.5 Spawn and teaching

The barrel is on screen from spawn with wallow, chock and furrow (the end circle of spec 002 is the sand bar); the end lane is readable
from spawn. First encounter (tier 3 and the tutorial) uses skin 0 only.

### 8.6 Skins

| Skin | Jungle floor | Jungle canopy | River | Mountains | Ruins |
|---|---|---|---|---|---|
| 0 | Barrel boulder in a wallow | **Hollow log**: a rolling log with a hollow end (the ring faces the player), set in a bark-chip bed | **Drifting raft** with a 1.9 m high stack of tied bamboo/crates, pushed by a visible current ripple line across the ford; no roll | **Snow roller** (a natural cylinder of rolled snow with a spiral groove) | **Stone disc roller** (millstone, 1.6 m thick drum) in a **carved stone channel** that is both the furrow and the stop (a socket at the end) |

River raft motion: a slow rock +-1.5 degrees, a bow wave and wake at both ends in the travel direction, a pole stuck in the bank at the start.
It stops against a mooring post set outside the lane at the end (a post, so within CP only). No spin.

Tris: barrel boulder 900, wallow/chock/furrow 600 (shared across worlds, reskinned), log 900, raft 1,100, snow roller 800, stone disc 900.

---

## 9. Archetype A5: Thorn patch (lane denial, Jungle signature)  [priority 5]

**Hitbox:** `LaneDenial` per covered lane: 2.04 m wide, **4.0 m deep, 2.2 m tall** (a Full-block-like mass, spans 2 lanes in chunks).

### 9.1 Real thing

A **thorn thicket growing out of old roots**: a bramble hedge-wall over a collapsed root cage.

| Part | Description | Size / fit |
|---|---|---|
| Root cage | A lattice of old fallen limbs and arching roots (0.1 to 0.25 m thick) that gives the rectangular volume | 2.04 x 4.0 x 2.2 per lane, continuous over the 2-lane mask (0.36 m crack bridged by canes) |
| Canes | Dense thorny canes (dark `#3E4A2E` thorn green, brown ink-lined thorns) climbing the cage; the **front face and the top-front 0.5 m are vertical walls** of tangled canes (no gap wider than 0.2 m), top edge ragged at 2.2 +- 0.1 | G6 |
| Thorns | Angular thorn hooks (the style guide: spiky = do something), 0.12 to 0.3 m, biggest on the front face | hazard language |
| Roots | Where canes enter the ground, thick roots emerge (embed 0.08 m) and spread 0.6 m; the thicket's origin is a **bush root crown** at the verge side of the patch, visible as a thick woody base in the CP zone with an upward cane fan | CP zone, <= 2.5 m |
| Ochre marks | Red-ochre stripes on 3 of the root-cage posts at the front (ink ticks); thorn tips are ink, never red | G8, removes the current red tips |
| Contact | Dark pad plus fallen cane litter and leaf litter, a few hanging seed pods (non-hazard color) | G2 |
| Sway | Canes tremble +-1 degree at 1.2 Hz in the top 0.5 m only | AC-514 |

**Hitbox mapping:** front and sides are walls; the box top is a ragged edge, so ghost <= 0.15 m at the front-top edge by construction. The
4.0 m depth is read as a hedge, not a bush: the side faces are walls too (a hero grazing the side stumbles, spec 002 5.2).

Skin 1: **thorn hedge along a fallen trunk** (the log's spine inside the thicket). Skin 2 (second thorn type for variety, not new gameplay): hooked vines (kudzu-like) over a rock mound with a flat top. Tris: 1,800 per lane-length (4 m), 2-lane mask = 3,400.
Thorn patch appears only in the Jungle (and Dusk Jungle); other worlds use strikes (section 10).

---

## 10. Archetype A6: Lane strike (telegraphed hazard, shared behavior, four skins)

**Hitbox:** `LaneStrike` box 2.04 m wide, 2.0 m deep, y 0 to 3.0, **live only while `StrikePhase == Active`** (36 ticks = 0.6 s). Phases:
`Dormant`, `Warning` 78 ticks (1.3 s), `Active` 36 ticks, `Rest` (cycle 204 ticks, rest 90 ticks). The sim fires `HazardWarning` at the
start of Warning and `HazardStrike` at the start of Active; a hero at constant speed arrives about 18 ticks into Active (trigger lead 1.6 s).

### 10.1 Visual contract (binding)

The box is live from the first Active tick and covers the whole column 0 to 3.0 m. So:

| # | Rule |
|---|---|
| S1 | **At the first Active tick, the model is solid over the lane from ground to >= 2.8 m.** Not later (the box would be a ghost), not before the permitted window below. |
| S2 | **Early window:** the model may look lethal (any part below 1.8 m over the lane) at most **18 ticks before the first Active tick**, because a hero cannot reach the box earlier (the trigger lead guarantees arrival at about +18 ticks). Nothing may reach below 1.8 m over the lane before that. |
| S3 | **Active hold:** the model stays solid for all 36 Active ticks (a bounce of <= 0.06 m is allowed). |
| S4 | **Release:** after Active ends, the model is below 1.0 m tall within 12 ticks or clear of the lane (all within the first 12 Rest ticks), and the lethal-looking part clears below 0.15 m or above 3.4 m within 30 ticks. |
| S5 | The pose is a **pure function of `(StrikePhase, StrikeTicks, alpha)`**, a static class `StrikePose.Evaluate` that EditMode tests exercise without Unity. |
| S6 | Telegraph (Warning, 78 ticks): the lane pad chevrons pulse (existing), plus the world-specific tell below at the times listed. A permanent presence (the support hangs there all run) means the hazard is readable from spawn. |

### 10.2 Jungle: log-drop trap (used for the Jungle strike, in dusk loops and wherever the track places a strike in the Jungle) [ASSUMED, Q2]

A **lashed log bundle held up by a liana over a pulley limb, with a counterweight stone**.

| Part | Description | Size |
|---|---|---|
| Log bundle | Four logs (0.9 m) lashed upright into a 2.0 x 2.0 m square column, 3.0 m tall, ochre-marked at hip height | fills the box (2.04 x 2.0 x 3.0) |
| Pulley limb | Support limb at 7.0 m over the lane from a CP trunk; the liana runs over a bark pulley groove | G3, CP zone |
| Counterweight | A netted stone hanging beside the trunk at the far end of the limb, going up as the bundle goes down | CP |
| Catch peg | A wooden peg through a loop holding the liana at the trunk | CP, visible, flicks out |
| Rest pose | Bundle hangs with its **bottom at 3.6 m** (above the 3.3 m jump head), swaying +-2 degrees | harmless-looking? No: it is hung over a pad with chevrons and ochre marks (reads as "a trap is set") |

Timeline (ticks since Warning start; g = 9.81, drop height 3.6 m, fall time `sqrt(2*3.6/9.81) = 0.857 s = 51 ticks`):

| Tick | Event |
|---|---|
| Warning 0 | Chevrons start; the bundle creaks and sways +-4 degrees; the catch peg shivers |
| Warning 27 | Peg flicks out (0.1 s); bundle falls from 3.6 m, bottom `y(t) = 3.6 - 4.905 t^2` (t in s from tick 27) |
| Warning 60 (18 ticks before Active) | bottom crosses 1.8 m: first moment it looks lethal (S2) |
| Active 0 (Warning tick 78) | bottom reaches 0.0: impact, dust ring, bounce <= 0.06 m, solid column (S1) |
| Active 0 to 36 | Stands; the liana goes slack, counterweight at its top |
| Rest 0 to 12 | Counterweight falls; bundle snaps up with spring recoil: bottom 0 to 1.8 m in 12 ticks, then continues up at 3.6 m/s to 3.6 m (S4) |
| Rest end | Bundle hangs and settles; the peg is reset by the time Warning begins (a visual reload of the liana at Rest tick 60 to 90, the peg slides back, clip-friendly) |

### 10.3 Per-world strike skins

| World | Real thing | Source and support | Timeline (tick = since Warning start) | Fill |
|---|---|---|---|---|
| River | **Water spout (geyser)**: a boiling vent in a rock-rimmed pool at ford level | Vent crater, mineral crust, mist, a **foam ring** on the pool; the crater lip is <= 0.12 m high | W0: pool bubbles; W40: dull gurgle, steam; W66 to W78: jet rises from the vent and reaches 3.0 m at **Active 0** (rise speed 3.0 m / 0.2 s = 15 m/s in the last 12 ticks); Active: standing plume, foamy white with ink-lined edges; Rest 0 to 12: collapses to spray < 1.0 m | Plume plan shape is a **rounded rectangle** 2.0 x 1.9 (a fan-shaped fissure jet), ghost <= 0.35 m allowed for fluid |
| Mountains | **Rockfall** from an **overhanging ledge** (Cliff_Overhang) with a perched slab, a visible crack line and trickling dust and pebbles | Ledge CP at 6.0 to 8.0 m on the outer side, spanning over the lane: the lane is under the overhang; slab sits on the lip | W0: dust trickle, shadow on the lane (existing pad) darkens; W18: crack opens; W27: slab tilts off the lip, which is 3.6 m above the lane (the ledge is built so the drop is 3.6 m, same 51-tick fall as 10.2; a higher ledge would need a longer fall than the Warning allows); Active 0: lands upright on its edge, solid slab 2.04 x 2.0 x 3.0 with a small dust ring; Rest 0 to 12: it **cracks and crumbles in place** into a rubble heap < 0.15 m (6 ticks of shard burst) | slab fills the box |
| Ruins | **Pressure-plate spike pillar** (replaces darts, Q2): a lit plate in the lane (existing pad becomes a carved plate with a glowing glyph), a **carved stone pillar with spike faces** rising from a slot | Slot under the floor, the pillar is the same 2.0 x 2.0 x 3.0 mass, wall slots at both sides (CP) | W0: glyph glows; W40: grinding sound; W66: slot dust; W66 to W78: pillar rises (3.0 m in 12 ticks, 15 m/s) to be at full height at **Active 0**; Active: stands; Rest 0 to 12: retracts below 1.0 m (S4), then slowly to the floor | slab fills the box |
| Jungle (alternate) | Log-drop (10.2) | see above | see above | see above |

Darts alternative (GDD 8.2) is unfit for the box (a dart volley fills < 15 percent of 2.0 x 2.04 x 3.0). See Q2 for the sim-change option.

Tris: log-drop 1,300 + limb 700 (shared); spout 600; rockfall slab 700 + ledge (shared Cliff_Overhang 1,800); spike pillar 900 + slot 300.

---

## 11. Spawn, despawn, pop-in and LOD (all archetypes)

| Rule | Value | Notes |
|---|---|---|
| Spawn distance | Rig spawns when its nearest point (decals, forward debris, CP) is at 95 m or farther and its front is within 100 m of the hero | Fog end is 90 m: spawn is fully fogged. The forward overhang (decal, CP, wallow) is at most 5 m |
| Never inside the telegraph | No state change that adds geometry (CP, marks, LOD switch, material swap) occurs closer than **45 m** (fog start) at any speed. At boost speed (33.6 m/s) 45 m is 1.34 s, still earlier than 1.2 s | Replaces a view-distance rule: the telegraph envelope is 25.2 m at normal and 40.3 m at boost |
| LOD | LOD0 under 50 m, LOD1 (60 percent tris) from 50 m. Switch happens with a 6 m hysteresis band and cross-dither inside the fog band | No LOD swap inside 45 m |
| Canopy/curve cover | Where possible spawn is hidden by a bend or canopy edge (the route's sight corridor is unchanged); if not, fog is the cover | |
| Despawn | At `despawnBehindM` plus the rig's rear overhang, 15 m behind the hero plus 4 m: behind the camera (6 m back) | CP and decals leave with the rig |
| Pool | Rigs pooled per archetype (spec 002 8.6 counts plus 1 CP prop pool per CP type); 0 allocations per frame | AC-515 |
| Hiding the pop of scenery under CP | The placer skips cells overlapping keep-outs; the keep-out query is read once per cell rebuild | AC-509 |
| Gap/ravine lips | Always spawned with the ravine: edges, rim roots and mist on the same pool slot | section 13 |

---

## 12. Sim-change proposals (not applied; quantified)

| # | Proposal | Cost / benefit | Recommendation |
|---|---|---|---|
| **P1** | Change the mover box to fit the sphere | **Does not work**: any box leaves a sphere at 23 percent front fill at best. Rejected. The fix is the barrel silhouette (8.2). | Rejected; no change |
| **P2** | Make the strike box time-varying (a falling log: box bottom follows `3.6 - 4.9 t^2`, top = bottom + 3.0, live from the start of the fall) | Removes the early-window rule, but the box becomes lethal for the ~51 fall ticks as well (trigger lead must grow by 0.85 s, to 2.45 s, chunk spacing and F3 rows change); validator and golden traces re-baselined; large cost for a modest gain over 10.2 | Not now. Revisit if the clip test says the first 18 ticks look bad |
| **P3** | Full block depth 1.0 to 1.5 m for the buttress fin | Chunk gaps grow 0.5 m; F3 row spacing (9.1 m min) has 0.9 m slack; trunk stays behind; benefit small because ghost is already <= 0.1 m | Rejected |
| **P4** | Mover ease-in and ease-out: `x(t)` ramps over the first 6 ticks (acceleration 48 m/s^2) and last 6 ticks; the move takes 36 ticks instead of 30 | Settle margin 0.9 s becomes 0.8 s (F8 needs 0.5 s: still passes); mover leaves the start lane 0.05 s later; validator re-run for the 4 mover chunks; all T3 golden traces change; balance S1 to S9 re-run (expected effect: none above noise) | Optional; **recommend only if the owner finds the launch or stop unnatural after seeing the chock/dust visual** (Q3) |
| **P5** | Strike box height 3.0 to 2.2 for river and ruins skins | Lower fill demand; jump head can pass at 2.2 m? Hero bottom peaks 1.5 m, so a jump does not clear 2.2 m: no gameplay change | Rejected; keep 3.0 so one tested box is shared |

---

## 13. Ravines, gap edges and the vine chasm

### 13.1 Ravine (gap, jump)

The sim: ground missing for 3.0 or 4.0 m in the gap's lanes (spec 002 5.4). Today `GapView` is an ink slab on the ground plane plus
a flag; there is no depth, no rim and no walls.

Real thing: **a crack in the forest floor over a drop**.

| Part | Description | Size |
|---|---|---|
| Void | A recessed pit with **depth 14 m visible** (faked by two stepped wall layers and a dark gradient plane at -14 m); the old slab is replaced; the void is dark teal/ink, never red | wall layers at 0 to -6 m and -6 to -14 m, fog-matched |
| Near and far lips | Broken soil and rock lips: the lip face is 0.6 m deep stepped ledge, grass and roots overhanging the edge 0.2 m, a lighter fresh-break color along the edge | the lip top is exactly at ribbon level (G1); lips are the new `Ground_RavineEdge` prefab with the correct fit box |
| Walls | Visible rock/soil walls with strata lines, 5 to 8 hanging roots (thin, brown) and a mist layer at -8 m | |
| Hanging roots | Roots from the near-lip overhang hanging 1.0 to 3.5 m into the gap (sway +-4 degrees); none reaches the hero's jump arc (< 4.5 m above the ground) | art, not collision |
| Flags | The red-and-ink flags become **ochre-striped stakes** with a tied cloth strip, set 0.1 m before the near edge, 2 stakes per gap (outer corners) | G8, style 4.1; flags on the near edge only |
| Mist | Slow mist cards inside the void at -3 m, additive, low alpha | |
| Outer-lane gap | In an outer lane the gap reaches the path edge; the cut continues into the shoulder and verge ground with the wall continuing into the CP zone (a stepped bank), so the ravine looks like it crosses the whole terrain, not just the path | |
| Landing | Far lip has a dust patch to show where to land and a rim of grass | |
| Canopy layer (spec 003) | Broken bough: splintered limb ends, the gap shows the forest floor 24 m below with fog | spec 003 A9 |

**Hitbox mapping:** the gap is a ground absence: the lip top edge is exactly the sim edge (within 0.05 m); the lip stepping toward the
void is a 0.2 m overhang only on the near side (the hero's feet leave at the sim edge, no foot clipping). Telegraph: void and flags
are visible at spawn.

Per world: River creek gorge (water 12 m below with white foam, rope remnants), Mountains cliff gap (rock strata, broken bridge planks
hanging on one rope at the far lip), Ruins pit (collapsed floor, shards of paving, ancient stairs seen below). Tris: lips 600 each, walls 1,200 (shared strips), roots 300, mist 100.

### 13.2 Vine chasm (the 16 m swing chasm)

The sim: a 16 m ground absence under a vine section (GDD 7, spec 004 4.6): the near rim 4 m before the vine pivot, the far edge 12 m past it (chunk data `Gap(All, Z - 4, 16)`). Spec 003 8.3 dresses the canyon. This spec adds the grounding rules:
- The same lip, walls and strata as 13.1 but **taller and longer walls**: fixed 30 m depth, cliff sides continue 30 m before and after the chasm into the CP zone as a stepped bank (so the chasm reads as a canyon crossing the whole land, not a ditch).
- A visible **river or darkness** at the bottom (River: a stream; Jungle: a dark leaf-floor with mist), light shafts allowed inside.
- Near and far lips as in 13.1 plus a worn takeoff patch (bare soil, footprints) and a landing glade (spec 003 8.2).
- Missed vine death: the hero falls past visible walls (the cause text is unchanged).
Tris: walls 2 x 1,800, lips 2 x 600, dressing 800.

---

## 14. Coins and power-up pickups

### 14.1 Coins (placement is the sim's; this spec only defines how they sit)

Sim: 0.5 m coins at y = 0.75 m on `Line`, `Arc`, `Trail` patterns (spec 002 10).

| Rule | Value |
|---|---|
| Natural line | Lines run along the worn-trail wear tracks already drawn in each lane; `Trail` coins follow a visible **bent-grass track** (flattened ferns and footprints, decal <= 0.02 m) between the lanes; `Arc` coins sit over the obstacle they refer to: the arc's apex is above the log or ravine it asks the hero to clear |
| Glint pool | Each coin has a soft gold glint pool on the ground below it (0.5 to 0.8 m, brightness falling with height, hidden when the coin is above 2.0 m): the coin reads as a glowing object, not a hovering disc, and the pool lets the player judge the jump arc |
| Sparkle ribbon | `Arc` and `Trail` coins are joined by a faint additive pollen ribbon (alpha 20 percent) so the pattern reads as one curve; ribbons only on patterns >= 5 coins |
| Source | A line of >= 6 coins starts at a **coin cache** in 1 of 3 lines (hash): a half-buried clay offering bowl, a hollow root nook or a cloth bundle at the shoulder/CP zone (<= 0.5 m, no hitbox) with a golden glow; the line "spills" from it. The line ends in a soft sparkle fade (no drain prop) |
| Clearance | The coin radius + 0.1 m from every hitbox is spec 002 C1; the rig adds nothing within 0.3 m of a coin sphere (decals only) |
| Gem and read | Gold with a turquoise gem center (existing) so it reads against gold scenery; value contrast vs the background is checked in the art review |
| Spin | One turn per second (existing), phase offset by id |
| Over a ravine | An arc over a gap shows a thin pollen ribbon only; no cache |
| Magnet | Coins pulled by the Magnet fly on a curved path with a short comet tail (existing sim motion; no new rule) |

Tris: coin 140 (existing art) with glint quad 2; cache 200 each (3 variants).

### 14.2 Power-up pickups

Sim: a pickup in a free lane at **y = 1.0 m**, collection radius 0.6 m (power-up config). Pickups in the sim have no box.

Real thing: **a luminous pod growing out of a ground hollow**, a plant spore/seed pod that releases the power-up charm on its rising motes.

| Part | Description | Size |
|---|---|---|
| Hollow | A shallow moss-and-stone ring on the ground, radius 0.7 m, height <= 0.10 m (no collision: the hero steps over it), with a soft glow disc of the power-up's cool color (Magnet cyan, Shield sky blue, Boost magenta) | G1, G2 |
| Mote column | A tapered cone of 12 additive motes rising from the hollow to the icon (radius 0.25 m at the base, 0.1 m at the icon), 1 Hz | looks like the icon is lifted by the hollow |
| Icon | The existing 0.8 m icon (Magnet "U", Shield dome, Boost chevron) with ink rim, bobbing 0.15 m at 1 Hz at y = 1.0; **the column and the glow pool tie it to the ground** | |
| Carrier skin | 3 variants for variety, same footprint: **root cradle** (3 roots cupping the glow), **stump bowl** (a hollow stump top at 0.10 m as a low stump cap, a ring), **flower pod** (a large closed bloom 0.12 m high) | all <= 0.12 m |
| Unclaimed pickups in the canopy layer | The hollow is a knot in the bough surface | |
| Collect | The motes burst into the icon colored sparks, the hollow dims over 1.5 s and stays dull (not removed); no pop | |

Why not a stump pedestal: a 0.6 m stump in the lane would let the hero run through a solid-looking prop. All carriers stay <= 0.12 m.
Rule: pickup colors are cool, never gold (coins) and never red (hazard).

Tris: hollow 300, motes (particles), icon (existing 3 groups).

---

## 15. Tri budgets and art asset list

### 15.1 Budgets (obstacle layer, in the 95 m view window; inside the global 150k and the scenery 60k of spec 003 11.5) [ASSUMED]

| Item | Tris | Draws | Notes |
|---|---|---|---|
| Obstacle bodies (<= 10 rows in view, <= 4 within LOD0 50 m) | 12k | 8 | LOD1 beyond 50 m |
| Context pieces (instanced CP types) | 5k | 5 | root plates, stumps, limbs, ledge pieces |
| Ravine / chasm walls and lips (1 to 2 in view) | 4k | 4 | |
| Coins (<= 120 in view) | 17k worst case at 140 tris | 1 (instanced) | |
| Decals, pads, glints | 1k | 2 | one atlas |
| Pickups / hollow | 1k | 2 | |
| **Total** | **40k** | **22** | Hard limit for this layer: 45k and 28 draws (AC-519) |

If the coin mesh is 140 tris and 120 coins are in view, it is 16.8k; coin LOD to 40 tris beyond 25 m cuts this to 9k [ASSUMED].

### 15.2 Asset list per world (asset-pipeline; method Blender procedural unless stated; names extend `EnvironmentArt`)

| Group | Assets (Jungle first) | Tris each | Method |
|---|---|---|---|
| Low barrier | `Obst_Log_Trunk` (3 lengths), `Obst_RootPlate` (2), `Obst_Stump`, `Obst_Log_Crown`, `Obst_RockHump` | 1,400 / 1,200 / 500 / 700 / 400 | `Trunk_Fallen` Meshy pair (15 credits, planned); rest Blender |
| High barrier | `Obst_HangMat` (3), `Obst_Limb` (2), `Obst_LianaTie`, `Obst_BoughMass`, `Obst_PlankRootArch` | 1,200 / 900 / 120 / 900 / 1,800 | Blender; arch from `Root_Arch` Meshy pair (planned) |
| Full block | `Obst_ButtressFin` (3), `Obst_TrunkBehind` (reuse `Tree_TrunkSeg`), `Obst_StandingStone` (2), `Obst_WedgedSlab`, `Obst_RootWeb` | 1,600 / 0 / 800 / 900 / 300 | Blender; `Tree_Buttress` Meshy pair (planned) as the fin base |
| Mover | `Obst_BarrelBoulder` (2), `Obst_Wallow` (decal mesh), `Obst_Chock`, `Obst_Furrow` (strip), `Obst_SandBar`, `Obst_DustPuff` (particle) | 900 / 200 / 120 / 150 / 150 | Blender; existing `Obstacle_Boulder` retired or reused as a rock in the scree CP |
| Thorn patch | `Obst_ThornCage` (3), `Obst_CaneWall` (2), `Obst_RootCrown`, `Obst_ThornLitter` | 1,800 / 1,200 / 600 / 100 | Blender |
| Strike (Jungle) | `Obst_LogBundle`, `Obst_PulleyLimb`, `Obst_Counterweight`, `Obst_CatchPeg` | 800 / 700 / 300 / 60 | Blender |
| Ravine | `Ground_RavineEdge` (replace), `Ravine_Wall` (2 strips), `Ravine_Roots`, `Ravine_Mist` | 600 / 1,200 / 300 / 8 | Blender |
| Chasm | `Chasm_Wall` (2), `Chasm_Lip` | 1,800 / 600 | Blender |
| Coins / pickups | `Coin_Glint` (decal), `Coin_Cache` (3), `Pickup_Hollow` (3), `Pickup_Motes` | 2 / 200 / 300 / particle | Blender |
| **Jungle total new** | about 55 models | | **Meshy credits: 0 beyond the 45 planned in the art plan** (re-use the three planned pairs); no new Meshy spend in this spec |

River adds (all Blender): `Obst_Driftwood` (2), `Obst_Net` (hung net, floats), `Obst_RiverRock` (2), `Obst_Raft` (2), `Obst_Spout` (vent, crater, foam ring, plume cards), `Obst_Gorge` (water, foam, rope remnants), `Obst_Mooring`. Mountains: `Obst_Drift` (2), `Obst_RockRow`, `Obst_IceLedge` (reuses `Cliff_Overhang`, Meshy planned), `Obst_RockPlates`, `Obst_SnowRoller`, `Obst_RockfallSlab`, `Obst_Rubble`, `Obst_BrokenBridge`. Ruins: `Obst_ColumnDrum` (2), `Obst_Lintel`, `Obst_Statue`, `Obst_StoneDisc`, `Obst_Channel`, `Obst_SpikePillar`, `Obst_PlateSlot`, `Obst_Pit`. Tri per piece stays <= 1.5k (small) and 3k (hero), atlas 1024 px per world.

---

## 16. Numbers (config)

All presentation-only; `Assets/_Game/Config/ObstacleGroundingTuning.asset` (never in the sim hash). Starting values [ASSUMED].

| Field | Unit | Start | Range |
|---|---|---|---|
| `embedMinM` / `embedMaxM` | m | 0.03 / 0.12 | 0..0.2 |
| `maxFloatM` | m | 0.02 | 0..0.05 |
| `contactDecalScale` | x | 1.2 | 1.1..1.4 |
| `debrisPerObject` | count | 6 | 4..10 |
| `ghostMaxStaticM` / `ghostMaxMoverM` | m | 0.15 / 0.25 | |
| `cpZoneInnerM` / `cpZoneOuterM` | m | 4.2 / 7.0 | |
| `cpMaxHeightNearM` | m | 2.5 | 1.0..3.5 |
| `cpMaxHeightInnerBendM` | m | 1.2 | 0.5..2.0 |
| `limbHeightM` (high barrier) | m | 4.6 | 4.0..6.0 |
| `limbHeightStrikeM` | m | 7.0 | 5.0..9.0 |
| `matSwayDeg` / `matSwayHz` | deg / Hz | 3 / 0.4 | |
| `swayFadeDistM` | m | 12 | |
| `rollRadiusM` | m | 0.95 | locked to kit |
| `rockIdleDeg` / `rockAnticipationDeg` | deg | 1.5 / 5 | |
| `anticipationS` | s | 0.3 | |
| `dustPuffsPerS` | /s | 20 | 6 with Reduce Motion |
| `dustMaxLive` | count | 20 | |
| `sinkOnSettleM` / `sinkTicks` | m / ticks | 0.10 / 6 | |
| `strikeHangBottomM` | m | 3.6 | 3.4..4.5 |
| `strikeGravity` | m/s^2 | 9.81 | |
| `strikeEarlyWindowTicks` | ticks | 18 | 0..30 |
| `strikeReleaseTicks` | ticks | 12 | |
| `strikeHoldBounceM` | m | 0.06 | |
| `popInMinDistM` | m | 45 | >= fog start |
| `lod0DistM` / `lodHysteresisM` | m | 50 / 6 | |
| `spawnNearestPointM` | m | 95 | >= fog end + 5 |
| `coinGlintFadeHeightM` | m | 2.0 | |
| `pickupHollowRadiusM` / `pickupHollowHeightM` | m | 0.7 / 0.10 | |
| `layerTriBudget` / `layerDrawBudget` | tris / draws | 45,000 / 28 | |

---

## 17. Feel targets (measurable)

| Target | Value |
|---|---|
| Visual position error vs box (any moving part) | 0 m at the interpolated tick; never trailing; leading <= 2 ticks (33 ms) |
| Strike visual vs box | S1 to S4 (10.1), error <= 1 tick |
| Rolling no-slip error | contact point speed <= 0.1 m/s |
| Pop-in | 0 visible pops closer than 45 m (video check at 21 m/s and 33.6 m/s) |
| Frame cost of the obstacle layer | <= 0.5 ms CPU on iPhone 11 / SE 2; 0 allocations per frame |
| Hit explainability | In the death replay frame (1 s hold), the lethal object is visibly overlapping the hero's box (ghost <= limits) in 100 percent of the audit's captured hits |

---

## 18. Implementation plan (small tasks; each compile-checked and playable)

| Task | Owner | Depends on | Deliverable | Done when |
|---|---|---|---|---|
| **G1** | gameplay-engineer | none | `ObstacleGroundingTuning.asset` (+ asset class), `SkinResolver` (stateless), read-only `TrackSimulation.GetKeepOuts`, static pure poses `StrikePose`, `MoverRollPose` (EditMode-testable) | AC-506/509/510/516/517 pass; sim hash unchanged (AC-302) |
| **G2** | ui-engineer | G1 | Rig refactor of `ObstacleView`, `HazardView`, `GapView`: Body (fit box) + Context + decals + debris; grounding by `PathFrame` pitch; gray-box context pieces (colored primitives) so layout and fairness can be tested before art | AC-501/502/503 on gray-box; game plays like today |
| **G3** | asset-pipeline | G2, art-director (contact sheet) | **Wave 1 (Jungle, the five common archetypes):** low barrier, high barrier, full block, boulder barrel with wallow/chock/furrow, thorn patch; prefabs with fit boxes and context pieces | art review of a still per archetype; AC-504 |
| **G4** | ui-engineer | G3 | Wire wave 1: ochre marks, sway, mover idle/anticipation/roll/settle effects, dust, thorn sway, spawn/LOD/despawn rules | AC-510 to 515; PlayMode view test |
| **G5** | gameplay-engineer | G1 | Tools: `FitBoxAudit`, `GroundingAudit` (embed, float), `RedAudit` (extends spec 003 AC-323), `PopInProbe`, `StrikeTimeline` test | tools run in CI; AC-504/505/512/516/520 |
| **G6** | asset-pipeline + ui-engineer | G4 | **Wave 2 (Jungle):** ravine and chasm (13), coins (glint, cache, ribbons), pickups (hollow, motes) | AC-518, 523, 524 |
| **G7** | asset-pipeline + ui-engineer | G6 | **Wave 3 (Jungle):** log-drop strike (10.2) and canopy skins | AC-516/517 |
| **G8** | game-designer + art-director | G4 | Clip test pass of wave 1 (spec 003 14.6 plus an obstacle reel: 6 obstacle encounters, 5 s each); fixes go back to G3/G4 | owner signs the reel |
| **G9** | asset-pipeline + ui-engineer | G8 | River set, then Ruins, then Mountains (same order as spec 003 T10) incl. strike skins | each world passes the reel |
| **G10** | performance-engineer | G4 | Measure the obstacle layer on iPhone 11/SE 2; refine 15.1 | AC-519, 521 |
| **G11** | qa-engineer | G5 | Screenshot gallery: 50 seeds x 4 worlds, each archetype at 2.0 s and 1.2 s ahead; video of pop-in at 21 and 33.6 m/s | gallery reviewed |

**Production order** (quality over schedule; the game stays playable after each step):
1. Wave 1 Jungle (G1 to G4): **low barrier, high barrier, full block, boulder, thorn patch**, in that order within the wave (each one is merged when it passes its still and AC).
2. Tools (G5) in parallel with wave 1.
3. Wave 2: ravine, chasm, coins, pickups (G6).
4. Wave 3: strike (G7), then clip reel (G8).
5. Worlds: River, Ruins, Mountains (G9).

Art-director owns review checklist: the art plan section 5 plus these items: (1) can I say what the object is and what it is touching? (2) is there a visible origin? (3) does the silhouette fill the hitbox at hero height? (4) red only as ochre marks on a hazard? (5) pop-in test at 21 m/s.

---

## 19. Acceptance criteria (numbers are `[ASSUMED]` until the first audit run)

Grounding
- **AC-501** For every grounded prefab in every skin, the lowest vertex of the Body is between `-embedMaxM` and `-embedMinM` relative to the rig root (embed 0.03 to 0.12 m), and no ground-level part floats above the ribbon surface by more than 0.02 m on slopes up to 12 percent (`GroundingAudit`, 100 percent of prefabs).
- **AC-502** Every grounded rig has a contact decal 1.1 to 1.4 times the footprint and 4 to 10 debris pieces <= 0.15 m high.
- **AC-503** Every elevated part (hanging mat, strike bundle, pulley limb, spike pillar slot) has a support or slot in the prefab hierarchy; an audit script fails a prefab where an elevated mesh has no connected support mesh within 0.05 m.
- **AC-504** `FitBoxAudit`: ghost distance <= 0.15 m for low barrier, high barrier, full block, thorn patch and strike (Active) and <= 0.25 m for the mover, for every skin, standing and sliding hero.
- **AC-505** Hazard red appears only on hazard prefabs, only as ochre marks flanked by ink; zero red on scenery, context pieces of non-hazard props, coins or pickups.
- **AC-506** The skin for a given `(seed, obstacle id)` is identical in 1,000 reruns and independent of frame rate; across the same chunk twice in a row the skin differs in >= 90 percent of cases; the first appearance of an archetype always uses skin 0.
- **AC-507** Context pieces stand in the CP zone (>= 4.2 m) except the obstacle's own overhead limb; none is inside the lane band; none exceeds the height limits of 3.3 (2.5 m near, 1.2 m inner bend) on any seed (automated over 1,000 seeds x 4 worlds).
- **AC-508** On the inner side of a bend, no CP exceeds 1.2 m in height; on the outer side 2.5 m inside 4.2 to 5.0 m.
- **AC-509** No scenery prop footprint overlaps a CP keep-out (1,000 seeds); rebuilt cells are identical (extends AC-309).

Motion
- **AC-510** The mover's visual center x equals the sim `moverX` (interpolated) within 0.02 m on every frame of a 1,000-trace replay; spin satisfies `|omega * R - v| <= 0.05 m/s` while moving; contact point speed <= 0.1 m/s; the pose is a pure function (EditMode test of `MoverRollPose`).
- **AC-511** The mover starts moving on the `MoverStarted` tick (+- 1 tick) and the chock pop is within 2 ticks of it; settle sink and rock complete within 0.4 s of `MoverSettled`.
- **AC-514** Sway amplitudes: mat 3 degrees (0.5 inside 12 m), thorn canes 1 degree; the high barrier's lowest tip never goes below y = 1.05 and never above y = 1.20; the thorn top-front edge stays within the box (+0.08 m).
- **AC-515** 0 allocations per frame in the obstacle layer; pools never grow in a 5,000 m run.
- **AC-516** `StrikePose` EditMode test: bottom y at Active tick 0 <= 0.05 m; at Warning tick 60 (18 ticks before Active) it is 1.8 m +- 0.1 m; no tick before Warning tick 60 has bottom <= 1.8 m; during Active ticks 0 to 35 bottom <= 0.06 m; at Rest tick 12 the bottom is >= 1.0 m (S4); the bottom is above 3.4 m or below 0.15 m within 30 Rest ticks. Equivalent tests per skin (spout height 3.0 m at Active 0, pillar height 3.0 m at Active 0, slab solid at Active 0).
- **AC-517** In 1,000 simulated hero runs through a strike chunk, a hero dying to a strike has the visual solid over the lane (ghost <= 0.15 m) in 100 percent of deaths; a hero passing in the rest window sees no lethal-looking part over the lane for more than 12 ticks after the box dies.

Spawn and visibility
- **AC-512** Pop-in probe: for every rig and CP, the first frame it is visible has all its geometry >= 90 m away and no LOD swap or material swap happens closer than 45 m, at 10, 21 and 33.6 m/s (headless camera model).
- **AC-513** Telegraph unchanged: all obstacle fronts, mover end sand bars, ravine stakes, strike pads are inside the viewport (6 percent margin) and not occluded by their own context at 2.0 s and 1.2 s (extends AC-322; CP and limbs included as occluders).
- **AC-522** The route validator's viewport probe includes CP and overhead limbs as occluders and passes for 10,000 seeds x 5,000 m x 4 worlds (rule: CP never hides a lethal or answer feature).

Budgets
- **AC-518** Ravine lips sit at the sim edge within 0.05 m for both lips and all gap lengths (3.0 and 4.0 m); the void is visible from spawn.
- **AC-519** The obstacle layer in the benchmark route stays at or below 45k tris and 28 draws, and costs <= 0.5 ms CPU on iPhone 11 / SE 2.
- **AC-520** The asset audit finds every archetype x world x skin prefab named in section 15.2 with fit box, tri count within budget and a license entry in `docs/LICENSES.md`.
- **AC-521** Coins: the glint pool follows the coin's x/z and its y fades correctly (hidden above 2.0 m) in 100 percent of frames; pooled; 0 allocations.
- **AC-523** Pickups: the hollow is <= 0.10 m high (no collision), the icon at y = 1.0 +- 0.15 m, and the mote column connects hollow and icon in 100 percent of frames.
- **AC-524** Regression: AC-302 (sim golden traces) and spec 002 AC-201 to AC-250 pass unchanged.

---

## 20. Simulation targets (balance-simulator)

No sim number changes in the default plan. Regression only: S1 to S9 identical for the same seeds. If the owner approves P4 (Q3), balance-simulator re-runs the four mover chunks (T3-01 to T3-04) for F8 (settle margin >= 0.5 s: expected 0.8 s), S1 to S9, and the golden traces are rebaselined once. If P2 is ever chosen, F3 and the strike trigger lead are re-derived.

---

## 21. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Context pieces in the CP zone hide something | AC-522 uses them as occluders; outer-side default; heights capped; inner-bend 1.2 m |
| R2 | The barrel boulder looks like a log or machine part | Lumpy surface, lichen and cracks, a worn flat, chamfered ends; art review of a still and the clip reel |
| R3 | Instant start/stop of the mover reads as odd | Chock pop, dust cover, sand bar; P4 as the fallback with known cost (12) |
| R4 | The log-drop's first 51 ticks are visible while the hitbox is not live yet | S2 caps lethal-looking time to 18 ticks, which the hero cannot reach; if tests find a case (a slow hero), P2 |
| R5 | Too many skins x 4 worlds | Wave order; skin 0 per archetype first; skins 1 and 2 are extras after the reel |
| R6 | Red ochre lore is rejected by the owner | Bands can be shader overlays on the same positions with no change to geometry (Q1) |
| R7 | Pickup hollows add clutter | Single glow color, <= 0.10 m, one per pickup (max 1 in view normally) |
| R8 | Hidden Stumbled-hero ghost-through of the fin | Dust puff; accepted [ASSUMED]; check in the audit |

---

## Open questions for owner

All have a recommended default that is already applied (`[ASSUMED]`). Work proceeds on the defaults.

1. **How are the red hazard bands explained in the world?**
   a) Red-ochre trail marks painted on wood and stone and tied cloth strips, as if left by earlier explorers (recommended: keeps red only on hazards and sits on real surfaces).
   b) Shader overlay only (no in-world object, simplest, least grounded).
   c) Natural markings only (red moss, berries; weaker signal, risks confusion with scenery).
2. **Which strike skins do we build?**
   a) Jungle log-drop trap (dusk loops), River geyser, Mountains rockfall, and Ruins **spike pillar replacing darts** (recommended: all four fill the box and have visible sources).
   b) Keep Ruins darts and shrink the strike box for that skin only (needs a sim change P5-style, a new validator pass, lower fill).
   c) No Jungle strike; Jungle only has the thorn patch, and the log-drop is cut (smaller scope; GDD already says so).
3. **Do we change the sim so the rolling boulder eases in and out (P4)?**
   a) No: visual-only hiding with chock pop, dust and sand bar; revisit after the clip reel (recommended).
   b) Yes now: 6-tick ease in/out in the sim, move time 0.6 s, settle margin 0.8 s, golden traces rebaselined.
   c) Replace the mover with a pendulum/swing that starts and stops naturally (new design, new chunks).
4. **May obstacle context pieces stand at the edge of the sight corridor (4.2 to 7.0 m, height-capped, outer side of bends)?**
   a) Yes, as specified, and checked by the viewport probe (recommended: needed for believable origins).
   b) Only below 1.2 m everywhere (stumps and low root plates; hanging mats and strike limbs lose their trunks, so the supports float: not recommended).
   c) No context pieces at all (obstacles stand alone: the opposite of the direction).
