# Studio Status Board

**Read this first in every new session.** It's the single source of truth for where the project stands.
The coordinating session updates it whenever it launches an agent, receives an agent's report, or records an owner decision.
Decisions themselves live in `design/DECISIONS.md`; the plan lives in `docs/AGENT_PLAN.md`.

_Last updated: 2026-10-09 (AURELIA Phase 0: look test, local development)_

## Working setup (2026-10-09)
Local development on the owner's M4 Mac (see CLAUDE.md step 3). Branch `ccr-9e904458-5jxbus`. Cloud sessions are over;
the cloud resume watchdog `trig_0125FGkR542RNUbbCvMm3LWX` stays disabled (no delete in the tool; harmless).
Headless Unity: `tools/ci/unity.sh compile|test`. Old lane-based code, tests, audio, Run scene and `tools/sim` moved to
`archive/pre-aurelia/` (2026-10-09); the look test now uses a stand-in runner until the AURELIA movement core lands.
Baseline after the archive: all assemblies compile, EditMode 57/57 pass.

**Carried over from the 2026-10-08 handoff:** Pista cleanup in Blender (steps 01–05 redo, 06 bake is done:
`art_source/pista/clean/textures/`), export `pista_clean.glb`, previews + self-review, README; then Meshy rig +
animations (approved cap 150 credits). Then import rigged Pista into the LookTest scene, AI/CC0 environment props from
`design/aurelia/LOOK_TEST_BRIEF.md`, screenshots to the owner, owner opens it on Mac + iPhone (P0-E).

## Snapshot / handoff (2026-10-09 ~14:30 IST)
- **Owner: TRY Option D (painterly / stylized-realistic) on the hero basin first (2026-10-09); adopt only if the render convinces.** Look gate target is now `design/aurelia/keyframes/F4_f_painterly_openai.jpg`. Earlier context: hero basin v3 ~64% of the realistic F4 keyframe (sent). Coordinator's rethink: realism stalls on organic density (~70% ceiling with AI + procedural). Options sent to owner: **B** realistic + buy a few pro nature packs (~$50–150, owner must approve; shortlist exact packs first), **D** painterly/stylized-realistic at no cost (OpenAI style test: `design/aurelia/keyframes/F4_f_painterly_openai.jpg`; Pista would need a light shading restyle), A keep going, C freelance artist. Coordinator recommends B. Don't start new environment work until the owner picks.
- **Gameplay:** vertical slice parts A (9835543) and B (d58990d) committed; Expedition 1 fully playable in gray-box (bot 0 hits). Running: code-reviewer on the slice; asset-pipeline on Pista clips (Swim_Surface, Swim_Dive, Swim_Underwater, Swim_Leap, Vine_Release, Water_Wade, BalanceRun, Death fix, SlideEntry fix; cap 80 Meshy).
- **Next:** fix review findings; wire new clips into PistaExpedition prefab; creature/vine visuals; then apply the chosen art style to the slice.
- **Git LFS:** ~0.5 GB of GitHub's free 1 GB used; tell the owner before it runs out (~$5/mo per 50 GB).
- Unity process check: `pgrep -x Unity`.

## Current milestone
**LOOK GATE (owner, 2026-10-09):** no further scenes/environment content until the owner approves the final render of the waterfall-basin hero scene. Gameplay systems continue in gray-box.

**AURELIA, Phase 0: look test** (owner go, 2026-10-08). Source of truth for the direction: `design/aurelia/BLUEPRINT.md` and
`design/aurelia/vision_board.png`. Pista stays (Duko dropped 2026-10-08); store name "Aurelia: Wildward". Look: **realistic**, AI-made art plus free CC0 assets,
no paid packs. Quality bar: professional, polished in a small scope (owner wants "the greatest product").

