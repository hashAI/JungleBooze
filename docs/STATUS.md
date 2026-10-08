# Studio Status Board

**Read this first in every new session.** It's the single source of truth for where the project stands.
The coordinating session updates it whenever it launches an agent, receives an agent's report, or records an owner decision.
Decisions themselves live in `design/DECISIONS.md`; the plan lives in `docs/AGENT_PLAN.md`.

_Last updated: 2026-10-08 (AURELIA Phase 0: look test)_

## Current milestone
**AURELIA, Phase 0: look test** (owner go, 2026-10-08). Source of truth for the direction: `design/aurelia/BLUEPRINT.md` and
`design/aurelia/vision_board.png`. Pista, Duko and the game name stay. Look: **realistic**, AI-made art plus free CC0 assets,
no paid packs. Quality bar: professional, polished in a small scope (owner wants "the greatest product").

**Phase plan (owner checks at the end of each phase; don't start the next until it passes):**
| Phase | Build | Owner check | Est. |
|---|---|---|---|
| **0 Look test** ← now | One realistic forest + waterfall stretch, realistic Pista running through it, on iPhone | Looks professional on the phone and runs smoothly | 1–2 wk |
| 1 Feel | Movement + camera tuned: a fun 60-second run | Is just running fun? | 2–3 wk |
| 2 Vertical slice | Blueprint Part LV: route choice, river/swim, vine, Duko, a creature, a secret, results, first upgrade | 5 testers say "let me try again" | 4–6 wk |
| 3 MVP | Full forest biome, 12–15 chunks, journal, 5–8 unlock abilities, light adaptive difficulty, audio | Owner wants to play daily | 3–4 mo |
| 4 Retention test | Missions, daily expedition, next-goal results; TestFlight 20–50 testers | D1 ≥ 40%, D7 ≥ 15% | 1–2 mo |
| 5 Launch | Store, compliance, performance | Ship | ~1 mo |

**Phase 0 tasks:**
- [x] P0-A art-director: realistic art direction for AURELIA (original visual language, clearly not Pandora), realistic Pista and Duko redesign briefs + prompt library. Images wait for API keys in this environment
- [x] P0-B tech-architect: look-test technical design (URP realistic-on-mobile settings, budgets, CC0 sourcing with licenses) + LookTest scene builder and asset fetch script
- [ ] P0-C realistic Pista concept images → owner picks. **Blocked: OpenAI account out of credits.** Run `tools/art/gen_concepts.py turnaround A|B|C`, then `keyart A|B|C`
- [ ] P0-D 3D Pista via Meshy (needs `MESHY_API_KEY`), rig + run/jump/slide animations (phone video → AI motion capture)
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
| game-designer | done (stage A2) | Spec 002: track, obstacles, coins, generator, fairness, lifecycle | `docs/specs/002-track-obstacles-coins.md` | Apply spec 002's listed changes to spec 001 and the GDD; spec 003 vine swinging |
| balance-simulator | done (A3) | Python reference model, golden traces, S1–S9 report (`docs/sim-reports/2026-10-07-spec001.md`) | `tools/sim/`, `docs/sim-reports/` | Fairness fuzzing for spec 002; recommendations R1–R6 go to game-designer |
| tech-architect | done (stage D) | First-open bootstrap: URP mobile asset, input set to Both, iOS basics (placeholder bundle id `com.pistaduko.junglerunner`, iOS 15, portrait), empty Run scene in build list; Mac play guide | `Assets/_Game/Editor/Setup/`, `docs/adr/0003-first-playable-bootstrap.md`, `docs/PLAY_FIRST_BUILD.md` | Add EditMode tests for `ProjectSetupRules`; Roslyn analyzers |
| gameplay-engineer | done (batch 2) | Power-ups, lane strikes, Duko, continue; compile-checked | `Scripts/Gameplay/PowerUps`, `Hazards`, `Companion`, `Views`, `App/RunSceneBootstrap.cs` | Wire audio events; then Batch 3 |
| ui-engineer | done (C2) | Track wired into the Run scene with gray-box views and HUD | `Scripts/Gameplay/Views`, `Scripts/App`, `Scripts/UI/Hud` | Fix errors the owner sends from Unity |
| art-director | done (concepts) | Hero take 1 and macaw take 1 locked; four unused recolors on disk | `design/concepts/2026-10-07/` | Store art later |
| asset-pipeline | waiting | — | — | Keys ready. Starts from locked sheets: `hero_take1_turnaround.png`, `macaw_take1_turnaround.png` |
| audio-director | done (files) | SFX, Duko voice (from history), Jungle/menu music, `AudioPlayback` | `Assets/_Game/Audio`, `Scripts/Services/Audio` | Wire playback into the Run scene |
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
0. API keys for image/3D/audio generation must be added to the cloud environment (see Phase 0 "Blocked").
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
- AudioPlayback is not spawned in the Run scene yet, so Play is still silent.
- Unity editor version `6000.3.0f1` and package versions weren't checked against Unity's registry.
- Hand-made `.meta` files and asmdefs are unconfirmed until Unity opens the project.

## Log
Newest first. One line per event.
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
