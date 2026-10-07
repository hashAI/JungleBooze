# tools/sim: spec 001 reference model, bots and golden traces

Owner: balance-simulator. Python 3 standard library only (nothing to install).

| File | What it is |
|---|---|
| `runner_model.py` | Deterministic 60 Hz tick model of `docs/specs/001-player-movement.md`: lanes (tween, queue, reverse, bump), jump by formula, fast-fall, slide (with extension under high barriers), input buffer, coyote time, pause/resume via `PauseResumed`, swept-AABB collisions (lethal vs stumble, edge forgiveness, daze), outcome accounting (I9). Ground comes from a pluggable provider (`FlatTrackQuery` default, `TestTrackQuery` with gaps). Places where the spec is silent are marked `[MODEL-CHOICE]` in the code. |
| `test_runner_model.py` | unittest suite pinning the model to the spec tick numbers (AC-01 to AC-46 where they apply to simulation). |
| `test_golden.py` | Checks the golden files still match the model and pins their key numbers. |
| `test_course.py` | Seeded movement gauntlet (spec 001 section 15): reference obstacles and 2–4 m gaps, single or pairs, GDD 11.2 tier spacing. |
| `test_bots.py` | Bot harness checks: deterministic bot runs, I9 accounting on bot runs, what-if config overrides reach the runner. |
| `bots.py` | Oracle solver (DFS over per-tick commands) and expert / average / new skill bots (profiles [ASSUMED]). |
| `run_targets.py` | Runs S1–S9 and writes the report. Raw results go to `out/` (git-ignored: regenerate them; the committed artifact is the report in `docs/sim-reports/`). |
| `report.py` | Builds `docs/sim-reports/<date>-spec001.md` from `out/*.json`. |
| `golden.py` | Writes / checks `golden/*.json`. |

## Commands

```
cd tools/sim
python3 -I -m unittest -v                 # all tests (64, about 1 s)
python3 -I golden.py --check              # golden files match the model (exit 1 if not)
python3 -I golden.py                      # regenerate golden files (only after a spec change)
python3 -I run_targets.py all             # S1..S9 + report (S1 about 23 min on 4 cores, bots a few min)
python3 -I run_targets.py bots            # S4..S7 only (13,400 bot runs)
python3 -I run_targets.py whatif          # candidate tuning variants on the S5/S6 seeds (model only, nothing applied)
python3 -I run_targets.py report          # rebuild the report from out/
```

From the repo root: `python3 -I -m unittest discover -s tools/sim -t tools/sim -v`.

## Golden trace format (`golden/*.json`, schema `junglebooze.golden-trace.v1`)

One file per scenario. All tick values assume the spec 001 start values, which are written into the file so a
C# test can build the same `RunnerConfig` and fail loudly if the defaults drift.

| Field | Type | Meaning |
|---|---|---|
| `schema` | string | `"junglebooze.golden-trace.v1"`. Bump on any breaking change. |
| `name`, `description` | string | Scenario id and what it checks (with the expected key ticks). |
| `spec` | string | Spec the trace pins. |
| `generator` | string | Tool that wrote the file (`tools/sim/golden.py (runner_model.py)`). Informative only. |
| `seed` | int | Run seed. Spec 001 movement uses no randomness, so the seed only identifies the run; pass it to the run setup anyway. |
| `config` | object | Authoring values (`RunnerTuning` field names in camelCase, designer units). |
| `derived` | object | Tick and derived values `ToConfig()` must produce (`laneSwitchTicks` 7, `laneQueueStartTick` 4, `jumpAirtimeTicks` 36, `fastFallMaxTicks` 6, `slideTicks` 39, `inputBufferTicks` 9, `coyoteTicks` 5, `stumbleBounceTicks` 9, `stumbleDazeTicks` 180, `gravityMps2` 33.333333, `jumpVelocityMps` 10). |
| `speed` | object | `fixedSpeedMps` (constant speed; the speed curve is bypassed), `useStartRamp` (false: the 30-tick start ramp is off), `speedMultiplier`. |
| `track` | object | `kind`: `"flat"` (use `FlatTrackQuery`) or `"gaps"`. `gaps`: list of `{z0, z1, x0, x1}` rectangles with no ground. `HasGround(x, zMin, zMax)` is false only when one gap rectangle covers the whole HERO footprint (`X ± 0.35`, `z ± 0.25`), boundaries inclusive: `z0 <= zMin && z1 >= zMax && x0 <= xMin && x1 >= xMax`. Adjacent rectangles are not merged. |
| `obstacles` | list | Empty in v1 (collision goldens come with spec 002). |
| `pauses` | list | `{before_tick, note}`: the run is paused between tick `before_tick − 1` and `before_tick`. The pause itself changes nothing in the simulation; the replay only has to feed the `PauseResumed` flag that is already in `ticks[before_tick].cmd_bits`. |
| `tolerance` | object | Max absolute difference allowed for `x`, `y`, `z` (1e-4 m) to absorb float vs double. `lane`, `occupied_lane`, `state` and event types must match exactly. |
| `outcomes` | object | Expected I9 outcome counters at the end of the trace (only non-zero ones). A still-pending buffered Jump at the end counts as `Buffered`. |
| `ticks` | list | One entry per simulated tick, in order, starting at tick 0. |

