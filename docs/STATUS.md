# Studio Status Board

**Read this first in every new session.** It's the single source of truth for where the project stands.
The coordinating session updates it whenever it launches an agent, receives an agent's report, or records an owner decision.
Decisions themselves live in `design/DECISIONS.md`; the plan lives in `docs/AGENT_PLAN.md`.

_Last updated: 2026-10-06_

## Current milestone
**Week 1: movement, deterministic core, bot player, style guide.** Week 0 is done.

## Agents: who is doing what

| Agent | State | Current / last task | Output | Next for this agent |
|---|---|---|---|---|
| producer | not used yet | (the coordinating session does this role for now) | this file | — |
| game-designer | **working** | Spec 002: endless track, obstacles, coins, fairness rules | `docs/specs/002-track-obstacles-coins.md` | Spec 003: vine swinging (week 3) |
| balance-simulator | waiting | — | — | Run spec 001 targets S1–S9 on the seeded test course once movement code lands |
| tech-architect | done (week 0) | Architecture, ADRs 0001–0002, Unity skeleton, deterministic core + tests, CI test workflow | `docs/ARCHITECTURE.md`, `docs/adr/`, `UnityProject/`, `.github/workflows/test.yml` | Week 1: Roslyn analyzers; add the `JungleBooze.App` assembly to the rules file once the owner agrees |
| gameplay-engineer | **working** | Implementing spec 001 (player movement), bot input provider, tests named by AC id | `UnityProject/Assets/_Game/Scripts/Gameplay`, `Tests/` | Spec 002 |
| ui-engineer | waiting | — | — | Week 4: menus, shop |
| art-director | done | Style guide and prompts updated with names Pista/Duko and the chest-band sash | `design/STYLE_GUIDE.md`, `design/prompts/` | Generate concept images once an image API key exists |
| asset-pipeline | waiting | — | — | Needs an image/3D generation API key from the owner |
| audio-director | waiting | — | — | Week 5. Note: the macaw speaks a few words, so voice lines and localization are needed |
| qa-engineer | waiting | — | — | Review spec 001 test coverage after the gameplay-engineer finishes |
| performance-engineer | waiting | — | — | Benchmark scene (week 2+) |
| code-reviewer | waiting | — | — | Review the spec 001 implementation |
| monetization-engineer | waiting | — | — | Week 4–5 |
| appstore-compliance | **working** | Trademark check of Pista/Duko, 8 public app name candidates, early rejection-risk review, checklist refresh | `docs/compliance/`, `docs/APP_STORE_CHECKLIST.md` | Privacy manifest when SDKs are chosen |
| release-engineer | **working** | iOS build script, fastlane beta lane, one-command build for the owner's Mac, plain-language setup guide | `fastlane/`, `tools/build/`, `docs/RELEASE.md`, `Scripts/Editor/Build/` | First TestFlight build for G3 (end of week 2) |

States: **working** (launched, report not received) · waiting · blocked · done.
If a new session finds an agent marked **working** but no matching output or commit, assume that agent was interrupted. Check the listed output files and relaunch the task.

## Owner gates
| Gate | Status |
|---|---|
| G0 Accounts & setup | Partly done. Decided: builds on owner's Mac, iPhone 11 / SE 2 is the lowest device, Unity Personal for CI. **Owner to do:** add `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` GitHub secrets; open the project once in Unity 6 on the Mac; Apple Developer account; image/3D/audio API keys |
| G1 Creative brief | Done: wild jungle kid, macaw, pulpy adventure, all 4 worlds, macaw speaks a few words |
| G2 Art direction | Done: Inkbound Pulp, hero H2 Mapcloth, macaw M3 Dusk |
| G3 Feel check #1 | Not started (end of week 2, needs a TestFlight build) |
| G4–G8 | Not started |

## Open questions for the owner
1. Add the `JungleBooze.App` assembly to the project rules' assembly list? (tech-architect proposal)
2. Ads and prices: deferred to week 4.

## Assumptions waiting for owner review (`[ASSUMED]`)
- Stumble rule: clipping an obstacle's side is a stumble, the second stumble ends the run (spec 001). Judge at G3.
- Macaw cheer call-out word is "Shiny!" / "Wow!".
- Portrait only; daily calendar pauses instead of resetting; Assist ("Lift") by double tap; shield doesn't save from chasms.
- Coins are gold with a turquoise gem center so they read against gold scenery.

## Not yet verified
- Style guide draw-call and triangle estimates (78/120 draws, ~137k/150k tris) need the benchmark scene.
- Image prompts are untested (no image API key).
- **Nothing has been compiled.** The cloud container has no Unity or .NET. First real check: opening the project on the owner's Mac, or the GitHub test workflow once the secrets exist.
- Unity editor version `6000.3.0f1` and package versions weren't checked against Unity's registry.
- Hand-made `.meta` files and asmdefs are unconfirmed until Unity opens the project.

## Log
Newest first. One line per event.
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