**Phase plan (owner checks at the end of each phase; don't start the next until it passes):**
| Phase | Build | Owner check | Est. |
|---|---|---|---|
| **0 Look test** ← now | One realistic forest + waterfall stretch, realistic Pista running through it, on iPhone | Looks professional on the phone and runs smoothly | 1–2 wk |
| 1 Feel | Movement + camera tuned: a fun 60-second run | Is just running fun? | 2–3 wk |
| 2 Vertical slice | Blueprint Part LV: route choice, river/swim, vine, a creature, a secret, results, first upgrade | 5 testers say "let me try again" | 4–6 wk |
| 3 MVP | Full forest biome, 12–15 chunks, journal, 5–8 unlock abilities, light adaptive difficulty, audio | Owner wants to play daily | 3–4 mo |
| 4 Retention test | Missions, daily expedition, next-goal results; TestFlight 20–50 testers | D1 ≥ 40%, D7 ≥ 15% | 1–2 mo |
| 5 Launch | Store, compliance, performance | Ship | ~1 mo |

**Phase 0 tasks:**
- [x] P0-A art-director: realistic art direction for AURELIA (original visual language, clearly not Pandora), realistic Pista and Duko redesign briefs + prompt library. Images wait for API keys in this environment
- [x] P0-B tech-architect: look-test technical design (URP realistic-on-mobile settings, budgets, CC0 sourcing with licenses) + LookTest scene builder and asset fetch script
- [ ] P0-C realistic Pista concept images → owner picks. **Blocked: OpenAI account out of credits.** Run `tools/art/gen_concepts.py turnaround A|B|C`, then `keyart A|B|C`
- [x] P0-D 3D Pista via Meshy (needs `MESHY_API_KEY`), rig + run/jump/slide animations (phone video → AI motion capture)
- [ ] P0-E owner opens the LookTest scene on the Mac and on iPhone; judges look and smoothness

**Blocked:** API keys are not in this cloud environment (the old `~/.config/junglebooze/secrets.env` was on another machine).
Owner adds `OPENAI_API_KEY`, `MESHY_API_KEY`, `ELEVENLABS_API_KEY` in the environment settings; a new session picks them up.

**On hold:** old Batch 3/4 (missions, shop, worlds) and lane-based design work. The FP1 code stays and is reused where it fits.

**Earlier milestone (done):** FP1 compiled and played by the owner on 2026-10-07; Batch 1–2 (vines, menus/save, power-ups,
Duko/continue, audio files) compiled clean on the Mac.

## Agents: who is doing what

| Agent | State | Current / last task | Output | Next for this agent |
|---|---|---|---|---|
| producer | not used yet | (the coordinating session does this role for now) | this file | — |
| game-designer | done (2026-10-09) | Spec 103 vertical slice: scripted "Expedition 1", 13 chunks ~4:05, swim/vine/canopy/sailback/Veil Grotto secret/Shield/Deep Breath upgrade, AC-103-01–50 | `design/aurelia/GDD.md`, `design/aurelia/specs/` | Phase 2 chunk catalog detail; FTUE script |
| balance-simulator | done (A3) | Python reference model, golden traces, S1–S9 report (`archive/pre-aurelia/docs/sim-reports/2026-10-07-spec001.md`) | `tools/sim/`, `docs/sim-reports/` | Phase 1: new movement/chunk model from the AURELIA spec (old model kept as reference) |
| tech-architect | working (2026-10-09) | Painterly trial: stylized shading (rock/foliage/water/falls), grade to F4_f, Pista material restyle, style switch; → F4_compare_painterly.jpg | `Assets/_Game/Editor/Setup/`, `docs/adr/0003-first-playable-bootstrap.md`, `docs/PLAY_FIRST_BUILD.md` | Add EditMode tests for `ProjectSetupRules`; Roslyn analyzers |
| gameplay-engineer | done (2026-10-09) | Vertical slice part B (d58990d): swim, vine, canopy, sailback, Deep Breath, revive, validator V3/V7/V13/V14/W1–W5, analytics (on-device). EditMode 464/464, PlayMode 18/18 | `Scripts/Gameplay/PowerUps`, `Hazards`, `Companion`, `Views`, `App/RunSceneBootstrap.cs` | Wire audio events; then Batch 3 |
| ui-engineer | done (C2) | Track wired into the Run scene with gray-box views and HUD | `Scripts/Gameplay/Views`, `Scripts/App`, `Scripts/UI/Hud` | Fix errors the owner sends from Unity |
| art-director | working (2026-10-09) | ART_DIRECTION_PAINTERLY.md (painterly trial rules, palette from F4_f, Pista restyle, checklist) | `design/aurelia/ENVIRONMENT_STRATEGY.md` | Store art later |
| asset-pipeline | done (2026-10-09) | Iteration 2 assets (4e959d8): arch v2 14k tris 2×2k ~25 px/m, FP_ArchVines, travertine tiers (weakest; re-pass after seeing cascades), ledge v2, framing clusters, canopy. OpenAI $0.59 | `art_source/pista/`, `tools/blender/pista/` | Report → import rigged Pista into LookTest |
| audio-director | waiting | Old lane-era audio archived (2026-10-09) | — | AURELIA audio after the vertical slice (ElevenLabs needs owner OK) |
| qa-engineer | waiting | — | — | FP1 stage F: test plan + owner play-test script |
| performance-engineer | waiting | — | — | Benchmark scene (week 2+) |
| code-reviewer | working (2026-10-09) | Review of vertical slice A+B → docs/reviews/2026-10-09-vertical-slice.md | — | Rerun after the owner's review, if the owner wants it |
| monetization-engineer | waiting | — | — | Week 4–5 |
| appstore-compliance | done (2026-10-09) | AURELIA design review: no blocker; 16 ranked risks. High: name "AURELIA" alone is crowded (Kingdom of Aurelia, adult VN Aurelia); Unity `submitAnalytics` must be off for "Data Not Collected"; bundle id/product name still lane-era | `docs/compliance/2026-10-aurelia-design-review.md`, `docs/APP_STORE_CHECKLIST.md` | Privacy manifest when SDKs are chosen; follow-ups: "report an ad" option, Declared Age Range API, CI check for the word "booze" |
| release-engineer | paused (owner: build the game first) | Partial: build script, fastlane lanes, one-command build, setup guide (stopped mid-verification) | `fastlane/`, `tools/build/`, `docs/RELEASE.md`, `.github/workflows/build-ios.yml` | Resume later: finish and verify the Fastfile lanes |

States: **working** (launched, report not received) · waiting · blocked · done.
If a new session finds an agent marked **working** but no matching output or commit, assume that agent was interrupted. Check the listed output files and relaunch the task.

## Spend log
Meshy: 3 (image edit test) + 30 (Pista 3D v1) = **33 credits used**; balance 2,432 after the owner's upgrade.
**From 2026-10-09: 1,000 Meshy credits pre-approved (no per-step cap); ask the owner before going past 1,000 spent since 2026-10-09.** Prefer free retries. Spent since 2026-10-09: 355 (Pista 62, keyframes 93, hero kit 121, Pista clips 79). ~645 of the 1,000 left. OpenAI images since 2026-10-09: ~$0.75 of the $75 cap. OpenAI: 3 draft images (well under $1); account out of credit.

## Resume watchdog
- Routine `trig_0125FGkR542RNUbbCvMm3LWX` ("AURELIA resume watchdog") wakes session_01GJ9eWD47NbuMr3AXGv86j8 every 2 hours (minute 52 UTC). **Disabled 2026-10-08 (owner stopped the session).**
  It resumes stalled work after usage limits or interruptions; idle when only a pending budget answer blocks work.
- Old routines `trig_01KrbEoPcZMAR8K2x73kVcUn` and `trig_016rQLbb82z8R9q8csbCuZjN` deleted 2026-10-08.
- Stall started: _none_. After 3 days stalled, ask the owner and disable the routine until they answer.
- On session handoff: the new session creates its own watchdog, deletes this one, and updates the id above.

## Owner gates
| Gate | Status |
|---|---|
| G0 Accounts & setup | Partly done. Decided: builds on owner's Mac, iPhone 11 / SE 2 is the lowest device, Unity Personal for CI. **Owner to do:** add `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` GitHub secrets; open the project once in Unity 6 on the Mac; Apple Developer account; image/3D/audio API keys |
| G1 Creative brief | Done: wild jungle kid, macaw, pulpy adventure, all 4 worlds, macaw speaks a few words |
| G2 Art direction | Done: Inkbound Pulp, hero H2 Mapcloth take 1, macaw M3 Dusk original take 1 (violet/orange locked 2026-10-07) |
| G3 Feel check #1 | Not started (end of week 2, needs a TestFlight build) |
| G4–G8 | Not started |

## Open questions for the owner
- (2026-10-09) Store name options "Aurelia: <coined word>" to prepare (6–8 with collision checks) for the owner to pick. Bundle id: ask before the first TestFlight upload. To do (free): turn off Unity `submitAnalytics`.
0. API keys for image/3D/audio generation must be added to the cloud environment (see Phase 0 "Blocked").
1. Ads and prices: deferred to week 4 (tracking already decided: none).
2. Jungle music variation: three loops on disk (A/B/C). Playback is not wired yet; default to A until the owner listens.

## Assumptions waiting for owner review (`[ASSUMED]`)
- Rootstone tone: paler/warmer than ART_DIRECTION #A39079 to match the OpenAI F4 keyframe. Hero arch: detail normal first, split into two 2k texture sets (+1 draw) only if close-ups stay soft.
- Spec 103 (2026-10-09): Deep Breath is ability #1 (150 coins; opens the Sunken Arch), replacing Trail Sense in GDD §13; no drowning/breath meter; no revive offer in the first run; the slice compresses the Blueprint's 10-min first run to ~4 min. Full list in spec 103 §17.
- AURELIA (spec 101/GDD, 2026-10-09): controls "Steer + Flick" (drag steers 0.040 m/pt, flick = 2.2 m dodge, swipe up/down fire on threshold); speed 10→16 m/s; fixed jump 1.41 m / 0.60 s, coyote 100 ms, buffer 150 ms; frontal crash into a tall blocker ends the run; health +1 per 350 m undamaged; revive 1/2/4 crystals (max 3); rare resource "Crystals"; power-ups Magnet/Shield/Explorer Vision; 7 abilities; rating 9+; first 60 s can't die; analytics on-device (TestFlight upload only); DDA starts −0.3; MVP 14 chunks. Owner confirms controls at the Phase 1 check.
- Track: seam-fallback breather doesn't reset the breather timer; each breather gets its own pick; mover moves for 30 ticks after its trigger tick; random stream ids TrackGeneration=1…Cosmetic=5.
- Collisions: a second obstacle clipped on the same tick as a stumble is ignored; obstacles touched while invulnerable are ignored for that pass; a box already overlapping at tick start is a stumble, never a death; sliding under a standard high barrier counts as a near-miss.
- Possible feel change for the owner later: jump airtime 600 ms → 650 ms (same 1.5 m height) makes low barriers easier; the simulation shows average-player survival 87% → 90%. Not applied.
- Movement interpretations for the game-designer to confirm: pending inputs counted while pending; the swipe that cancels a queued lane move counts as Executed; 0 ms means "off" for coyote and run-start ramp.
- Placeholder app name "Jungle Runner" and bundle id `com.pistaduko.junglerunner`; minimum iOS 15.0; portrait, iPhone only; URP for FP1 with a built-in fallback menu.
- Stumble rule: clipping an obstacle's side is a stumble, the second stumble ends the run (spec 001). Judge at G3.
- Macaw cheer call-out word is "Shiny!" / "Wow!".
- Portrait only; daily calendar pauses instead of resetting; Assist ("Lift") by double tap; shield doesn't save from chasms.
- Coins are gold with a turquoise gem center so they read against gold scenery.
- Power-ups: coyote time counts as air so a Speed Boost does not end during it; obstacles touched during the boost slowdown are still smashed.
- Companion voice volume has no settings slider yet; spoken lines use SFX volume, and 0 switches Duko to squawks.

## Not yet verified
- Track code (B2): compile risks flagged by the agent: a property named `Track` inside namespace `JungleBooze.Gameplay.Track`; properties named like their types; `ref readonly` returns. Runner↔track integration never exercised. No `.asset` config files yet (code defaults are used).
- Overlap: `RunSession` (track) and `GameSession` (presentation) both implement the run lifecycle; C2 must pick one.
- Stage D bootstrap script: APIs checked by hand against Unity/URP source, not compiled. `-warnaserror+` is on, so any warning breaks the build.
- Style guide draw-call and triangle estimates (78/120 draws, ~137k/150k tris) need the benchmark scene.
- Image prompts were used (OpenAI `gpt-image-2`); macaw recolors exist but the locked 3D input is original take 1.
- Core, Services, Gameplay, UI, App, and Editor compiled clean on the owner's Mac with Unity 6000.3.25f1 csc and warnings as errors (2026-10-07). Test assemblies were not judged in that headless pass (nunit/mscorlib mismatch).
- AudioPlayback is not spawned in the Run scene yet, so Play is still silent.
- Unity editor version `6000.3.0f1` and package versions weren't checked against Unity's registry.
- Hand-made `.meta` files and asmdefs are unconfirmed until Unity opens the project.

## Log
Newest first. One line per event.
- 2026-10-09: Pista +11 clips (swim/dive/underwater/leap, vine release, wade, balance run, water entry/exit, Death_Stumble, Slide_Clean), 79 Meshy credits. Swim is breaststroke (no crawl in Meshy).
- 2026-10-09: Owner: try painterly (D) on the hero scene first. Launched art-director (rules), tech-architect (shading + grade), asset-pipeline #2 (hand-painted kit, caps OpenAI $10, Meshy 100). Gameplay-engineer fixing slice review; asset-pipeline #1 on Pista clips.
- 2026-10-09: Owner chose art style D: painterly / stylized-realistic. Launching art direction update, stylized shading, hand-painted re-texture of the hero kit.
- 2026-10-09: Hero basin v3 ~64% sent; style options B/D presented with an OpenAI painterly test. Vertical slice part B committed. Launched slice code review.
- 2026-10-09 13:31: Usage limit (reset 12:30) stopped all 3 agents (part B had EditMode 462/462; iteration 3 not yet implemented; swim clips mid-review). Resumed all three with their context.
- 2026-10-09: Hero basin iteration 2 committed (~60%; HDR capture fix). Iteration 2 assets committed. Launched iteration 3.
- 2026-10-09: Vertical slice part A committed (9835543). [ASSUMED] first-run 60 s: crash becomes a side-clip; RandomStreamIds.Discovery = 7. Launched part B.
- 2026-10-09: Hero basin v1 comparison sent to owner (~55%). Launched iteration 2 (grade/water/falls + arch/terraces/foreground assets).
- 2026-10-09: asset-pipeline done: hero-basin kit exported to Art/Environment (raw Meshy downloads git-ignored).
- 2026-10-09: Phase 1 review fixes committed (238fbe0). [ASSUMED] high obstacles = thin branch + see-through vine curtain (readability). Launched vertical slice part A (world structure); part B (swim/vine/canopy/creature/secret) follows.
- 2026-10-09: Owner: gameplay is fine (coordinator improves it autonomously); worried about look quality. Plan: one hero scene first (waterfall basin matching the OpenAI F4 keyframe), side-by-side comparison with honest per-layer gap, then other scenes with the same recipe. Redirected tech-architect and asset-pipeline to the hero scene.
- 2026-10-09: game-designer done: spec 103 vertical slice. [ASSUMED] Deep Breath first (recommended option A).
- 2026-10-09: Owner asked for an OpenAI image test: F4 keyframe with gpt-image-2 medium 1536x1024 succeeded and beats Meshy. OpenAI images approved (medium quality; working cap $75). Backdrops/leaf textures switch to OpenAI.
- 2026-10-09: Launched gameplay-engineer (review fixes), game-designer (spec 103 vertical slice), asset-pipeline (environment kit, cap 450 credits), tech-architect (water/mist/roots + integration). Sent owner gameplay videos, keyframes, look test v2.
- 2026-10-09: Owner picked store name "Aurelia: Wildward"; focus today = build the game, other work later. Reports in: real Pista + gameplay videos, Phase 1 review (1 blocker), keyframes, look test v2.
- 2026-10-09: Phase 1 Feel implemented and verified in Unity (dcc0721). Launched gameplay-engineer (real Pista + video), code-reviewer (Phase 1), art-director (Meshy keyframes, cap 100).
- 2026-10-09: Environment strategy revised to Meshy + CC0 + in-house: shopping list 831 credits (reserve 107); possible extra ask up to 600 credits only if a checkpoint shows a layer below the bar (near foliage most at risk). Launching keyframes (item 1, ~75 credits).
- 2026-10-09: Owner: no purchases for environment (Meshy + CC0 + in-house only); store name "Aurelia: <coined word>"; bundle id later. gameplay-engineer finished spec 101 code (113 logic tests pass outside Unity, bot clears course) but blocked from Unity by LookTest compile break; told tech-architect to restore compile.
- 2026-10-09: art-director done (environment strategy). Launched tech-architect on look test v2 graybox + light (free steps). Asked owner for environment budget, store name direction.
- 2026-10-09: appstore-compliance done: AURELIA design review (no blocker; name, analytics flag, bundle id are the high risks).
- 2026-10-09: tech-architect done (look test builds, iOS build OK, over draw budget, look not professional yet). asset-pipeline done (Pista rigged + animated, 62 credits). Sent owner Pista sheet, run cycle and a look-test screenshot.
- 2026-10-09: Launched gameplay-engineer on spec 101 (Phase 1 Feel).
- 2026-10-09: game-designer done: GDD + specs 101 (movement/camera) and 102 (chunks/routes/World Director). Needs ADR 0002 amendment (new per-tick input: commands + drag distance; replay version bump).
- 2026-10-09: Owner pre-approved 1,000 Meshy credits (no per-step cap; free retries first); beyond that, ask.
- 2026-10-09: Launched game-designer (GDD, specs 101/102), asset-pipeline (Pista finish + Meshy rig/anim, cap 150), tech-architect (look test build, screenshots, iOS build check). Local session resume watchdog: in-session hourly cron at :17 (expires after 7 days).
- 2026-10-09: **Moved to local development on the owner's Mac.** Owner mandate: professional, fully polished, App Store-safe AURELIA; autonomy except money; lean coordinator relying on subagents. Archived the old lane-based code/tests/audio/Run scene/tools/sim to `archive/pre-aurelia/`; look test decoupled (stand-in runner). Compile clean, EditMode 57/57.
- 2026-10-08: **Owner: stop the session.** Stopped asset-pipeline before it wrote anything (it was installing Blender); Pista task still to do exactly as in the Handoff note. Resume watchdog trig_0125FGkR542RNUbbCvMm3LWX disabled (not deleted); re-enable it when the owner restarts work.
- 2026-10-08: Owner OK'd cleanup of the old vision. Moved the old GDD, specs 001/002, sim report, Inkbound Pulp style guide, hero/macaw concept sheets and prompts, realistic Duko prompts and 2026-10-07 concepts to `archive/pre-aurelia/` (with SUPERSEDED banners and a README). Updated CLAUDE.md, AGENT_PLAN.md, 7 agent definitions and cross-links to AURELIA wording; Duko-dropped notes in ART_DIRECTION and meshy prompts. FP1 code and tools/sim kept for Phase 1 review (sim tests 64/64 pass).
- 2026-10-08: New session session_01GJ9eWD47NbuMr3AXGv86j8 took over (keys present). New watchdog trig_0125FGkR542RNUbbCvMm3LWX, old one deleted. Relaunched asset-pipeline: redo Blender steps 01–05 (intermediate files were lost), export pista_clean.glb, v2 previews + self-review, README; then Meshy rig + animations (cap 150 credits).
- 2026-10-08: Usage limit stopped both agents. Compile check verified by the coordinator: all assemblies and shaders PASS. Pista textures baked; export/rig/anim pending. Handing off to a fresh session.
- 2026-10-08: Owner confirmed Meshy was always on a paid plan: outputs owned, no attribution. Logged in LICENSES.md.
- 2026-10-08: Owner upgraded Meshy (2,432 credits) and approved wise use. Asset-pipeline may rig + animate Pista after cleanup, cap 150 credits.
- 2026-10-08: **Owner mandate: build AURELIA to completion autonomously, never move away from the blueprint, test constantly, auto-resume after limits, always ask before spending.** New watchdog trig_016rQLbb82z8R9q8csbCuZjN (every 2 h). Launching asset-pipeline (Pista Blender cleanup, free) and tech-architect (cloud compile check with Unity 6000.3.25f1 DLLs). Budget asks pending: Meshy rigging (~5–10 credits), animations (Meshy ~20–30 credits vs free phone mocap).
- 2026-10-08: Owner rule: always confirm budgets before spending. P0-B done (tech-architect): ADR 0004 (iPhone 12 provisional minimum, URP-Realistic), CC0 fetch tool (16 Poly Haven assets, 68.9 MB), LookTest scene builder/runtime/shaders/overlay, 2 EditMode test files; nothing compiled in Unity, shaders never compiled. Fixed a real compile error in RunAudioCues.cs (Stumble → Stumbled). ADR numbers shifted (save format now 0005). 3D Pista v1 from Meshy multi-image-to-3D (30 credits, balance 432): 28.9k tris, 4k PBR; previews rendered with Blender (bpy 4.2) in the cloud. Issues: rope still on, backpack map patch blurred, ponytail clumpy.
- 2026-10-08: Generated 3 Pista drafts (OpenAI, medium, 1536x1024). Owner said "pick one": take B simplified. High-quality final failed: OpenAI credits exhausted again after 3 images. One Meshy image-to-image try (3 credits) ignored the edit and dropped the back view; discarded. Meshy balance 462.
- 2026-10-08: Pista prompts reworked to the board explorer (age 16, covered torso, 3 takes). P0-C blocked: OpenAI account has no credits (HTTP 429 insufficient_quota); nothing generated or billed. Generator script saved to tools/art/gen_concepts.py. Asked owner: add OpenAI credit; Pista skin tone/face.
- 2026-10-08: Owner allowed installing tools in the cloud container. Told both running agents.
- 2026-10-08: Keys are in the environment. Art-director generating Pista concepts only (owner wants to see them before any next step). Owner dropped Duko the macaw (confirmed).
- 2026-10-08: Owner: Pista age 16, look based on the board explorer; orientation decided on the phone; sky no preference (no moon [ASSUMED]). Sent art-director back to rework Pista prompts.
- 2026-10-08: P0-A done (art-director): ART_DIRECTION, LOOK_TEST_BRIEF, prompt library (Pista/Duko 3 takes each, Meshy, environment). No images (no keys). Budgets above ARCHITECTURE.md passed to P0-B. Note: Meshy Free-plan output is CC BY 4.0; check the owner's plan before shipping assets. Owner asked: Pista age, cheek dots, orientation, sky.
- 2026-10-08: Owner: minimum device can rise to iPhone 12/13 for a polished look. Passed to both running agents.
- 2026-10-08: **Owner: go.** AURELIA with Pista/Duko, realistic, AI-made art, phased plan. Launched P0-A art-director and P0-B tech-architect.
- 2026-10-08: Owner: keep Pista, Duko and the game name for now; no plans locked, no paid assets, prefers AI + realistic look. Owner is still thinking about direction.
- 2026-10-08: Owner: replace Pista/Duko. Art fidelity and asset budget still open; owner asked for effort estimate, whether AI can reach good mobile quality, and whether to start a new repo.
- 2026-10-08: **Owner shared the AURELIA blueprint and vision board: "I want a polished game now, not a toy project."** Saved both to `design/aurelia/`, recorded the direction in DECISIONS. Could not compile in the cloud container (no Unity); last Mac compile check (Batch 2) was clean. Asked the owner about art fidelity, Pista/Duko and asset budget before rewriting the GDD.
- 2026-10-07: **Owner: finish up; use original macaw (take 1 violet/orange).** Recolors color1–4 are on disk unused. Audio files generated (SFX regenerated; Duko voice recovered from ElevenLabs history; 3 Jungle loops + menu + sting). Playback code exists, not wired into the Run scene. Updating STATUS, committing Batch 2 (power-ups, Duko/continue, audio, concepts), pushing.
- 2026-10-07: Power-ups compile-check clean (Core, Services, Gameplay, UI, App, Editor; warnings as errors). Test assemblies were not judged: the headless nunit reference wants mscorlib, which is a compiler-setup mismatch, not a game-code error. [ASSUMED] coyote counts as air; slowdown still smashes obstacles. Macaw recolors and audio still running. Not committed yet.
- 2026-10-07: Power-ups agent finished. Views spawned in RunSceneBootstrap; Speed Boost holds its ending and vine sections until the boost is over. RunSceneBootstrapTests view count was already stale (expects 4).
- 2026-10-07: Macaw recolor agent hit a resource error before any image. Relaunched. Power-ups relaunch still running. Audio still running.
- 2026-10-07: Power-ups agent hit a resource error before any edit. Relaunched the same task. Audio and macaw recolors still running.
- 2026-10-07: **Owner: resume.** Checked disk. Duko + Continue is in the tree (companion, continue UI, bootstrap). Power-ups/hazards code exists (`PowerUpSystem`, lane strikes, `PowerUpView`, `HazardView`) but those two views are not spawned in `RunSceneBootstrap`. Audio folder is only `.gitkeep`; the generated SFX are not on disk (credits already spent). No `macaw_take1_color*` files. Launching gameplay-engineer (finish and wire power-ups), audio-director (recover SFX from ElevenLabs history if possible, then voice, music, playback), art-director (4 macaw recolors).
- 2026-10-07: **Owner: "stop all".** Stopped all agents. State: Duko + Continue FINISHED (compiled clean, uncommitted). Power-ups + hazards STOPPED MID-EDIT in RunnerSimulation (incomplete; may not compile). Audio STOPPED: SFX generated (755 credits), Duko voice and music not done, AudioService unknown. Macaw recolors STOPPED (check design/concepts/2026-10-07 for macaw_take1_color*). Some WIP got into status commits 73c4619/ae0cef7 (used commit -a; from now on commit status with an explicit path). Nothing new launches until the owner says so. On resume: finish power-ups (agent was re-reading RunnerSimulation), finish audio (voice, music, AudioService, wiring), finish macaw recolors, then compile-check and commit.
- 2026-10-07: Concept art done (12 of 30 generations). Owner picked hero take 1; macaw take 1 shape but new colors: art-director making 4 recolors. (asset-pipeline was NOT launched.)
- 2026-10-07: Owner added Meshy (1,100 credits) and ElevenLabs (Starter, 90k chars) keys to ~/.config/junglebooze/secrets.env; both verified. Launched audio-director (SFX, Duko voice, music, AudioService; wiring after batch 2). Meshy waits for the owner's concept pick.
- 2026-10-07: Launched batch 2: gameplay-engineer (power-ups + signature hazards), gameplay-engineer (Duko companion + continue flow), art-director (hero/macaw concept sheets via OpenAI images, max 30 generations).
- 2026-10-07: Vine swinging done (6 vine sections, grab/swing/release grades, chains, chasm 'Missed vine' death, gray-box views, camera). Coordinator compiled all 8 assemblies together with Unity's compiler, warnings as errors: clean. Not play-tested. Vine [ASSUMED]: launch physics, 18 m chasms (B3 validator must allow), chain rules, section lengths. TrackRunSetup.CreateDefault now includes vines, so seeds give different tracks than before.
- 2026-10-07: Owner provided an OpenAI API key for image generation. Stored outside the repo in ~/.config/junglebooze/secrets.env (source it). Image models available. Owner should rotate it later since it was pasted in chat.
- 2026-10-07: Menus/save agent done: main menu, pause, Game Over, Settings, local JSON save (wallet, best, settings). Compiled clean with Unity's compiler; not play-tested. 8 [ASSUMED] items in its report (Play starts immediately; coins kept when quitting from pause).
- 2026-10-07: Owner compiled and played FP1. Owner decision: build features first; review and tests come later. Launched batch 1 (vine swinging, menus/save).
- 2026-10-07: Stage C2 done. First playable (track, obstacles, ravines, coins, HUD) is written; waiting for the owner to open it in Unity and report errors.
- 2026-10-07: Owner declined compile-review agents ("just write code"). Launched only C2.
- 2026-10-07: Owner lifted the pause: "give me a playable version, just focus on that". Launching C2 + two compile-fix passes.
- 2026-10-07: Stage B2 done (track code, no new tests). All agents finished. Project is paused for the owner's code review.
- 2026-10-07: Owner deferred tests; track agent redirected from writing tests to verifying code consistency and .meta files.
- 2026-10-07: Owner stopped the code-reviewer to save tokens. Track agent (B2) continues to finish its tests.
- 2026-10-07: Stage B1 done (gameplay-engineer): collisions + tests. Not compiled.
- 2026-10-07: Owner asked to review the code before any more changes. Running agents finish; nothing new is launched; resume watchdog disabled.
- 2026-10-07: Stage C1 done (ui-engineer). Launched code-reviewer on the presentation slice for the FP0 preview. Known issue: Track/ files lack .meta files (B2 still running).
- 2026-10-07: Stage A3 done (balance-simulator): reference model, 64 unit tests green, targets report with S4/S5 failing on bot profiles.
- 2026-10-07: Owner wants to play progress ASAP. Added early preview FP0 (flat world) right after C1.
- 2026-10-07 06:30 UTC: Usage limit hit ~01:10 and stopped all four agents; checkpoints had saved work up to 01:05. Resumed B1, B2, C1 and A3 from their saved files after the limit reset.
- 2026-10-07: Launched B1 (collisions) and B2 (track, coins, score, run lifecycle) in parallel.
- 2026-10-07: Stage A2 done (game-designer): spec 002. Launching B1 (collisions) and B2 (track) in parallel.
- 2026-10-07: Launched stage C1 (ui-engineer) in parallel with A2/A3, since it only needs the movement simulation.
- 2026-10-07: Stage A1 done (gameplay-engineer): movement simulation + tests, not compiled.
- 2026-10-07: Stage D done (tech-architect): bootstrap script, ADR 0003, Mac play guide.
- 2026-10-07: Launched FP1 stages A1, A2, A3 and D in parallel. Background checkpoint commits every 10 minutes.
- 2026-10-07: Worker restart wiped uncommitted work; gameplay-engineer, game-designer (spec 002), balance-simulator and qa-engineer were interrupted with nothing saved. Owner set the autonomous First Playable mandate. Restarting in smaller stages.
- 2026-10-06: Owner set focus on building and testing. Stopped release-engineer (work so far committed). Launched balance-simulator (reference model) and qa-engineer (test plan).
- 2026-10-06: Owner decided: app name candidate "Pista & Duko: Jungle Swing", lawyer check in week 7, no tracking, add the App assembly.
- 2026-10-06: appstore-compliance finished the name check and early risk review. Top risks: "JungleBooze" leaking into bundle/product IDs (permanent), and kid hero + ads counting as directed to children.
- 2026-10-06: Owner approved self-resume after usage limits for up to 3 days. Created the resume watchdog routine.
- 2026-10-06: Owner set the automatic handoff threshold at about 40% of the conversation's capacity.
- 2026-10-06: art-director finished the names/sash update to the style guide and prompts.
- 2026-10-06: Launched game-designer (spec 002), appstore-compliance (names + early review), release-engineer (iOS/TestFlight pipeline), art-director (style guide update). gameplay-engineer still running with no files written yet.
- 2026-10-06: Owner approved automatic session handoff (see project rules, "Starting a session" step 3).
- 2026-10-06: game-designer finished GDD fourth draft (all decisions applied; Mountains art moves up to week 3).
- 2026-10-06: Quality-over-schedule added as project rule 10. Launched game-designer to apply names, sash, English-only, no-lighter-world rule, bloom and macaw placement to the GDD.
- 2026-10-06: Owner decided: names Pista/Duko (trademark check pending), sash band below the X, English-only launch, and quality over schedule (launch waits rather than shipping a lighter world).
- 2026-10-06: art-director finished the binding style guide and final hero/macaw prompts; proposed 5 name pairs.
- 2026-10-06: game-designer finished the GDD sync (third draft). Art-director files committed mid-task.
- 2026-10-06: Launched gameplay-engineer (spec 001) and game-designer (GDD sync). Created this status board.
- 2026-10-06: game-designer finished spec 001 (65 acceptance criteria) and aligned the GDD.
- 2026-10-06: Owner decided G2: Inkbound Pulp, Mapcloth, Dusk. Launched art-director (style guide) and game-designer (spec 001).
- 2026-10-06: Owner decided build setup, lowest device, Unity license, talking macaw.
- 2026-10-06: tech-architect finished week 0. art-director delivered 3 style boards and hero/macaw concepts.
- 2026-10-06: Owner decided G1. game-designer wrote the first GDD draft.
- 2026-10-06: Agent team and plan created.
