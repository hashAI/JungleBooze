# Studio Status Board

**Read this first in every new session.** It's the single source of truth for where the project stands.
The coordinating session updates it whenever it launches an agent, receives an agent's report, or records an owner decision.
Decisions themselves live in `design/DECISIONS.md`; the plan lives in `docs/AGENT_PLAN.md`.

_Last updated: 2026-10-07 (build phase)_

## Current milestone
**FP1 COMPILED AND PLAYED by the owner (2026-10-07).** New phase: **BUILD THE GAME** (owner: "only focus on building first; review, test etc. later").
Agents implement features straight from the GDD sections (no separate spec docs, no new tests, no code-review passes for now).
The coordinating session compile-checks in Unity batch mode on the owner's Mac (Unity 6000.3.25f1 is installed) after each batch and fixes errors before committing.

**Build plan (tick as they land):**
- [x] Batch 1 (parallel): gameplay-engineer vine swinging (GDD §7) · ui-engineer main menu, pause, game over, settings, save of coins/best (GDD §19, §13.1)
- [x] Batch 2: power-ups and lane-strike hazards (GDD §10, §8.3) · Duko companion + continue (GDD §15, §14.4) · audio files + playback code (not yet hooked into the Run scene)
- [x] Batch 3a: missions, daily reward, shop/unlocks (GDD §13), uncompiled
- [x] Batch 3b: onboarding (GDD §12), uncompiled
- [x] Batch 4: worlds and transitions (GDD §9) · difficulty ramp check (GDD §11), uncompiled
- [ ] Later: 3D from locked concepts, wire audio into the run, ads/IAP, review, tests, TestFlight.

**PAUSED by owner (2026-10-07) after Batches 3 and 4. No agents running.** Resume note:
1. Pull on the Mac, open in Unity, send the compile errors (a lot of code is unseen by a compiler: audio wiring, meta menus, shop, tutorial, worlds, art plumbing). Fix those first.
2. Art: cloud has no keys/Blender/Meshy access. Either run `tools/assetgen/meshy_characters.py` and `gen_env_assets.py` on the Mac (caps: 250 / 200 Meshy credits, 4 images), or start a new session after adding MESHY_API_KEY/OPENAI_API_KEY to the environment and allowing api.meshy.ai / api.openai.com.
3. Stale PlayMode tests (view count now ~18; fresh save starts the tutorial). Tests were deferred by the owner.
4. Then: fairness validator (B3) on the new T4 chunks, wire world banner/music crossfade to `WorldThemeView.SegmentChanged`, review/tests pass.
The 8 files in design/concepts/2026-10-07 show as modified in the cloud container only because of a git LFS stub problem: never commit them from the container. Mesh generation waits on Meshy after this commit. If interrupted: compile-check with Unity 6000.3.25f1 rsp + bundled csc, then commit.

**Goal: First Playable (FP1). Autonomous mandate: no owner questions until it's done.** Release/store work is paused.

**FP1 is done when** the owner, on their M4 MacBook, can:
1. Follow `docs/PLAY_FIRST_BUILD.md`: install Unity Hub + Unity 6 LTS (with iOS Build Support), open `UnityProject/`.
2. Press Play and run an endless gray-box track (primitives in the style-guide palette): Pista (stand-in shape) switches
   3 lanes, jumps, slides; obstacles of the shared kit; coins; speed ramps up; collisions end the run (stumble rule).
3. Control with arrow keys / WASD / space in the editor, mouse-drag swipes, and touch swipes on iPhone.
4. See a HUD (distance, coins) and a Game Over panel with Restart.
5. Run the EditMode tests in Unity's Test Runner and see them pass.
6. Optionally build to their iPhone via Xcode with a free Apple ID (steps in the same doc).
Not in FP1 (comes right after): vine swinging, Duko, power-ups, menus/shop, real art, audio.

