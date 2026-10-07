# Studio Status Board

**Read this first in every new session.** It's the single source of truth for where the project stands.
The coordinating session updates it whenever it launches an agent, receives an agent's report, or records an owner decision.
Decisions themselves live in `design/DECISIONS.md`; the plan lives in `docs/AGENT_PLAN.md`.

_Last updated: 2026-10-07 (06:50 UTC)_

## Current milestone
**FIRST PLAYABLE WRITTEN (2026-10-07), not yet compiled.** Owner's next step: open `UnityProject/` in Unity 6.3 per `docs/PLAY_FIRST_BUILD.md`, press Play in the Run scene, and send back any red errors. Fix those first.

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
| gameplay-engineer | done (B1, B2) | Collisions and track code finished; not compiled | `Scripts/Gameplay/Runner/`, `Scripts/Gameplay/Track/`, `Config/Track/` | Deferred tests (track AC-208–248), B3 validator — after owner review |
| ui-engineer | done (C2) | Track wired into the Run scene with gray-box views and HUD | `Scripts/Gameplay/Views`, `Scripts/App`, `Scripts/UI/Hud` | Fix errors the owner sends from Unity |
| art-director | done | Style guide and prompts updated with names Pista/Duko and the chest-band sash | `design/STYLE_GUIDE.md`, `design/prompts/` | Generate concept images once an image API key exists |
| asset-pipeline | waiting | — | — | Needs an image/3D generation API key from the owner |
| audio-director | waiting | — | — | Week 5. Note: the macaw speaks a few words, so voice lines and localization are needed |
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
| G2 Art direction | Done: Inkbound Pulp, hero H2 Mapcloth, macaw M3 Dusk |
| G3 Feel check #1 | Not started (end of week 2, needs a TestFlight build) |
| G4–G8 | Not started |

## Open questions for the owner
1. Ads and prices: deferred to week 4 (tracking already decided: none).

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

## Not yet verified
- Track code (B2): compile risks flagged by the agent: a property named `Track` inside namespace `JungleBooze.Gameplay.Track`; properties named like their types; `ref readonly` returns. Runner↔track integration never exercised. No `.asset` config files yet (code defaults are used).
- Overlap: `RunSession` (track) and `GameSession` (presentation) both implement the run lifecycle; C2 must pick one.
- Stage D bootstrap script: APIs checked by hand against Unity/URP source, not compiled. `-warnaserror+` is on, so any warning breaks the build.
- Style guide draw-call and triangle estimates (78/120 draws, ~137k/150k tris) need the benchmark scene.
- Image prompts are untested (no image API key).
- **Nothing has been compiled.** The cloud container has no Unity or .NET. First real check: opening the project on the owner's Mac, or the GitHub test workflow once the secrets exist.
- Unity editor version `6000.3.0f1` and package versions weren't checked against Unity's registry.
- Hand-made `.meta` files and asmdefs are unconfirmed until Unity opens the project.

## Log
Newest first. One line per event.
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
