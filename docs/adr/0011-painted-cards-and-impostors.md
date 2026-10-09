# ADR 0011: Painted cards, impostors and painted FX for the painterly hero basin

- Status: Proposed (painterly v4, 2026-10-09; owner rule: cheap tricks for the hardest parts, 60 fps on iPhone 12)
- Date: 2026-10-09
- Deciders: tech-architect
- Numbering: ADR 0010 is used by the device quality tiers (`Scripts/App/Perf/QualityTiers`, performance work in progress).
- Hard to undo: **no.** Each trick is a switch in `HeroCards` (the `_cards` block of `HeroBasinConfig_Painterly.asset`).
  Turning them off brings back the 3D arch, the layered plunge, kit pillars and the procedural foam.

## Context
Painterly v3 matched F4_f at about 70%. The parts that failed are the parts where geometry is expensive and still
wrong: the arch's silhouette (two 15k-tri instances that read as a low, thin band), the plunge (5 layered columns plus
spray and plume cards), flat white foam, and slab-like cascades. These parts are 150-400 m from the camera. At that
distance a 5 m sideways camera move changes the view by about 1.3 degrees, so a flat painting at the right depth
shows no visible parallax error.

## Decisions
1. **Projection-matched matte cards.** The arch (`BD_F4f_ArchCard`) and the far fall (`BD_F4f_FarFalls`) are quads
   placed by casting the landscape camera's rays through a screen rectangle (`ArchScreenRect`, `FallScreenRect`) to a
   plane at the object's true depth. Because they sit at their real depth, fog, mist and nearer geometry sort and
   occlude them correctly, and other camera poses (portrait P1) see a correct plane. These cards are valid only for
   vista framings. Anything the player runs past or through stays 3D.
2. **Painted Card shader** (`JungleBooze/Painted Card`). Opaque queue, depth write on, alpha-to-coverage with a
   sharpened coverage ramp. It does not alpha-blend: everything behind the card is depth-rejected, and with MSAA off
   (Low tier) it falls back to a plain alpha test. It is unlit with a partial fog share. An optional "water flow"
   mode slides only the bright, unsaturated pixels of the painting in two cross-faded phases, so the painted plunge
   moves while its cliff stays still.
3. **Impostors.** Far pillars are camera-facing painted cards from one atlas (`BD_F4f_PillarCards`, UV rects in
   config). Their feet sit in painted mist below the terrain line. Near pillars use the slab meshes (`RS_Pillar*_P`)
   with `FP_CanopyCrown_P` crowns.
4. **Painted FX.**
   - Water foam samples a 2x2 painted atlas (`FX_Foam_P`: lace, streaks, impact, contact) tiled in metres. It is
     gated by the existing vertex-R foam field and capped at `FoamMaxAlpha` (0.85). Local keyword `_PAINTED_FOAM`.
   - Short falls (up to 14 m) are painted sheets (`JungleBooze/Painted Sheet`, `FX_Cascade_P`). There is one quad
     strip per sheet, and wide falls are split so a painted row is never stretched more than about 1.3x. Only the
     body of the painting flows; the lip and the spray stay put. Short falls get no impact disc, which removed the
     opaque white areas.
5. **Keep the window clear.** 3D crowns whose tops would project into `ArchWindowRect` are not placed, so the painted
   fall and sky stay framed.
6. **Measurement.** `HeroBasinOverdraw` runs after every painterly capture. It models an A14 TBDR GPU:
   - opaque surfaces count only their visible pixels (HSR)
   - alpha-tested surfaces are counted against opaque depth (upper bound)
   - blended surfaces are counted against opaque plus clipped alpha-tested depth
   - the skybox counts once per uncovered pixel

   It prints fragments per pixel and an estimated cost per layer (per-shader ALU and texture estimates), and writes
   `F4_overdraw.png` / `P1_overdraw.png`. It uses a manual depth test in float targets, because the hardware depth
   buffer did not persist between command-buffer draws on the batch-mode Metal path. Device numbers still come from
   Xcode GPU captures.
7. **Tiers.** `HeroCards.LowTierDrops` names extra blended layers (god rays, mist band, third jungle wall). The
   builder tags those layers with `HeroTierContent`, which switches them off once at load on `DeviceTier.Low`. High
   is unchanged. Pipeline-level tier settings (render scale, MSAA, shadows) stay in ADR 0010's `QualityTiers`.

## Consequences
- F4 (v4): 44 draws and 236k tris (v3: 45 and 308k). The model estimates 2.5 ms of fragments plus about 2.6 ms of
  fixed passes. Blended overdraw is 1.28 layers per pixel.
- The arch is now a painting. Lighting changes (time of day) need a repaint or a second card. Its look depends on
  the card's exposure and fog values matching the 3D grade. They do not fully match yet: the card reads darker.
- New shaders add two small variants and one local keyword to Water. The look test and realistic style are untouched,
  because every hook is null or off unless the painterly config enables it.