**Notes for stage C:** the Run scene is empty, so the runtime bootstrap creates the portrait camera and a directional light and only acts in the scene named "Run". Don't use `Shader.Find`; clone the material from `GameObject.CreatePrimitive` and set `.color`. Prefer `InputSystemUIInputModule`. Warnings are errors. Replace the placeholder controls table in `docs/PLAY_FIRST_BUILD.md`.

**Early preview FP0 (owner asked to play progress ASAP):** as soon as C1 is done, a focused compile-correctness review of
the flat-world slice (Core, Runner, Session, Views, Controls, App, UI/Hud) and fixes, then tell the owner to pull and press Play.
FP0 = flat ground, lanes, jump, slide, pause, HUD, Game Over/restart. Obstacles and coins arrive with FP1. The owner's real
compiler on the Mac is the fastest way to find remaining errors; fix whatever they send back first.

**FP1 plan (stages; tick as they land):**
- [x] A1 gameplay-engineer: movement simulation (spec 001 minus collisions) + EditMode tests (AC-01–14, 16–33, 45–46, 61–63; collisions deferred to B)
- [x] A2 game-designer: spec 002 (track, obstacles, coins): 16 Jungle chunks, generator, coins/score, fairness rules, run lifecycle, AC-201–250
- [x] A3 balance-simulator: Python reference model (64 tests pass), 6 golden traces, targets report: S1–S3, S6–S8 pass; S4/S5 fail (bot mistake profiles, low-barrier jump is the tightest timing)
- [x] B1 gameplay-engineer: collisions (swept AABB, lethal vs stumble, daze, edge forgiveness, near-miss, step hooks) + tests for AC-34–45
- [x] B2 gameplay-engineer: track simulation (generator, TrackSimulation as ITrackQuery, coin layout, scoring, TrackRunWorld/Factory, RunSession lifecycle, config assets, .meta files). Tests deferred by owner; 4 track test files written but never run
- [ ] B3 gameplay-engineer: chunk fairness validator (spec 002 §11) as an EditMode test + editor menu
- [x] C1 ui-engineer: presentation (bootstrap, session with Ready/Dying/GameOver, Run again/Same track, input, gray-box views, HUD, PlayMode tests). Swap point for C2: `RunSceneBootstrap.CreateWorldFactory`.
- [x] C2 ui-engineer: track world wired in; obstacle, ravine and coin views; score HUD; stumble/near-miss feedback. GameSession stays the lifecycle (RunSession unused)
- [x] D tech-architect: first-open bootstrap (`Assets/_Game/Editor/Setup/ProjectBootstrap.cs`, ADR 0003) + `docs/PLAY_FIRST_BUILD.md`
- [ ] E code-reviewer: compile-correctness and logic review of everything; fixes applied (an early run was stopped by the owner to save tokens)
- [ ] F qa-engineer: test plan + owner play-test script

Lesson from the 2026-10-07 worker restart: uncommitted agent work is lost on restart. Keep agent tasks small,
and commit after every agent report.

## Agents: who is doing what