Each `ticks[]` entry is the state **after** that tick's `Step`:

| Field | Type | Meaning |
|---|---|---|
| `t` | int | Tick index (`ITimeSource.Tick` value the step ran with). |
| `cmd` | list of string | Command flag names fed on this tick (`MoveLeft`, `MoveRight`, `Jump`, `Slide`, `CompanionAssist`, `PauseResumed`). |
| `cmd_bits` | int | Same as a byte: `MoveLeft = 1`, `MoveRight = 2`, `Jump = 4`, `Slide = 8`, `CompanionAssist = 16`, `PauseResumed = 32`. Feed this to `Step`. |
| `lane` | int | `TargetLane` (0 = left). |
| `occupied_lane` | int | `OccupiedLane` (nearest lane center to `X`, tie to `TargetLane`). |
| `x` | float | Lateral position `X` (m), lane center = `(lane − 1) × 2.4`. |
| `y` | float | Height `Y` above the track surface (m). |
| `z` | float | Distance (m). |
| `state` | string | `Running`, `Sliding`, `Airborne`, `FastFalling`, `Coyote`, `Falling` or `Dead`. |
| `hitbox_h` | float | Hitbox height used for collisions this tick (1.8 or 0.8). |
| `events` | list | Events emitted on this tick, in emission order: `{type, ...payload}`. Payload keys follow spec 001 section 11 (`from`, `to`, `dir`, `reversal`, `queued`, `fromSlide`, `coyote`, `buffered`, `airTicks`, `wasFastFall`, `restart`, `reason`, `row`, ...). Types and order are binding; payload values are informative. |

### Replaying in a C# EditMode test (sketch)

```csharp
// For each golden file:
var doc = GoldenTrace.Load(path);                       // JSON -> plain C# record (test helper)
var config = RunnerConfigFactory.FromGolden(doc);       // assert doc.derived == config ticks first
var sim = new RunnerSimulation(config, doc.TrackQuery(), new FixedSpeed(doc.speed.fixedSpeedMps));
foreach (var row in doc.ticks)
{
    sim.Step((InputCommand)row.cmd_bits);
    Assert.That(sim.State.TargetLane, Is.EqualTo(row.lane), $"tick {row.t}");
    Assert.That(sim.State.X, Is.EqualTo(row.x).Within(doc.tolerance.x), $"tick {row.t}");
    Assert.That(sim.State.Y, Is.EqualTo(row.y).Within(doc.tolerance.y), $"tick {row.t}");
    Assert.That(sim.State.Locomotion.ToString(), Is.EqualTo(row.state), $"tick {row.t}");
}
```

The names above are placeholders for whatever the gameplay-engineer's runner exposes; only the file format is
fixed here.

### Traces

| File | Covers |
|---|---|
| `01_lane_switch_and_reverse.json` | 7-tick switch, first X change on the command tick, reversal from current X (L5) |
| `02_double_lane_queue_bump_cancel.json` | Double swipe queue (starts tick 4, arrives tick 10 = 11 ticks), third swipe Bumped (L7), opposite swipe cancels the queue (L4) |
| `03_jump_buffered_and_expired.json` | Jump arc (apex 1.5 m at n = 18, land at n = 36), buffered jump 9 ticks early fires on landing, 10 ticks early expires |
| `04_coyote_jump.json` | Leave tick, Coyote state, jump on e + 5 is a full coyote jump over a gap |
| `05_fastfall_into_slide.json` | Fast-fall in ≤ 6 ticks, slide on landing (39 ticks), lane move while sliding, slide restart |
| `06_pause_resume.json` | `PauseResumed` clears buffer and lateral queue; arc and active move continue |

## Determinism notes

- The model uses no randomness. Courses and bots use `random.Random(seed)`; that is tooling only. Golden traces
  store explicit per-tick commands, so C# never needs to reproduce Python's generator.
- The model uses Python floats (double). If the C# runner uses `float` for `X`/`Y`, differences stay far below
  the 1e-4 m tolerance over these trace lengths.
