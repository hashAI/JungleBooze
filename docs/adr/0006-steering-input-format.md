# ADR 0006: Steering input format (replay format 2)

- Status: Accepted (amends ADR 0002 §3 and ARCHITECTURE §5.3)
- Date: 2026-10-09 (proposed); accepted 2026-10-09 after the Phase 1 review fixes
- Deciders: gameplay-engineer, per spec 101 §3.2; confirmed by gameplay-engineer with the tech-architect sign-off
  delegated by the coordinator (2026-10-09), after review `docs/reviews/2026-10-09-phase1-feel.md` (S6)
- Hard to undo: **yes** (replays, bots and every input source produce this struct)

## Context

ADR 0002 defined the per-tick input as `InputCommand` byte flags (`MoveLeft`, `MoveRight`, `Jump`, `Slide`,
`CompanionAssist`, `PauseResumed`) for the lane-based game. AURELIA has no lanes (spec 101): steering is a relative
drag that moves a continuous lateral target, plus a fixed 2.2 m dodge. A flag-only format cannot carry analogue
steering, and replays must stay bit-exact.

## Decision

1. **`InputFrame { InputCommand Commands; short LateralDeltaMm; }`** is the per-tick input (Core).
   `IInputProvider.ReadInput(long tick)` returns it (was `ReadCommands` → `InputCommand`). Touch/keyboard, bot and
   replay providers all produce it; the simulation cannot tell them apart.
2. **`InputCommand` (byte flags), renumbered:** `Jump = 1`, `Slide = 2`, `DodgeLeft = 4`, `DodgeRight = 8`,
   `TouchBegan = 16`. At most one of Jump/Slide/Dodge* per tick (`InputCommands.Discrete` picks the lowest bit if a
   malformed frame carries several). `TouchBegan` is recorded for analysis (gesture starts in replays); since
   2026-10-09 [ASSUMED] the simulation ignores it, because a dodge now moves 2.2 m from Pista's current position
   (spec 101 §2.3, review S1) instead of from a touch origin. The flag keeps its value; the binary layout and the
   format version are unchanged (a replay is only valid for the build and config hash that recorded it). Lane commands, `CompanionAssist` and `PauseResumed` are
   removed: the companion is not in Phase 1, and pause is outside the simulation (the run is frozen; the
   recognizer ignores touches held at resume). From now on values are append-only again.
3. **Steering is quantized to whole millimetres per tick** after sensitivity. Input sources carry the sub-millimetre
   remainder to the next frame (`FrameInputDispatcher`), so the sum delivered equals the finger travel with no drift,
   and the simulation consumes only integers. A frame's drag is split evenly across the frame's ticks (remainder on
   the last); discrete commands are delivered one per tick in recognition order.
4. **Replay format version 2.** `InputRecording` stores the header (format version, seed, config hash, build
   version) and sparse `(tick, InputFrame)` records (empty frames are skipped). `InputRecordingSerializer` writes
   `"JBRP"`, `int32 version`, `uint64 seed`, `uint64 config hash`, build string, `int32 count`, then
   `int64 tick, uint8 commands, int16 lateral mm` per record. Reading any other version throws
   `NotSupportedException`: replays are never migrated (they are only valid for one build anyway, ADR 0002).
   Format 1 (lane era) files are not readable; none exist outside the archived code.
5. The config hash in the header is FNV-1a 64 over the JSON of the movement configs and the course
   (`MovementConfigAssets.ComputeHash`), so a replay can be matched to the tuning that produced it.

## Options considered

| Option | Verdict |
|---|---|
| Float metres per tick | Rejected: float sums differ by accumulation order; a replay must reproduce the exact value. |
| Absolute lateral target per tick | Rejected: larger records, and the "no debt past the edge" rule (spec 101 AC-101-05) needs deltas applied to a clamped target inside the simulation. |
| Keep lane flags and add a separate steering stream | Rejected: two streams to keep in sync for no benefit. |
| `short` millimetres (chosen) | ±32.7 m per tick, far beyond any input; 3 bytes per non-empty tick plus the tick. |

## Consequences

- A 60 s run with continuous steering records about 3,600 frames (~40 KB uncompressed); fine for bug reports.
- Bots (`PerfectBot`) and replays drive the same simulation through the same struct; EditMode tests prove
  record → replay equality on the feel course and identical state for 1–5 ticks per frame (AC-101-03).
- `ARCHITECTURE.md` §5.3 should be read with this ADR: `InputCommand` members and `ReadCommands` changed as above;
  the 150 ms buffer and 100 ms coyote time stay in the simulation (spec 101 values, not the old GDD ones).

## Amendment 1 (2026-10-09): replay format 3, expedition runs

- Status: Accepted (gameplay-engineer, after review `docs/reviews/2026-10-09-vertical-slice.md` B1)
- Hard to undo: no (format versions are never migrated; old files simply stop loading)

**Context.** Directed expedition runs depend on more than the input stream: the run seed
(`RunSeed(worldSeed, runIndex)`), the profile state the World Director and tracker read (owned abilities, pending
showcase, skill S, discovered journal entries), the test-only forced speed, and revives, which change the simulation
outside the input stream. Format 2 stored none of these (the header seed was always 0), so a run 2+ replay could not
reproduce the chunk sequence, and any revived run diverged at the revive tick.

**Decision.**
1. `InputRecording` gains `Setup` (`ReplaySetup`: first-expedition flag, owned and pending-showcase masks, skill,
   forced speed, discovered ids) and **markers** (`ReplayMarker { long Tick; ReplayMarkerKind Kind }`, `Revive = 1`;
   kind values are append-only). The header seed is the run's seed.
2. `ExpeditionSession.Recording` records inputs of Running/Finishing ticks keyed by the session tick, restarts the
   recording with the seed and setup on `BeginRun`, and adds a `Revive` marker on the session tick the revive was
   accepted. A marker is applied **before** that tick's input is read (`ExpeditionReplay.Play`).
3. Binary layout, format 3: magic `"JBRP"`, int32 3, uint64 seed, uint64 config hash, string build; uint8 setup
   present; if present: uint8 flags (bit 0 first expedition), int32 owned, int32 pending showcase, float32 skill,
   float32 forced speed, int32 discovered count, strings; int32 marker count, per marker int64 tick + uint8 kind;
   int32 frame count, per frame int64 tick + uint8 commands + int16 lateral mm. Other versions throw
   `NotSupportedException` (no migration, as before).
4. The revive ready beat (`RunFlowConfig.ReviveReadyTime`, 1.0 s) is a Ready phase inside `RunSession`, so it is
   reproduced by the replay without extra data.
5. Debug-build replays are written to `Application.temporaryCachePath/replays` (iOS `Library/Caches`, not backed up).

**Consequences.** EditMode test `SliceReviewFixTests.B1_Run2WithRevive_RecordsSetupAndRevive_AndReplaysBitExactly`
records a run-2 profile with a paid revive, round-trips the file, replays it and compares the full `RunnerState`,
the chunk sequence and the run stats. The feel-test scene still writes plain recordings (setup null), which stay
valid as simulation-only replays.
