# Code review: Vertical slice part A + B (commits 9835543, d58990d; specs 102, 103)

- Reviewer: code-reviewer
- Date: 2026-10-09
- Scope: Scripts/Gameplay/{World,Expedition,Movement,Analytics}, Scripts/Core/Save, Scripts/Services/Save,
  Scripts/App/Expedition, Scripts/UI/Expedition (setters only), Editor/Expedition/SliceChunkLayouts (canopy widths), Tests.
  Read against spec 102, spec 103, ADR 0002/0006, ARCHITECTURE, GDD §11/§22, APP_STORE_CHECKLIST.
- Verification: `tools/ci/unity.sh test EditMode` run locally (no Unity instance was open): **464/464 passed**.
  The working tree also had uncommitted art/tool changes (Pista.fbx, Blender scripts); no reviewed code was modified.
  PlayMode was **not** run. Nothing was checked on a device.
- Verdict: **Changes requested.** There is 1 blocking finding.

Counts: blocking 1 · should fix 12 · nit 11

What holds up well: the simulation is tick-based throughout (swim, dive, leap, swing and deep dive count ticks;
`_dt` is constant), so the 10–16 m/s timings do not depend on frame rate. Slow-time help scales only the real-time
accumulator, so the simulation stays deterministic. Random streams are forked in a fixed order, and Expedition 1 uses
the script seed. Per-tick paths use fixed rings and arrays. The HUD setters update only when values change.
Analytics has no network path. Save writes use tmp → replace → bak, and a file from a newer build is read-only.
Test coverage of spec 103 ACs is broad (most ACs have a named test).

---

## Blocking

### B1. Replays from run 2 onward cannot be reproduced (ADR 0002 contract, ARCHITECTURE §5.3)
- **Where:** `Scripts/App/Expedition/ExpeditionRoot.cs:210` (`new InputRecording(0UL, ...)`, created once per scene and
  only `Clear()`ed per run), `ExpeditionRoot.cs:470-492` (`AcceptRevive`), `ExpeditionRoot.cs:837-861` (`SaveReplay`).
- **Problem:** the replay header seed is always 0. Directed runs seed the director with
  `RunSeed(worldSeed, runsCompleted)`, and the run also depends on `Owned`, `PendingShowcase`, `Skill` and
  `FirstExpedition`. None of these are stored. A revive changes the simulation outside the input stream
  (`Simulation.Revive()`), and it is not recorded either. Expedition 1 replays only work because the director
  replaces the seed with the script seed.
