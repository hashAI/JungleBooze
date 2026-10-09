# iPhone 12 readiness: painterly hero basin (2026-10-09)

Owner: performance-engineer. Status: **offline estimate + M4 calibration; no iPhone 12 measurement yet.**
The floor is iPhone 12 (A14), with a hard requirement of 60 fps (design/DECISIONS.md, 2026-10-09). Budgets are in
docs/ARCHITECTURE.md section 10.

## Verdict
- **Painterly v4** (the scene tech-architect has in progress in `main`, built 20:20, not committed): **likely 60 fps
  on iPhone 12 at full clocks, but the sustained margin is thin.**
  - Estimated GPU time: 9–11 ms in landscape and 10.5–13 ms in portrait, against a 12 ms budget and a 16.7 ms frame.
  - After 10 minutes of thermal throttling (about 0.8x GPU clock): about 11–13.5 ms landscape and 13–16 ms portrait.
  - Headroom to the 16.7 ms frame: **landscape about 35–45 %, portrait about 25–35 %.** Headroom to the 12 ms
    budget: landscape 10–25 %, portrait −7 to +12 %.
- **Painterly v3** (built 19:10, before the painted cards): **not ready.** Estimated 17.6–21.5 ms on A14, about 45–55
  fps. Most of the difference is the 2.5 layers per pixel of mist that v4 removed.
- CPU, draw calls, triangles, memory and GC allocations are well inside budget. **The GPU is the only risk, and the
  cause is alpha-tested foliage** (3.06 shaded layers per pixel in landscape, against a 0.35 budget).

## How the numbers were made
1. **Offline audit** (`JungleBooze.Editor.Perf.PerfAudit.Run`) of `HeroBasin_Painterly.unity` at the iPhone 12
   internal resolution of 1918x886 (1.7 MP, render scale 0.758). It counts every draw's shaded fragments as an Apple
   tile-based GPU shades them:
   - opaque: only the pixels that survive hidden-surface removal
   - alpha-tested: every fragment that passes depth, front to back (upper bound)
   - blended: every fragment

   It also compiles every shader variant in view for Metal/iOS and counts ALU operations and samples.
2. **Calibration run** on this Mac (M4, 10-core GPU). The same bench player is built for macOS and run uncapped
   (`-jbUncapped`) at the same internal resolution with the same URP asset (HDR, MSAA 4x, 2048 soft shadows,
   bloom, LUT). Its frame time is the M4's full-clock cost:

   | Scene | Landscape | Portrait |
   |---|---|---|
   | v3 | 4.88 ms | 5.12 ms |
   | v4 | **2.43 ms** | **2.91 ms** |

   CPU main thread was 0.8–1.8 ms in this development build.
3. **A14 = M4 × 3.6 to 4.4.** That is the ratio of GPU throughput (10 cores at about 1.6 GHz against 4 at about
   1.1 GHz) and of public Metal benchmark scores, plus a 0.8 factor for sustained thermals. Range ±20 %.
   `tools/perf/gpu_estimate.py` gives the per-layer split. Its absolute values run about 2x high on alpha-tested
   foliage, because it counts fragments that are discarded early, so use it for shares only.

**Not verified:** real A14 timings, iPhone thermals over 10 minutes, iOS memory footprint, and frame pacing. These
come from the device run below.

## Hero view numbers (v4)
| | Landscape | Portrait |
|---|---|---|
| Draws (main / shadow) | 44 / 5 | 42 / 5 |
| SetPass, measured on M4 incl. shadow and post (budget 40) | 34 | 34 |
| Shader variants in view | 10 | 10 |
| Triangles (main + Pista / shadow) | 236k + 20k / 58k | 232k + 20k / 58k |
| Shaded layers per pixel: opaque / alpha-tested / sky / blended | 0.58 / **3.06** / 0.12 / 1.28 | 0.52 / **1.65** / 0.15 / 1.20 |
| Shaded layers per pixel: p50 / p90 / p99 / max | 4 / 11 / 26 / 61 | 3 / 7 / 12 / 37 |
| Largest costs (model share of fragment time) | canopy cards: L3 kit crowns 29 %, L1 dressing 29 %; fronds 8 % | near-lens fronds (L1 dressing) 40 %; mist 10 % |

Heat maps (1 layer = blue, 4 = yellow, 8 = red, 12+ = white):
- `2026-10-iphone12-overdraw-landscape.jpg`: the white band is the canopy-card treeline.
- `2026-10-iphone12-overdraw-portrait.jpg`

**Fragment shader cost** (Metal, scalar ALU operations / texture samples + shadow compares per pixel):

| Shader | ALU ops | Samples |
|---|---|---|
| Nature Lit painterly, alpha-tested + wind | 544 | 7 + 9 |
| Nature Lit painterly, opaque | 503 | 5 + 9 |
| Nature Lit with layers | 573 | 11 + 9 |
| Character Painterly | 422 | 4 + 9 |
| Water | 382 | 5 + 1 |
| Painted Card | 129 | 3 |
| Painted Sheet | 109 | 2 |
| Backdrop Card | 126 | 2 |
| Atmos Card (mist) | 156 | 3 |

