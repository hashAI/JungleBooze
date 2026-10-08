# ADR 0002: Deterministic simulation core

- Status: Accepted; input format amended by [ADR 0006](0006-steering-input-format.md) (2026-10-09)
- Date: 2026-10-06
- Deciders: tech-architect
- Hard to undo: **yes** (every gameplay system is written against this contract)

## Context

The plan depends on agents "playing" the game at scale: thousands of headless bot runs for balance, a solver for impossible patterns, fairness fuzzing, and exact bug reproduction from replays. That only works if a run can be reproduced exactly from a small amount of data, and if the same code runs on device, in tests and in batch mode. Unity's default patterns (`Update` with `Time.deltaTime`, `UnityEngine.Random`, physics callbacks) make results depend on frame rate and global state.

## Decision

1. **Contract:** a run is a pure function of *(build, platform, seed, input stream, config)*. Same inputs ⇒ identical simulation state at every tick, regardless of frame rate, device speed or hitches.
2. **Simulation is plain C#** with no UnityEngine dependency where possible (`JungleBooze.Core` has `noEngineReferences: true`; simulation code in Gameplay avoids UnityEngine types except plain math structs if needed). MonoBehaviours only render state.
3. **Three seams, all in Core:**
   - `IRandom`, implemented by `Pcg32Random` (PCG32 XSH-RR). Chosen over xorshift variants for better statistical quality at the same cost, tiny state (two `ulong`s), cheap stream selection (`Fork(streamId)`), and a published reference test vector we test against. One forked stream per subsystem so changes in one system do not shift another's sequence.
   - `ITimeSource`, implemented by `FixedStepTimeSource`: 60 Hz fixed step [ASSUMED], accumulator driven by real frame time, max 5 steps per frame, excess dropped. Simulation reads `Tick` and the constant `DeltaTime` only.
   - `IInputProvider` returning `InputCommand` flags per tick (superseded by ADR 0006: `ReadInput(tick)` returns `InputFrame` = commands + steering drag in mm; replay format 2). Touch, bot and replay providers are interchangeable; `RecordingInputProvider` records any of them. Recordings store the seed plus sparse (tick, command) frames.
4. **Banned in simulation code:** `UnityEngine.Random`, `System.Random`, `UnityEngine.Time`, `DateTime.Now`, physics engine queries/callbacks for gameplay outcomes, unordered collection iteration affecting results, static mutable state. Collision for gameplay uses simple analytic checks (lane + AABB/capsule in track space) inside the simulation.
5. **Presentation** reads simulation state and interpolates with `InterpolationAlpha`; it never feeds back into the simulation.

## Floating point

Simulation uses `float`. IL2CPP on ARM64 and the Mono/x64 editor both use IEEE-754 single precision, but the compilers can differ in fused multiply-add contraction and intermediate precision, and `System.Math`/`Mathf` transcendental functions can differ between platforms. Therefore:

- Guaranteed: determinism **within one build on one platform** (device replays on device; editor/CI sims in editor/CI). This is what bug reproduction and balance simulations need.
- Not guaranteed: bit-identical results between editor (x64) and device (ARM64). Balance numbers from editor sims are statistically valid for device, but a device replay may diverge slightly in the editor.
- If cross-platform replays become necessary (e.g. server-validated leaderboards), switch distance/position math to fixed-point (`long`, e.g. 1/1000 m units). That is a contained change behind the same interfaces, and would get its own ADR.
- Simulation code avoids transcendental functions (`Sin`, `Pow`, `Exp`) in outcome-relevant paths where possible; curves come from precomputed tables in config.

## Options considered

| Option | Verdict |
|---|---|
| Unity physics + `Update`/`FixedUpdate` | Rejected: physics is not guaranteed deterministic, results tied to frame timing, cannot run headless at high speed. |
| `System.Random` with seed | Rejected: implementation is not specified to be stable across .NET runtimes; no cheap independent streams. |
| xorshift128+ / xoshiro | Viable; PCG32 chosen for the reference test vector, stream support and simpler API. Either would satisfy the contract. |
| Variable timestep with `deltaTime` | Rejected: results depend on frame rate. |
| Fixed-point everywhere now | Deferred: more effort and less readable code; not needed for the current goals. |

## Consequences

- Positive: bots, replays, headless sims, and EditMode tests use the real gameplay code; bug reports carry a replay; balance tuning is reproducible.
- Negative: gameplay engineers cannot use Unity physics or `Time.deltaTime` for gameplay; views need interpolation code; discipline is enforced by review (and later an analyzer).
- Verification: EditMode tests in `Assets/_Game/Tests/EditMode/` prove same seed → same sequence, the PCG32 reference vector, stream independence, identical results under smooth, jittery and 30 fps frame pacing, and record → replay equality.