| Agent | State | Current / last task | Output | Next for this agent |
|---|---|---|---|---|
| producer | not used yet | (the coordinating session does this role for now) | this file | — |
| game-designer | done (stage A2) | Spec 002: track, obstacles, coins, generator, fairness, lifecycle | `docs/specs/002-track-obstacles-coins.md` | Apply spec 002's listed changes to spec 001 and the GDD; spec 003 vine swinging |
| balance-simulator | done (A3) | Python reference model, golden traces, S1–S9 report (`docs/sim-reports/2026-10-07-spec001.md`) | `tools/sim/`, `docs/sim-reports/` | Fairness fuzzing for spec 002; recommendations R1–R6 go to game-designer |
| tech-architect | done (stage D) | First-open bootstrap: URP mobile asset, input set to Both, iOS basics (placeholder bundle id `com.pistaduko.junglerunner`, iOS 15, portrait), empty Run scene in build list; Mac play guide | `Assets/_Game/Editor/Setup/`, `docs/adr/0003-first-playable-bootstrap.md`, `docs/PLAY_FIRST_BUILD.md` | Add EditMode tests for `ProjectSetupRules`; Roslyn analyzers |
| gameplay-engineer | done (batch 2) | Power-ups, lane strikes, Duko, continue; compile-checked | `Scripts/Gameplay/PowerUps`, `Hazards`, `Companion`, `Views`, `App/RunSceneBootstrap.cs` | Batch 3 |
| ui-engineer | done (C2) | Track wired into the Run scene with gray-box views and HUD | `Scripts/Gameplay/Views`, `Scripts/App`, `Scripts/UI/Hud` | Fix errors the owner sends from Unity |
| art-director | done (concepts) | Hero take 1 and macaw take 1 locked; four unused recolors on disk | `design/concepts/2026-10-07/` | Store art later |
| asset-pipeline | done (look pass) | Ground trail + jungle floor textures, baked jungle wall segments (Blender kit), sky dome, trilight ambient, `EnvironmentLookConfig`; Blender mock before/after in `Art/Environment/Previews`; Meshy 120 credits spent (465 left) | `Art/Environment/`, `GroundView`, `SkyView`, `tools/blender/` | Owner compiles and looks; fix errors |
| audio-director | done (files) | SFX, Duko voice (from history), Jungle/menu music, `AudioPlayback` | `Assets/_Game/Audio`, `Scripts/Services/Audio` | Done in code (uncompiled); owner to listen |
| qa-engineer | waiting | — | — | FP1 stage F: test plan + owner play-test script |
| performance-engineer | waiting | — | — | Benchmark scene (week 2+) |
| code-reviewer | stopped by owner (token cost) | FP0 compile review was stopped before reporting; nothing written | — | Rerun after the owner's review, if the owner wants it |
| monetization-engineer | waiting | — | — | Week 4–5 |
| appstore-compliance | done | Name check (Pista: low caution, Duko: clear; not legal clearance), 8 ranked app names, 17 early risks, checklist refreshed for current Apple rules (Xcode 26 / iOS 26 SDK) | `docs/compliance/2026-10-name-and-early-review.md`, `docs/APP_STORE_CHECKLIST.md` | Privacy manifest when SDKs are chosen; follow-ups: "report an ad" option, Declared Age Range API, CI check for the word "booze" |
| release-engineer | paused (owner: build the game first) | Partial: build script, fastlane lanes, one-command build, setup guide (stopped mid-verification) | `fastlane/`, `tools/build/`, `docs/RELEASE.md`, `.github/workflows/build-ios.yml` | Resume later: finish and verify the Fastfile lanes |

States: **working** (launched, report not received) · waiting · blocked · done.
If a new session finds an agent marked **working** but no matching output or commit, assume that agent was interrupted. Check the listed output files and relaunch the task.

## Resume watchdog
- **Disabled 2026-10-07 for the owner review pause.** Re-enable when the owner says to continue.
- Routine `trig_01KrbEoPcZMAR8K2x73kVcUn` wakes the coordinating session every 4 hours (minute 23 UTC).
- It resumes stalled work (usage limit, interruption). It does nothing while agents run or while waiting on the owner.
- Stall started: _none_ (set this to the date/time of the first watchdog check that finds no progress; clear it when work resumes).
- After 3 days stalled, it stops, asks the owner whether to continue, and disables itself until they answer.
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
1. Ads and prices: deferred to week 4 (tracking already decided: none).
2. Jungle music variation: three loops on disk (A/B/C). Playback is not wired yet; default to A until the owner listens.

## Assumptions waiting for owner review (`[ASSUMED]`)
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
- Audio wiring (`RunAudioView`, `AudioPlayback.SetCatalog`, fixed `Stumble`→`Stumbled` in `RunAudioCues`) was written in the cloud and is not compiled yet.
- Unity editor version `6000.3.0f1` and package versions weren't checked against Unity's registry.
- Hand-made `.meta` files and asmdefs are unconfirmed until Unity opens the project.

