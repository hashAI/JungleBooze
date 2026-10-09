# ADR 0010: Device quality tiers and the iPhone benchmark

- Status: Accepted (performance-engineer, 2026-10-09). Tier thresholds are provisional until the iPhone 12 run.
- Deciders: performance-engineer
- Hard to undo: **no.** Two quality levels, one extra URP asset and one Resources config. Deleting them brings back
  one look for every device.

## Context
The owner adopted the painterly style with a hard requirement: 60 fps on iPhone 12 (A14), see design/DECISIONS.md
(2026-10-09). The owner's rule also says no visual trade-downs for the reviewed look. Before this ADR:

- The project still had Unity's six default quality levels. The editor used "Ultra" (LOD bias 2, forced anisotropic
  filtering), but iPhone builds defaulted to "Medium" (LOD bias 0.7). So the phone culled plants earlier than every
  editor capture the owner reviewed.
- Scenes without their own root (the hero basin) ran at iOS's default 30 fps. Nothing set the render scale either,
  so they drew 2.96 MP with MSAA 4x on an iPhone 12 instead of the 1.7 MP from ADR 0004.

## Decisions
1. **Two quality levels, `Low` (0) and `High` (1)** (`QualityTierSetup`, menu *JungleBooze > Perf > Set Up Quality
   Tiers*; it runs again on every bench build).
   - **High** is the former "Ultra" level, unchanged, with no pipeline override. It renders with the Graphics default
     `URP-Realistic`, exactly as in editor captures.
   - **Low** is the former "Medium" level with LOD bias 1 and the pipeline override `URP-Realistic-Low`. That asset
     is a copy of URP-Realistic, refreshed on every setup run, with MSAA 2x, a 1024 shadow map, a 30 m shadow
     distance and low soft-shadow quality.
   - Every platform defaults to High. `URP-Realistic` itself is never modified.
2. **Run-time switch** (`App/Perf/QualityTiers`, before the first scene loads). It reads
   `Config/Perf/Resources/QualityTiers.asset`, caps every scene at 60 fps, and classifies the device with
   `Core/Perf/DeviceTierRules`:
   - High: iPhone generation 13+ (iPhone 12 / A14 and newer, SE 3rd gen) and iPad generation 13+.
   - Low: older devices, and any device with under 3.5 GB of memory.
   - Desktop and unknown devices are High.

   Then it selects the level and sets the render scale for a fixed internal pixel count: High 1.7 MP (iPhone 12:
   scale 0.758, 1918x886), Low 1.2 MP. In the editor only the 60 fps cap applies, so captures stay as they are.
   Content that is High-only (ADR 0011 `HeroTierContent`) reads `QualityTiers.Current`.
3. **Device benchmark** (`tools/build/device_bench.sh`, `Editor/Build/DeviceBenchBuild`, `App/Perf/DeviceBenchRunner`).
   - Bootstrap scene `Scenes/Benchmark/DeviceBench.unity` (generated), followed by HeroBasin_Painterly and Expedition.
   - Phases come from `Config/Perf/DeviceBenchConfig.asset`: hero landscape 60 s, hero portrait 60 s, Expedition bot
     90 s landscape and 60 s portrait, then a 600 s soak on the hero landscape view.
   - Measures:
     - frame time p50/p95/p99 from a 0.1 ms histogram
     - CPU main/render and GPU ms (FrameTimingManager)
     - draws, SetPass, triangles and GC allocations (development builds)
     - thermal state, physical footprint and available memory (`Plugins/iOS/JBPerfNative.mm`)
     - battery level and Low Power Mode
   - Writes one CSV row per second plus a per-phase summary to `Documents/bench`. File sharing is on in bench builds
     only, so the logs show in Finder.
   - Signing: automatic signing with the team found on the Mac. A free Apple ID works.
4. **Offline audit** (`Editor/Perf/PerfAudit`). Covers overdraw per draw at the internal resolution (as a TBDR GPU
   shades it), Metal ALU and sample counts per shader variant, textures (iOS formats and memory) and meshes.
   `tools/perf/gpu_estimate.py` turns these into an A14 estimate.
   **Calibration:** a Mac player of the same bench with `-jbUncapped` gives the M4 full-clock frame time; scale it by
   about 3.6 to 4.4 for A14.

## Consequences
- iPhone builds now render the High look the owner reviewed (LOD bias 2, not 0.7). That costs GPU time on device
  compared with the old Medium default, but it is the look that was approved.
- A 60 fps cap applies to every scene. FeelTest, Expedition and LookTest already set it.
- GPU ms read under a 60 fps cap includes clock scaling: the GPU slows down when it has slack. Read headroom from
  missed frames and the thermal state on device, or from an uncapped desktop run.
- `LookTestPipelineSetup.RestoreMobile` only touches the current level's override. High has none, so it behaves as
  before.