- **Failure scenario:** a playtester reports an "unfair" death in run 3 (fun gate F11 / checklist #4). The
  `expedition-last.jbr` replay produces a different chunk sequence from tick 0, so the death cannot be reproduced. Any
  run with a revive diverges at the revive tick even if the seed were right.
- **Fix:** create the `InputRecording` per run with `setup.Seed`, and store the run setup (owned, pending showcase,
  skill bits, first-expedition flag, forced speed) plus revive ticks. Bump the replay format to 3 with an ADR 0006
  amendment, and add a record → replay test for a run-2 profile with one revive. Short-term alternative: refuse to
  write a replay for directed or revived runs, so nobody trusts a wrong file.

## Should fix

### S1. The Showcase is spent when the chunk is *planned*, not when it is reached
- **Where:** `Scripts/Gameplay/World/WorldDirector.cs:250-255` (`ShowcasedAbilities |=` at plan time; planning runs ≥ 2
  chunks / 220 m ahead), `Scripts/Gameplay/Expedition/ProgressionRules.cs:88` (clears `pendingShowcase` at banking).
- **Failure scenario:** run 2 after learning Deep Breath. `R_Swim_Pool_01` is planned as pick 1 or 2 at tick 0. The
  player dies in pick 1. Banking clears the pending flag, so the showcase is never forced again. That defeats spec 103
  §9.4 "How run 2 shows it" and fun-gate signal #7 for that player.
- **Fix:** clear the pending bit only when the runner enters the showcase chunk (`RunTracker.StepChunkEntry` checks
  `pick.Reason == Showcase`). Test: die before the showcase chunk → still pending in run 3.

### S2. Sailback spawn groups collide between chunks n and n+2, so a creature discovery is silently lost
- **Where:** `Scripts/Gameplay/Expedition/SailbackSystem.cs:18, 129` (`GroupCapacity = 16`, `g = group % 16`,
  `group = serial * 8 + k`).
- **Problem:** serials n and n+2 map to the same slot for the same spawn index k. Chunk n+2 is appended while the
  runner is still in chunk n (stream-ahead 2). Its spawn overwrites `_groupId/_groupObserved/_groupFired`, so the
  animals of chunk n no longer match `_groupId[g]` and stop being observed.
- **Failure scenario:** in a director run, `C_Canopy_VineSpan_01` (flock, k=0) is followed two picks later by
  `R_Branch_Waterfall_01` (k=0). The canopy flock never reaches 48 observed ticks, and D-02 is never found. A player who
  missed the sailback in Expedition 1 can keep missing it (spec 102 §7 FTUE creature guarantee). Expedition 1 is
  unaffected only because of its serial parity.
- **Fix:** key the slots by a free-slot search (like the animal slots), or use a capacity that is coprime with the
  stride and larger than live chunks × spawns. Add a director-run test with canopy at n and waterfall at n+2.

### S3. A save I/O exception at run end leaves the player stuck with no results
- **Where:** `ExpeditionRoot.cs:810-835` (`FinishRun`: `_resultsShown = true` is set before `_save.Save` at :815,
  then `ShowResults`), `ExpeditionRoot.cs:450` (`Learn`). `FileSaveStorage.Write` throws on IOException/disk full.
- **Failure scenario:** the device storage is full. `Save` throws from `Present()` in `Update`. Results never show,
  and `_resultsShown` stops a retry. The run is banked in memory only, and the screen stays on the dead runner. In
  `Learn`, the coins are deducted in memory, the exception escapes, and the unlock UI never appears.
- **Fix:** catch IO and permission errors in `SaveService.Save`, return false, and log them. Show results anyway, and
  retry the save on the next safe point (RUN AGAIN, app pause). Add a test with a throwing `ISaveStorage`.

### S4. Revive placement can be unfair at a gap: gaps are not cleared, there is no ready beat, and i-frames do not cover Fall
- **Where:** `Scripts/Gameplay/Movement/RunnerSimulation.cs:421-519` (revive at the last safe point ≥ 6 m back; clears
  only obstacles at :470), `ExpeditionRoot.cs:485-491` (the simulation resumes on the next frame).
- **Failure scenario:** a fall into a 4.5 m gap at 15 m/s, or into a canopy beam gap. The revive point is 6–7 m
  before the lip at 0.5× speed, ramping up. The player's thumb is still on the Continue button, and they have about
  0.7–0.9 s to jump. That is below the Rhythm `minActionGap` and the Learning visibility. A Fall is not absorbed by
  i-frames, so a second death costs 2 more crystals. Nothing validates revive points.
- **Fix:** after accepting, use the 1.0 s ready beat as on resume. Choose the revive point so that the next required
  action is ≥ the phase `minActionGap` (plus visibility) away at the ramped speed. Add a test that revives at every
  gap in the slice chunks and asserts the Perfect bot passes with a 0.15 s reaction delay.

### S5. V7 (visibility) checks obstacles near a chunk entry from the wrong place (the validator can miss them)
- **Where:** `Scripts/Gameplay/World/ChunkValidator.Traversal.cs:717-731` (`Math.Max(0f, sr)`), `:767-773`
  (`CurvedPoint` uses only this chunk's curve).
- **Problem:** for any obstacle within `v · lead` of the entry seam (32 m at 16 m/s, 2 s), the runner viewpoint is
  clamped to s = 0. The previous chunk's curve (it may curve right after its 6 m straight seam zone, radius 60 m) and
  its geometry are never considered.
- **Failure scenario:** a pool chunk with a Blocker at s 20 follows a chunk that bends sharply before its exit seam.
  The blocker is outside the frustum 1.5 s before contact, but V7 passes because it looked from s = 0.
- **Fix:** evaluate V7 from a straight run-in of `v·lead` (seams guarantee only 6 m straight), or require that
  obstacles start ≥ `vMax · visibility` after the entry seam. Alternatively, validate visibility on pairs at
  director-fuzz time. Add a test with an obstacle at s 10.

### S6. Director emergency path can pick a variant that fails V11, and an empty filter throws mid-run (AC-102-11)
- **Where:** `WorldDirector.cs:316` (`Finish(..., emergency)` passes `ignoreSeam: true`, and `EligibleVariants`
  then admits variants that fail V11), `WorldDirector.cs:319` (throws `InvalidOperationException`).
- **Failure scenario:** a future chunk with two variants where only variant 0 passes V11. An emergency pick can
  roll variant 1, which puts two required actions closer than `minActionGap` across the seam. If the filter is ever
  empty, the exception escapes `Session.Step` in `Update` every frame, and the game freezes. Today the 10k-seed fuzz
  shows 0 emergency picks, so this is latent.
- **Fix:** never ignore V11 (`Collect` already guarantees that one variant passes). Replace the throw with a final
  fallback to any Recovery chunk with a matching entry seam, ignoring R1–R3, and log it. Add a test with a two-chunk
  library.

### S7. The on-device analytics log is in Documents, grows without limit, and opens the file per line
- **Where:** `Scripts/App/Expedition/LocalAnalyticsLog.cs:22, 47`; `ExpeditionRoot.cs:264`. The replays at :846 have
  the same location issue in debug builds.
- **Problem:** `persistentDataPath` on iOS is `Documents/`, which is backed up to iCloud and is meant for user data.
  Apple's storage guideline (App Review 2.23 / iOS Data Storage Guidelines) asks apps to put app-generated,
  re-creatable data in `Library/Caches` or to mark it do-not-backup. `events.jsonl` is never rotated, and
  `File.AppendAllText` runs once per line.
- **Fix:** write to `Application.temporaryCachePath` (or Application Support with the NoBackup flag). Cap the file
  (e.g. 256 KB, roll to `.1`). Write one batch per flush. Have appstore-compliance confirm the "Data Not Collected"
  wording still holds. It does today: nothing leaves the device.

### S8. Analytics `seed` is a stable per-install identifier
- **Where:** `Scripts/Gameplay/Analytics/AnalyticsRecorder.cs:204`, `ExpeditionRoot.cs:288, 1055-1059`.
- **Problem:** `RunSeed` is SplitMix64 of `(worldSeed, runIndex)`. Given a logged seed and `run_index`, `worldSeed` can
  be recovered. That seed is persistent and derived from the install time (`DateTime.UtcNow.Ticks`). The design keeps
  `session_id` random per launch, but this field links every session of an install. On-device only, so there is no
  privacy-label impact now. Once the planned TestFlight upload ships, it becomes a device-level identifier.
- **Fix:** before any upload, log a per-run random seed id or a salted hash that can't be inverted. Generate
  `worldSeed` from a CSPRNG rather than the clock. Raise this with appstore-compliance against the checklist.

### S9. Journal sightings are never saved (spec 103 §7.3 "sighting count +1")
- **Where:** `ProgressionRules.cs:91-100` (only new entries are added); `RunTracker.cs:277-281` counts
  `Stats.Sightings` but not per entry.
- **Fix:** track sightings per entry index in `RunStats`, and add them to `JournalRecord.sightings` at banking. Extend
  `AC103_28_30` to assert the persisted count.

### S10. Analytics and economy logic sit outside the planned layers (architecture drift)
- **Where:** `Gameplay/Analytics/IAnalyticsSink.cs`, `App/Expedition/LocalAnalyticsLog.cs`, `ExpeditionRoot.cs`
  (1,111 lines: save, revive payment, banking, analytics, results flow, input, camera).
- **Problem:** ARCHITECTURE §7 puts `IAnalyticsService` in Core and adapters in Services. Here the concrete log lives
  in App, and the economy flows (revive payment → save, learn → save) are App-only code. EditMode tests can't reach
  them, which is how S3 went unnoticed.
- **Fix:** move the sink interface to Core (`IAnalyticsService`) and the file log to Services. Extract a plain-C#
  `ExpeditionFlow` (finish run, learn, accept revive, save policy) with EditMode tests. Otherwise record the deviation
  in an ADR.

### S11. V14 measures the best-case lateral shift, not the worst case, and ignores the air lateral factor
- **Where:** `ChunkValidator.Traversal.cs:405-424`.
- **Problem:** `shift` is the distance between the *nearest* points of beam A and beam B's landing window. A runner on
  the far edge of A needs up to width + offset. Steering over the gap runs at `AirLateralFactor`, but the check uses
  the full 8 m/s human rate. The shipped beams pass with wide margins, so this is latent. A beam pair at the 1.0 m
  offset limit with a High obstacle just before the gap (slide factor) would pass V14 but fail humans.
- **Fix:** use the farthest point of A (`max(|toMin − fromMin|, |toMax − fromMax|)`, clamped to ≥ 0). Scale the airborne
  part by `AirLateralFactor`. Add a negative test.

### S12. Save edge cases: a crash between the two renames loses the last save, and corrupt files are overwritten
- **Where:** `Scripts/Services/Save/FileSaveStorage.cs:66-84`, `SaveService.cs:33-60`.
- **Problem:** on Unix, `File.Replace` renames primary → bak, then tmp → primary. If the app is killed between the
  two, only `.bak` (previous) and `.tmp` (newest, complete) remain. `Load` ignores `.tmp`, so the last run, or a LEARN
  purchase, is lost. When both files are corrupt, the next save moves the corrupt primary over `.bak`, so nothing is
  ever kept for diagnosis.
- **Fix:** on load, if the primary is missing and `.tmp` decodes, promote it. Copy unreadable files to
  `save.corrupt-<n>.json` before falling back. Add a FileSaveStorage test that simulates the half-finished state.

## Nits

- **N1** Revive re-arms vine rewards and counters: a revive at the funnel lets the player re-earn
  `PerfectReleaseCoins` (`RunTracker.cs:205-236`). Routes crossed again are counted twice (`RunTracker.cs:158`), and
  the distance re-run after moving back is added again (`Distance` is not rewound in `Revive`). This is small
  (crystals pay for it), but it inflates best distance and analytics.
- **N2** `Scripts/Gameplay/Run/RunSession.cs:126` accepts a revive in `RunPhase.Results`. The UI can't reach it today,
  but if it did, the second leg would never be banked (`_resultsShown` is already true). Allow `Dying` only.
- **N3** Restart (R) or an abandoned run leaves its analytics events in the ring with no `run_ended`, under the same
  `run_id`. `run_started.seed/abilities_mask` are read from `_lastSeed/_lastAbilities` at *flush* time
  (`AnalyticsRecorder.cs:62-67, 204-205`), so earlier `run_started` lines get the wrong seed. Store them in the event.
- **N4** `run_ended` lacks `crystals`, `hits`, `revives`, and `death` lacks `phase` (GDD §22). AC-103-50 doesn't
  require them, but the GDD table does.
- **N5** AC-103-11 "a buffered slide becomes a dive" is not implemented (`RunnerSimulation.Swim.cs:105-142` only
  converts fast-fall and a buffered jump). Mostly unreachable, because Slide in the air is a fast-fall, but either
  implement it or amend the spec.
- **N6** The swing (`RunnerSimulation.Vine.cs:23, 136-137, 191-192`) and the deep dive (`Swim.cs:519`) call
  `Math.Cos/Sin` every tick. ADR 0002 prefers tables in outcome paths, and a 61-entry swing table is trivial.
- **N7** Killing the app mid-run loses the run's discoveries and rewards, because banking happens only at results.
  This is acceptable by spec, but worth a line in the FTUE notes, since D-01/D-02 in a run 1 killed at minute 3 must be
  found again.
- **N8** `Dress` applies `RiskyCrystalChance` to every non-`Always` crystal anchor, whatever its route
  (`WorldDirector.cs:652-659`). Spec 102 §7 limits it to Risky branches.
- **N9** `Stats.DeathLabel = now.Cause.ToString()` allocates on the death tick (`RunTracker.cs:133`). Use a static
  name table.
- **N10** Beam C is at x −0.2 instead of the spec's −0.6 (`SliceChunkLayouts.cs:333`, `[ASSUMED]`). Get the designer
  to confirm it in DECISIONS, or move Beam B so the spec layout passes V14.
- **N11** `AnalyticsRecorder` drops events silently when the 512 ring is full (`Dropped` is never logged or
  serialized). Emit a `dropped` count in `run_ended`.

## Open questions for owner
None. All findings are technical. S7/S8 go to appstore-compliance before any analytics upload is enabled.