The 9 shadow compares are URP medium soft shadows (a 5x5 tent filter). Height fog with sun in-scatter adds four
exp2/log2 calls per pixel.

**Memory and size:**
- Textures: 64 textures, 94 MB on iOS, all ASTC (4x4 to 8x8) except two:
  - the sky HDRI cube is RGB9E5 1024² x 6, 32 MB
  - CanopyDapple is R8, 0.1 MB
- Meshes: 39 meshes, 49 MB. 38 of them keep a read/write CPU copy, 24 MB that could be freed.
- Bench build: 318 MB uncompressed (a development build; its UnityFramework binary is 125 MB).
- Shader stage variants after stripping: 2,296, of which URP UberPost is 961. Project shaders: Nature Lit 60,
  Character 10, Water 6, Waterfall 6, others ≤ 4.

**Expedition (gray-box):** 1.8 ms GPU on M4 at a 60 fps cap, about 230 draws, 20 SetPass, 50k tris. No concern.

## Recommended cuts (ranked; none changes the reviewed look unless marked)
1. **Sort the merged foliage batches front to back for the hero camera** (builder, perf-only). This applies to the
   triangle order inside each canopy, fronds or dressing batch. Alpha-tested foliage cannot use hidden-surface
   removal, so draw order decides how many hidden leaves are shaded. Expected: −20 to −40 % of foliage fragments.
   No visual change.
2. **Tighter canopy and frond cards** (asset-pipeline). Trim each card mesh to its leaf silhouette, and use an
   opaque leaf core with alpha test only at the rim. That reduces the discarded area that still costs a full
   544-op shader. Expected: −20 to −30 % of foliage fragments.
3. **Mark generated meshes non-readable** (builder, perf-only: `Mesh.UploadMeshData(true)` before saving).
   −24 MB of memory.
4. **Sky HDRI cube to ASTC HDR 6x6 or 512² faces** (importer setting, perf-only). −28 MB. The painted backdrop
   covers almost all of the sky.
5. **Strip unused URP post-processing variants** (URP Global Settings). Smaller build and faster loading.
6. **[look change, owner or tech-architect decision]** Foliage only: hard or low (4-tap) shadows instead of medium
   (9-tap), and fog evaluated per vertex. About −25 % of the foliage shader cost. On painterly leaves the
   difference is hard to see.
7. Fallback if the device run misses: an internal resolution of 1.5 MP on High instead of 1.7 MP (−12 % of
   fragment cost). **This is a look change.**

Items 1 to 3 would bring portrait from 10.5–13 ms down to an estimated 8.5–10.5 ms.

## Quality tiers (ADR 0010)
- **High** = the reviewed look on iPhone 12 and newer. `URP-Realistic` is unchanged. LOD bias is 2, as in every
  editor capture; iPhone builds previously used the "Medium" level with LOD bias 0.7.
- **Low** = MSAA 2x, a 1024 shadow map, 30 m shadow distance, low soft shadows, 1.2 MP, and the
  `HeroTierContent` layers off.
- The switch runs at start-up (`QualityTiers`). Every scene is capped at 60 fps. The hero scene used to run at
  iOS's default 30 fps.

## Owner steps for the device run (free Apple ID works)
1. **Plug in** the iPhone 12 with a cable and unlock it. On the phone, tap **Trust** and enter your passcode.
2. **Trust and developer mode.** Open Xcode, then Window > Devices and Simulators, and wait until the phone shows
   up. On the phone, open Settings > Privacy & Security > **Developer Mode**, turn it on, and restart the phone when
   it asks.
3. **Sign in to Xcode.** Go to Xcode > Settings > Accounts, click +, choose Apple ID, and sign in with your Apple ID
   (a free one is fine). Xcode creates "Your Name (Personal Team)".
4. **Select the team.** `tools/build/device_bench.sh` finds it automatically. If it stops and prints "Finish in
   Xcode": open the printed `Unity-iPhone.xcodeproj`, go to target Unity-iPhone > Signing & Capabilities, and pick
   your Personal Team.
5. **Run.** In Terminal, run `tools/build/device_bench.sh` (about 5 minutes on this Mac). The first time, iOS
   blocks the app: go to Settings > General > VPN & Device Management > your Apple ID > **Trust**, then tap
   "Aurelia Bench".

   The benchmark runs for about 15 minutes on its own. Unplug the cable, take the case off, and set brightness to
   about half. When the phone shows "BENCH DONE", plug it back in and run
   `tools/build/device_bench.sh --pull`. Send the printed folder.

## Reproduce
- Device build: `tools/build/device_bench.sh [--release] [--export-only] [--pull] [--clean]`.
- Offline audit: `Unity -batchmode -projectPath UnityProject -executeMethod JungleBooze.Editor.Perf.PerfAudit.Run
  -jbPerfOut <dir> -quit`, then `python3 tools/perf/gpu_estimate.py <dir>`.
- M4 calibration: `-executeMethod JungleBooze.Editor.Build.DeviceBenchBuild.BuildMac -jbOutput <dir>/AureliaBench.app`,
  then run the app with `-screen-width 2532 -screen-height 1170 -screen-fullscreen 0 -jbBenchSeconds 30 -jbUncapped`.
  Close other GPU work first: a second Unity rendering at the same time doubled the times in one run.