## Log
Newest first. One line per event.
- 2026-10-07: Spec 003 T8 scenery (ui-engineer, uncompiled, uncommitted): stateless `ScenerySystem` (Views/Scenery, one IRunView, no GameObjects per prop: placed per 10 m cell by hash of run seed + Scenery stream + cell + band, outside the sight corridor 5 m / 7 m inner bend / 9 m swing zone, oriented through PathFrame, near/mid/overhead bands, per-beat/world/layer density in `SceneryCellProfile`, LOD by cell distance 40 m, hard caps 60k tris / 256 pieces / 4 shafts via `SceneryBudget`, drawn with `Graphics.RenderMeshInstanced`, about 25 draws). Art slots Foliage_TreeA/TreeB, Tree_Trunk, Foliage_Bush, Foliage_FernCluster, Prop_Rock, Prop_Root, Vine_Liana with generated gray-box fallbacks; light shafts generated. Default ON; replaces GroundView's wall dressing (`GroundView.SkipVergeDressing`); switch = PlayerPrefs `JungleBooze.Scenery` 0. Tests `SceneryPlacerTests` (AC-308/309/327 pure math). [ASSUMED] native model sizes/footprints (TreeA/B 2.2 m reach, Tree_Trunk 1.5 m radius 34 m tall), world density 1/0.85/0.55/0.7, shafts every 3rd cell. T5 canopy hook = `SceneryCellProfile.For` High layer case.
- 2026-10-07: Spec 003 T5 (gameplay-engineer): canopy layers. `RouteGenerator` schedules Ascent (Floor, root ramp) -> High run (Bough surface) -> Descent (High, mirrors the Ascent length and gives back the height) in Jungle/River, tuning in `RouteTuning` (first not before calm start + 400 m, start-to-start 500 to 800 m, High run 150 to 400 m, clear of gateways 250 m after / 60 m before, vine in High at least 30 m after the Ascent), validator allows 12 % on Ascent/Descent; new `BoughView` (limb + leaf-rim pieces, forest floor 24 m below, light shafts, gap cut ends) and `GroundView.SetPathSkinHidden`; tests `RouteLayerTests` (AC-325/326). Uncompiled, uncommitted. [ASSUMED] gateway distances mirrored in `RouteTuning` (checked by a test against `WorldScheduleConfig`).
- 2026-10-07: UX polish batch (gameplay-engineer, uncompiled, uncommitted): world title banner (`WorldBannerView`, 2 s, top third, fade-only with Reduce Motion), per-world music crossfade (two pooled sources, 2 s, bed positions remembered per run, Continue keeps the bed), Duko idle chatter every 18-35 s of calm running (`DukoChatterView`), dev start distance F5 +300 m / F6 +1000 m (+ menu buttons; fast-forward via a Speed Boost dash, simulation unchanged), dev log of the route's first 8 beats. [ASSUMED] beds: Jungle A, River B, Mountains C, Ruins A, Dusk B; chatter silent (not squawks) at voice volume 0.
- 2026-10-07: **Device build works on iPhone** (pink screen fixed: procedural primitives, Resources URP materials). Roadmap written (docs/ROADMAP.md). Launched Phase A: T5 canopy layers, T8 scenery placer, UX polish (world banner, music crossfade, Duko chatter, dev start-distance).
- 2026-10-07: iPhone pink-screen fix (gameplay-engineer, uncompiled, uncommitted): device log showed `CreatePrimitive` failing (physics module stripped) and primitive-cloned materials on the Standard shader. Added `PrimitiveMeshes` (procedural meshes, no colliders), `RuntimeMaterialTemplates` + hand-written `Resources/RuntimeMaterials/GrayBoxLit.mat` and `GrayBoxTransparent.mat`, editor `RuntimeMaterialSetup` (menu JungleBooze > Setup > Create Runtime Materials), a dev-build shader report log, fog variants kept in Graphics settings, off-screen prewarm skipped on device [ASSUMED]. See PLAY_FIRST_BUILD.md.
- 2026-10-07: Winding route made visible (gameplay-engineer): default mode is now the generated route in all builds [ASSUMED]; F4 (or four-finger tap, dev builds) cycles Generated, Debug curve, Straight for the next run, saved in PlayerPrefs `JungleBooze.RouteMode`; dev-only bottom-left label (`DevRouteLabelView`) shows the mode, `(next run)` when pending, and F3 hint; `SelectableRouteSource` now holds all three sources; old `UseGeneratedRoutePrefKey` removed (menu uses the new setting); docs/PLAY_FIRST_BUILD.md section added. Uncompiled, uncommitted.
- 2026-10-07: Spec 003 T4 (ui-engineer): camera rewrite. Plain-C# `CameraRouteModel` (smoothing, yaw lead + rate caps, bank roll, pitch follow, bend/boost FOV, swing camera, Reduce Motion, screen-edge safety clamp), `CameraRouteRig` (samples the PathFrame), `CameraRouteTuning` (plain class, spec values), `FollowCameraView` is now a thin shell; EditMode tests `CameraRouteTests` for AC-305/307/314/315/316 plus a straight-route identity check. Uncompiled, not run, uncommitted. Straight route is unchanged (safety clamp gated off by curvature). [ASSUMED] open side = away from the vine's lane until T6 anchor trees; safety clamp may add up to 20 deg yaw / 8 deg FOV on tight bends (outer lane); no tuning .asset (plain class, spec 14.1 allowed).
- 2026-10-07: Spec 003 T6 (ui-engineer): natural swing rig in `VineView` + pure `SwingRigMath` (gray-box, uncompiled, uncommitted): overhead span at 8.64 m in 4 route-following pieces, hidden leafy knot at the rope top (rest and during the swing), rope sway +-5 deg fading to 0.5 deg in the last 30 m, stateless anchor tree 6-9 m beside the path with a limb over the lane (seeded from run seed + Scenery stream id, chunk serial, row), gold tuft + orange flower at the grab point, other vines dimmed 25 percent, landing glade cushion + bushes after the last vine of a section; art slots `Tree_Trunk`/`Tree_Branch`/`Vine_Liana`/`Vine_Tuft` via EnvironmentArt (gray-box fallback). AC-318..320 tests in `SwingRigMathTests` (AC-321 needs the replay harness, not written). [ASSUMED] max spawn speed 21 m/s for span length; glade starts 15 m after the last grab. Check on the Mac with F4 (curved route).
- 2026-10-07: Spec 003 T2 (gameplay-engineer): `RouteGenerator` (seeded beats, jerk-limited curvature and pitch followers, vine/gateway zones with emergency ease), per-world `RouteTuning`, `RouteValidator` + `ViewportProbe` + `RouteTrackSimulator`, `TrackSimulation.PeekChunk`/`CommittedAheadM`, EditMode tests for AC-303/304/306/312/313, menu `JungleBooze > Route`. Uncompiled, uncommitted. Track defaults changed: generateAheadM 150 to 190, maxActiveChunks 8 to 12, maxActiveCoins 256 to 320 (chunk sequence unchanged, test added). Route default stays straight (`UseGeneratedRoute` off). [ASSUMED] stream ids 17/18, route built to hero+115 not +200, calm start 250 m, Gateway counts as a breath, Ascent/Descent weight 0 until T5.
- 2026-10-07: Spec 003 T3 (ui-engineer): every view now places itself through PathFrame (new `PathPlacement` helper; ground ribbons built along the route, per-segment walls/foliage/tiles, objects oriented by RotationAt, camera mapped through the route, key light yaw follows the route heading), plus `DebugRouteSource` (yaw +-25 deg/300 m, slope 5%, R >= 109 m) and `SelectableRouteSource`; default stays straight (identity). F4 in the editor/dev builds toggles the curved debug route for the NEXT run. Uncompiled, uncommitted. [ASSUMED] SkyView unchanged (already camera-centred, symmetric); gray-box tiles are rigid 12 m pieces; camera has no yaw lead/bank yet (T4).
- 2026-10-07: Spec 003 T1 (gameplay-engineer): PathFrame + straight route (identity), RouteTuning, PathFrameRunView in the bootstrap, AC-301/302 EditMode tests. Uncompiled, uncommitted. Stream ids Route/Scenery are 17/18 because 6 is VineSchedule [ASSUMED].
- 2026-10-07: **Review MUST FIX 1-5, SHOULD FIX 6/7/8/10/11 and smoothness quick wins applied (gameplay-engineer), all uncompiled, uncommitted.** Tutorial rescue now happens before events are dispatched (Died dropped, session best undone); runner event buffer takes the track's 512; Reduce Motion is one live switch from the save (vine slow-mo, camera tilt/shake/FOV, shards, speed lines), Haptics row hidden; rolling swipe window; run coins and best banked on backgrounding (exactly once); current-run coins count toward a Continue; ObstacleView slots keyed by obstacle id; boulder art scaled up to its box (visual only); logs gated; Game Over save one frame late; real delta clamped to 0.1 s and snapped to 1/60 within 1.5 ms; GPU skinning on, intermediate texture Auto, shadows off in the URP asset; font and pooled-view prewarm (`RunPrewarm`); animation weights snapped to 0. Not done: coin instancing and the boulder hitbox (owner decides), audit #9/#10/#11/#13-15. [ASSUMED] items: current-run coins count for Continue and are banked into the wallet when paid; one Reduce Motion switch also covering shards/speed lines; Haptics hidden (no native plugin); rolling swipe window re-bases when it expires; a touch over UI is handled by resetting input on every HudFactory button press; boulder art fit capped at 2.5x per axis. **To verify on the Mac:** compile with warnings as errors, then the list in `docs/PLAY_FIRST_BUILD.md` ("Verify on the Mac after the review and smoothness pass").
- 2026-10-07: **Owner rule: Meshy credits are scarce. Budget policy:** no Meshy call without a per-task cap stated in the prompt and logged in tools/assetgen/*spend_log; prefer procedural Blender/bpy and recolors/re-use over Meshy; one preview+refine pair (~15 credits) per new asset, no re-rolls without approval; show a contact sheet before spending on variants; agents report credits used and remaining. Always re-read the balance before and after.
- 2026-10-07: **Owner vision change: jungle-crossing feel (Tarzan/Mowgli), approach A (path-space, curved presentation).** Logged in DECISIONS.md. Launched: game-designer (jungle-crossing spec), gameplay-engineer (invisible-obstacle crash bug), code-reviewer (first full review, report only), performance-engineer (smoothness audit, report only), art-director (jungle tree/vine art plan). Implementation of the curved path and swings follows the spec.
- 2026-10-07: Look pass done (asset-pipeline), uncompiled, checked only with Blender mocks (look_before/after/after_dusk). Textured trail, 12 m jungle wall segments (~58k env tris, ~20 env draws), sky dome, trilight ambient, blob shadows baked in (no shadow maps), look config asset. [ASSUMED] wear-track lane cues (no dashes); canopy frames top corners only; golden-orange jungle sky; jungle walls in every world for now. Risks: sky relies on UI/Default at queue 1000 in URP; wall FBX orientation derived not observed. Open owner questions: lane cues, canopy closure, sky color, per-world foliage (30 Meshy credits left of cap).
- 2026-10-07: Owner: in-game 3D art looks wrong (trees lying on the ground) and should look amazing. Cause: env FBXs kept Blender's -90 X axis rotation on the root, and `EnvironmentArt.Attach` resets root rotation. Fixed (5d9395c): all 17 FBXs re-exported with baked axes, load-time warning added; checked in a Blender mock of the run camera. Launched asset-pipeline for a full look pass (path, jungle walls, sky, lighting).
- 2026-10-07: **Integration pass done (all uncompiled).** Meshy art: Pista (7,859 tris, rigged, 5 clips, X painted on), Duko (3,879 tris, 7-bone rig), 17 env props (FBX). Env prefabs auto-build on first open (EnvironmentAutoBuild); URP materials per model; Duko plays embedded clips via PlayableGraph; UiTapAudioBinder for button sounds; facing fix constant ModelYawFixDeg in RunnerView/CompanionView. Meshy balance ~600 credits. Owner next step: pull claude/clever-cerf-bffyg5, git lfs pull, open in Unity, send compile errors. [ASSUMED] painterly look accepted for FP; Meshy licence UNVERIFIED.
- 2026-10-07: Meshy models generated (60 credits, 1040 left): Pista 8,356 tris, Duko 4,158 tris, static, no rig. Duko close to concept; Pista missing the back X and a clear satchel, not accepted. Waiting on owner: regenerate Pista (recommended), approve Duko, rigging route, Meshy commercial terms (UNVERIFIED in LICENSES.md).
- 2026-10-07: Merged branch claude/nifty-hawking-uk9zpl (Batches 3-4, art plumbing) into claude/clever-cerf-bffyg5, which is now the single branch with everything. Duplicate audio wiring resolved in favor of the other branch's RunAudioView. This session's cloud container HAS Meshy/OpenAI/ElevenLabs keys; asset-pipeline launched for hero + macaw models (cap ~300 credits).
- 2026-10-07: Batch 4 done (gameplay-engineer): four worlds (Jungle 0-1100 m, River to 2300, Mountains to 3600, Ruins to 5000, then loop with dusk Jungle), 54 m gateway chunk at boundaries, per-world gray-box themes and skins, hazard rules, five new T4 chunks and reweighted tier 4-6 pools (densities estimated ~7.8/8.6/8.9 per 100 m vs 8/9/10; tier 6 target not reachable with breathers). Speed curve and tier table match GDD 11. Uncompiled; fairness validator not run on T4 chunks. **Owner paused all work; no agents running.**
- 2026-10-07: Batch 3b done (ui-engineer): onboarding via `TutorialDirector` (contextual lessons on the real tier-1 run, 30% speed until first swipe, revive-with-hint instead of death, 75 s cap), `TutorialView`, Replay tutorial in Settings, tutorial flags in the save. Uncompiled. [ASSUMED] contextual not the fixed 45 s script; Skip only after first completion; tutorial run counts as run 1. Existing PlayMode tests are stale (view count 17; restart on a fresh save starts the tutorial). Batch 4 (worlds) still running.
- 2026-10-07: Batch 3a done (ui-engineer): missions (sets of 3, score multiplier), daily challenge, 7-day calendar (IDayClock), coin shop (upgrades, Head Start, Shield start), run loadout, panels on the main menu. Uncompiled. [ASSUMED] Boosts armed in the shop; calendar pauses, never resets; multiplier permanent +1/set (cap 30); mission targets and daily goals are guesses; characters/outfits not for sale; no ad doubling, no Restore Purchases, no haptics yet. Existing PlayMode tests may be stale (view count +1).
- 2026-10-07: Art agents finished but generated NOTHING: the cloud container has no Meshy/OpenAI keys (`~/.config/junglebooze/secrets.env` lives on the owner's Mac), no Blender, and the proxy blocks Meshy. Code is in: `EnvironmentArt` prefab loader in all environment views (Resources/EnvironmentArt, gray-box fallback), editor menu `JungleBooze > Art > Build Environment Prefabs`, character loading in RunnerView/CompanionView (Resources/Characters), import postprocessor, scripts `tools/assetgen/gen_env_assets.py` and `meshy_characters.py` (caps: 250 / 200 Meshy credits, 4 images; Meshy endpoints written from memory, verify on first run). Spent 0 credits. [ASSUMED] Duko flight stays procedural (no wing rig); whole model incl. hair fit to hitbox; animation ids chosen by the pipeline agent. Next: run the scripts on the Mac.
- 2026-10-07: Wired audio into the Run scene: `RunAudioView` (events, menu/Jungle music, Game Over sting), catalog moved to `Audio/Resources` and loaded by name. Fixed a compile error in `RunAudioCues`. [ASSUMED] Jungle theme A. Not compiled (cloud); compile-check on the Mac next.
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
