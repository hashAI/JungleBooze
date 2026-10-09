# JungleBooze Architecture

Owner: tech-architect. Status: **Week 0 baseline**. Changes to anything marked "hard to undo" need an ADR in `docs/adr/`.

This document is also the template for later games in the portfolio: keep game-specific details in clearly marked sections.

Items marked `[ASSUMED]` are defaults the team proceeds with until the owner or a later ADR says otherwise.

---

## 1. Goals

1. **Deterministic, testable gameplay.** The whole run is a pure function of *(seed, input stream, config, build)*. Bots, replays and balance simulations reuse the exact game code.
2. **Hard mobile budgets.** 60 fps on the floor iPhone, zero per-frame GC allocations during a run (section 10).
3. **Swappable services.** Ads, IAP, analytics, save, Game Center and haptics sit behind interfaces so tests, bots and the editor run without SDKs.
4. **Boring technology.** Unity 6 LTS, URP, UPM packages from Unity, plain C#. Every extra dependency must be approved by tech-architect and recorded in `docs/LICENSES.md`.

---

## 2. Engine and project

| Item | Choice | Notes |
|---|---|---|
| Engine | Unity 6 LTS, stream 6000.3 | ADR 0001. Pinned in `UnityProject/ProjectSettings/ProjectVersion.txt`. Upgrade only to newer patches of the same LTS stream, in a dedicated PR. |
| Render pipeline | URP, mobile-tuned | ADR 0001. First Playable: `URP-Mobile` (no realtime shadows, blob shadow). **AURELIA realistic look: `URP-Realistic` per ADR 0004** (HDR, ACES, one-cascade soft sun shadows, MSAA 4x, no lightmaps). |
| Scripting backend | IL2CPP, ARM64 only | Required for iOS. |
| API compatibility | .NET Standard 2.1 | Smallest surface; enough for Core. |
| C# version | C# 9 (Unity's compiler) | No file-scoped namespaces, no records in serialized types. |
| Input | Input System package (Enhanced Touch) | Only the touch adapter talks to it (section 5.3). |
| Content loading | Addressables | For content loaded after boot (worlds, skins, audio banks). Boot scene and first-run content ship in the player. |
| UI | uGUI for HUD/menus [ASSUMED] | UI Toolkit runtime is an option; ui-engineer may propose an ADR. |
| Minimum iOS | iOS 15.0 | ADR 0003. Unity 6000.3 supports iOS 13+; Apple announced iOS 15+ from April 2027; floor device runs iOS 26. |

### 2.1 Project settings applied on first editor open (ADR 0003)

The repository does not contain hand-written `ProjectSettings/*.asset` files or scenes. On the first open, the editor
script `Assets/_Game/Editor/Setup/ProjectBootstrap.cs` applies the settings below once (menu
`JungleBooze > Setup > Run Project Setup` re-runs it). The files Unity then writes must be committed in one PR.

Applied by the script:
- Editor: Asset Serialization = Force Text, Version Control = Visible Meta Files.
- Player: Active Input Handling = Both (restart required). Product name `Jungle Runner` and iOS bundle id
  `com.pistaduko.junglerunner` (placeholders [ASSUMED]; never the word "booze"). Portrait, iPhone only,
  minimum iOS 15.0, Color Space = Linear.
- Graphics: creates and assigns `Assets/_Game/Config/Rendering/URP-Mobile.asset` (+ `URP-Mobile-Renderer.asset`)
  with the ADR 0001 mobile settings (Forward, HDR off, MSAA 2x, no realtime shadows, no extra lights, no
  depth/opaque textures, SRP Batcher on). Fallback menu: `JungleBooze > Setup > Fallback: Use Built-in Renderer`.
- Scenes: creates the empty `Assets/_Game/Scenes/Run.unity` and makes it the first enabled build scene.

Unity defaults that already match (not touched): IL2CPP + ARM64 + Metal on iOS, .NET Standard 2.1, Incremental GC.

Still manual / deferred (Week 1, after FP1):
- Quality: a single "Mobile" quality level; delete the others.
- Managed Stripping Level = Medium once `link.xml` is in place; "Prebake Collision Meshes" on.
- Enter Play Mode Options: enabled, domain reload off (forbids static mutable state; enable after code review
  confirms there is none).
- Time: Fixed Timestep is **not** used by gameplay (gameplay uses `FixedStepTimeSource`); leave physics at default
  and do not rely on `FixedUpdate` for simulation.
- Frame rate: `Application.targetFrameRate = 60` set by the composition root (ProMotion devices would otherwise
  render at up to 120 Hz and spend battery and thermal headroom).

---

## 3. Layers and assemblies

```
                    ┌──────────────────────────────┐
                    │ JungleBooze.App              │  composition root, boot, scene flow
                    └──┬───────────┬───────────┬───┘
                       │           │           │
          ┌────────────▼──┐  ┌─────▼──────┐  ┌─▼──────────────────┐
          │ JungleBooze.UI│  │            │  │ JungleBooze.Services│  SDK adapters (ads, IAP, ...)
          └──┬─────────┬──┘  │            │  └─────────┬──────────┘
             │         │     │            │            │
             │   ┌─────▼─────▼──────────┐ │            │
             │   │ JungleBooze.Gameplay │ │            │
             │   └─────────┬────────────┘ │            │
             │             │              │            │
          ┌──▼─────────────▼──────────────▼────────────▼──┐
          │ JungleBooze.Core   (no UnityEngine)            │
          └───────────────────────────────────────────────┘
```

| Assembly | Folder | References | Purpose |
|---|---|---|---|
| `JungleBooze.Core` | `Scripts/Core` | none (`noEngineReferences: true`) | Deterministic primitives (`IRandom`, `ITimeSource`, `IInputProvider`), events, state machines, service **interfaces**, save data model and migrations. Pure C#, runs in any .NET. |
| `JungleBooze.Gameplay` | `Scripts/Gameplay` | Core, Unity.InputSystem | Simulation (runner, lanes, track generation, obstacles, vine swing, power-ups, companion) as plain C#; thin MonoBehaviour "views" that render simulation state; touch and bot input providers. |
| `JungleBooze.UI` | `Scripts/UI` | Core, Gameplay, Unity.InputSystem | HUD, menus, shop, settings, onboarding. Reads Gameplay state, calls Core service interfaces. |
| `JungleBooze.Services` | `Scripts/Services` | Core (+ SDK assemblies later) | Concrete adapters for ads, IAP, analytics, save, Game Center, haptics, plus null/fake implementations for editor and tests. |
| `JungleBooze.App` | `Scripts/App` | Core, Gameplay, UI, Services | Composition root: builds the object graph, owns the run driver and scene flow. The only assembly that knows concrete service types. |
| `JungleBooze.Editor` | `Editor` | all runtime assemblies | Editor tools, validators (asset budgets), batch-mode entry points (headless simulations, build scripts). Editor-only. |
| `JungleBooze.Tests.EditMode` | `Tests/EditMode` | Core, Gameplay, Services, test runner | Fast unit tests of plain C# logic. Editor-only. |
| `JungleBooze.Tests.PlayMode` | `Tests/PlayMode` | all runtime assemblies, test runner, Performance Testing | Scene behavior, frame loop, allocation and performance tests. |

Rules:
- References only point **down** the diagram. Gameplay never references UI, App or Services; Services never references Gameplay or UI.
- All runtime assemblies have `autoReferenced: false`, so nothing in `Assembly-CSharp` can quietly depend on them. Scripts outside `_Game` (`Assembly-CSharp`) are not allowed.
- `JungleBooze.App` was added beyond the original assembly list in the project rules, because the composition root must see concrete types from every layer without making any layer depend on another sideways. The assembly list in the project rules should be updated to include it.
- Each assembly has an `AssemblyInfo.cs` exposing internals to the test assemblies only.
- Namespaces follow `JungleBooze.<Layer>` (sub-namespaces allowed, e.g. `JungleBooze.Gameplay.Track`). Folders inside Core (`Random/`, `Time/`, `Input/`) do not add namespace segments.

### 3.1 Folder layout

```
UnityProject/
  Assets/
    csc.rsp                       compiler options (warnings as errors)
    _Game/
      Scripts/{Core,Gameplay,UI,Services,App}
      Config/                     ScriptableObject assets (tuning, rendering, audio mix)
      Prefabs/  Scenes/  Art/  Audio/
      Editor/                     JungleBooze.Editor
      Tests/{EditMode,PlayMode}
  Packages/manifest.json
  ProjectSettings/
```

Third-party SDKs installed as UPM packages stay in `Packages/`. Any SDK that must live in `Assets/` goes in `Assets/ThirdParty/<Vendor>/` and is never edited.

---

## 4. Data flow of one frame during a run

```
 Unity Update (App/RunDriver, the only per-frame entry point)
   │
   ├─ steps = time.Accumulate(Time.unscaledDeltaTime)     real time → whole fixed steps
   │
   ├─ repeat steps times:                                 SIMULATION (deterministic)
   │     cmds = input.ReadCommands(time.Tick)             touch / bot / replay
   │     simulation.Step(cmds)                            uses IRandom streams, config SOs, time.DeltaTime
   │     events → ring buffer (coin picked, hit, near-miss)
   │     time.Step()
   │
   ├─ presentation.Sync(simulation state, time.InterpolationAlpha)   VIEWS (non-deterministic, visual only)
   │     move pooled GameObjects, play VFX/SFX/haptics for buffered events
   │
   └─ UI reads state snapshot (score, coins, distance)   UI
```

- **Simulation → presentation is one-way.** Views never write simulation state. Cosmetic randomness (particle variation, idle animations) uses its own `IRandom` stream or Unity's random, never the simulation streams.
- **Events** from simulation to presentation go through a pre-allocated ring buffer of structs (no C# `event` delegates in the hot path, no boxing).
- **Services are called at the edges**, never from inside `Step()`: e.g. analytics is sent when the run ends, haptics are triggered by the presentation layer reading buffered events.

---

## 5. Deterministic core (ADR 0002)

Contract: **same build + same platform + same seed + same input stream + same config ⇒ identical run**, independent of frame rate, device speed and frame hitches.

### 5.1 `IRandom` (`Core/Random`)
- Implementation: `Pcg32Random` (PCG32 XSH-RR, 64-bit state). Verified against the reference C implementation's published test vector in EditMode tests.
- API: `NextUInt`, `NextInt(min, maxExclusive)` (unbiased), `NextFloat()` in [0,1), `NextFloat(min, max)`, `Chance(p)`, `Fork(streamId)`.
- **One stream per subsystem.** The run creates a root generator from the run seed and forks fixed stream ids at setup: track generation, obstacle variants, pickups, bot, cosmetic. Adding a random call in one subsystem must not change any other subsystem's sequence. Stream ids are constants in one file and are never renumbered.
- Banned in simulation code: `UnityEngine.Random`, `System.Random`, `Guid.NewGuid`, `DateTime.Now`, hash codes of reference types, iteration over `Dictionary`/`HashSet` where order affects results. code-reviewer enforces this; an analyzer rule will follow (section 12).

### 5.2 `ITimeSource` (`Core/Time`)
- Implementation: `FixedStepTimeSource`, 60 steps/s [ASSUMED], max 5 steps per rendered frame (excess time after a hitch is dropped, not simulated).
- Simulation reads only `Tick`, `DeltaTime` (constant) and `ElapsedSeconds` (= `Tick * step`, no drift). `UnityEngine.Time` is banned in simulation code.
- `InterpolationAlpha` is for views only, to smooth motion between the last two simulation states on 120 Hz ProMotion displays or when frames are uneven.
- Headless simulations do not call `Accumulate`; they call `Step()` in a tight loop as fast as the CPU allows.

### 5.3 `IInputProvider` (`Core/Input`)
> **Amended by [ADR 0006](adr/0006-steering-input-format.md) (2026-10-09):** `ReadInput(tick)` returns `InputFrame { InputCommand Commands; short LateralDeltaMm; }`; commands are `Jump`, `Slide`, `DodgeLeft`, `DodgeRight`, `TouchBegan`; replay format version 2. The lane-era text below is kept for history.

- `InputCommand` is a `[Flags] byte` enum matching GDD section 5.1: `MoveLeft`, `MoveRight` (swipe left/right), `Jump` (swipe up), `Slide` (swipe down), `CompanionAssist` (double tap). Commands are intents; the simulation interprets them by context (e.g. `Jump` while swinging releases the vine, `Slide` in the air fast-falls). The 150 ms input buffer and 80 ms coyote time from the GDD live in the simulation, not in the provider, so bots and replays get them too. Values are persisted in replays: append only, never renumber.
- `ReadCommands(tick)` is called exactly once per simulation step with increasing ticks and must not allocate.
- Implementations:

| Provider | Assembly | Use |
|---|---|---|
| `TouchInputProvider` | Gameplay | Device. Reads Enhanced Touch in `Update` and feeds a plain C# `GestureRecognizer` (EditMode-tested with timestamped samples, per GDD 5.2). Recognized gestures go into a fixed-size queue; `ReadCommands` returns one per tick so two fast swipes are never merged. Thresholds (28 pt, 250 ms, re-arm distance, double-tap window) come from a ScriptableObject. |
| `BotInputProvider` | Gameplay | Simulations. Reads simulation state, applies reaction delay and error rate from a skill profile ScriptableObject, uses its own `IRandom` stream. |
| `ReplayInputProvider` | Core (done) | Plays back an `InputRecording` (seed + sparse list of tick/command frames). |
| `RecordingInputProvider` | Core (done) | Decorator that records any provider. Always on in development builds so every bug report carries a replay. |

- A replay file = header (format version, build version, seed, config hash) + frames. A replay is only guaranteed valid for the same build and platform (see ADR 0002 on floating point).

---

## 6. Composition root and dependency injection

- **Hand-rolled composition root** [ASSUMED] in `JungleBooze.App`: a `Boot` scene contains one `AppRoot` MonoBehaviour that constructs services and passes them through constructors. No DI container in Week 0; VContainer is the approved upgrade path if the graph grows past what is comfortable by hand (needs an ADR).
- **No singletons, no static mutable state, no `FindObjectOfType`, no service locator** in game code. Static readonly constants and pure static helpers are fine.
- MonoBehaviours that need dependencies receive them through an `Init(...)` method called by the root or by a factory, never by looking them up.
- Lifetime scopes: **App** (services, save, config) → **Session/Menu** → **Run** (simulation, pools, run-scoped streams). A run scope is created and disposed per run.

---

## 7. Services (interfaces in Core, adapters in Services)

All interfaces are async-friendly but allocation-light, work offline, and have a `Null`/fake implementation used in editor, tests and bots. Sketch (final signatures are set by the implementing agent and reviewed here):

| Interface | Responsibilities | Adapter (planned) | Notes |
|---|---|---|---|
| `IAdsService` | Load/show rewarded and interstitial, availability, frequency caps hook | Ad mediation SDK (G5 decision) | Never shown in first session. Rewarded ads opt-in only. Must respect ATT + consent state. |
| `IConsentService` | ATT prompt, GDPR/UK consent, current consent state | ATT via iOS plugin + consent SDK | Must run before any tracking SDK initializes. |
| `IPurchaseService` | Product catalog with localized prices, purchase, restore, entitlement state | Unity IAP (StoreKit) | Prices always from StoreKit. Restore Purchases required. |
| `IAnalyticsService` | Log typed events (`RunEnded`, `PurchaseCompleted`, ...) | TBD | Events are structs, batched; no PII. Must match App Privacy labels. |
| `ISaveService` | Load/save `SaveData`, atomic write, migrations, iCloud sync hook | File system + iCloud key-value/document | See section 9. |
| `ILeaderboardService` | Authenticate, submit score, show leaderboards/achievements | Game Center | Optional for play; no login wall. |
| `IHapticsService` | Play semantic haptics (`Light`, `Success`, `Failure`, ...) | Core Haptics via native plugin | Respects settings toggle. |
| `IRemoteConfigService` | Optional overrides for tuning values | TBD (G5) | Defaults always ship in ScriptableObjects. |

Adding any SDK: tech-architect checks license, binary size impact, `PrivacyInfo.xcprivacy`, maintenance status and alternatives, records it in `docs/LICENSES.md`, and appstore-compliance checks it against `docs/APP_STORE_CHECKLIST.md`.

---

## 8. Configuration: ScriptableObjects

- All tuning lives in ScriptableObject assets under `Assets/_Game/Config/` (project rule 4): speeds, lane width, jump/slide timings, spawn tables, difficulty curve, economy prices, touch thresholds, bot skill profiles, pool sizes.
- Pattern: a ScriptableObject (`RunnerConfigAsset`) is the authoring wrapper; at run start it is converted into an immutable plain C# config (`RunnerConfig`) that the simulation uses. Core and simulation code never touch `ScriptableObject` types, so EditMode tests and headless sims construct configs directly.
- Configs carry `OnValidate` range checks and EditMode tests that load every config asset and validate it.
- A config hash (stable hash of serialized values) is stored in replays and sim reports.

---

## 9. Save data (hard to undo; ADR to follow in Week 4)

- `SaveData` is a plain C# class in Core with an integer `Version`. Serialized as JSON [ASSUMED] (readable, diffable, small).
- **Migrations:** one `ISaveMigration` per version step (`From = n`, `To = n+1`), applied in order on load. Every migration has an EditMode test with a fixture file of the old format. Fixture files are never deleted.
- **Atomic writes:** write to `save.json.tmp`, flush, then replace `save.json` (keep `save.json.bak` of the previous good file). On load: try main, then backup, then defaults. A corrupt save never crashes the game.
- **Unknown future versions** (downgrade) are loaded read-only and not overwritten.
- iCloud backup and conflict resolution (prefer most progress) is added in Week 4 behind `ISaveService`.
- No PII in saves. Purchases are re-derived from StoreKit entitlements, not trusted from the save file.

---

## 10. Performance and asset budgets

> **Floor device: iPhone 12 (A14), 60 fps, hard requirement** (owner, 2026-10-09, painterly style). The budgets below
> are those of [ADR 0004](adr/0004-realistic-look-on-mobile.md) Decision 7. They are measured on an iPhone 12 in a
> release build with the device benchmark (10.5). The latest numbers are in
> [docs/perf/2026-10-iphone12-readiness.md](perf/2026-10-iphone12-readiness.md).

Hard limits (project rule 6).

### 10.1 Runtime budgets (iPhone 12, internal resolution 1.7 MP)

| Metric | Budget | Latest (painterly hero basin v4, 2026-10-09) |
|---|---|---|
| Frame rate | 60 fps sustained over a 10-minute soak, p95 frame ≤ 18 ms, no thermal state above "fair" | not yet measured on device |
| GPU frame time | ≤ 12 ms (thermal headroom inside 16.7 ms) | estimate 9–11 ms landscape, 10.5–13 ms portrait (M4 full-clock run × 3.6–4.4) |
| CPU main thread | ≤ 8 ms | M4 dev build 0.8–1.8 ms → estimate ≤ 3.5 ms |
| CPU render thread | ≤ 8 ms | M4 0.7–1.2 ms |
| Draw calls, main view (SRP-batched) | ≤ 250 (shadow pass ≤ 100) | 42–44 + shadow 5 |
| SetPass calls | ≤ 40 | 34 (M4 counter, incl. shadow and post) |
| Triangles, main view | ≤ 350k (shadow pass ≤ 150k) | 232k–236k + Pista 20k; shadow ~58k (estimate) |
| Alpha-tested shaded fragments | ≤ 0.35 per pixel on average (35 % of the screen, one layer) | **3.06 landscape, 1.65 portrait (over)** |
| Blended overdraw | ≤ 1.5 layers per pixel on average | 1.28 landscape, 1.20 portrait |
| Texture memory, one biome | ≤ 150 MB | hero basin 94 MB (32 MB of it the sky HDRI cube) |
| Resident memory (physical footprint) | ≤ 1.0 GB (hard ceiling 1.2 GB) | not yet measured on device |
| GC allocations | 0 bytes per frame in game code | 0 (dev build; the bench overlay's 1 string per second excluded) |
| Cold start to interactive | ≤ 5 s | not measured |
| App download | ≤ 200 MB first install | bench app 318 MB uncompressed (dev build, 125 MB of it the dev binary) |

### 10.2 Asset budgets [ASSUMED initial values; asset-pipeline enforces with an editor validator]

| Asset | Budget |
|---|---|
| Hero character | ≤ 15k triangles, 1 material, ≤ 1024² texture set, ≤ 40 bones |
| Companion | ≤ 8k triangles, 1 material, ≤ 512² textures, ≤ 30 bones |
| Obstacle / prop | ≤ 2k triangles (LOD0), share atlases |
| Track chunk (environment) | ≤ 20k triangles visible, ≤ 4 materials, atlas ≤ 2048² |
| Textures | ASTC (6x6 default, 4x4 for hero/UI), mipmaps on for 3D, no uncompressed textures in builds |
| Audio | Music: streamed, AAC/Vorbis ~128 kbps; SFX: compressed in memory/ADPCM, mono unless stereo is needed |
| Shaders | URP Simple Lit / Unlit / custom mobile shaders only; strip unused variants |
| Animations | Optimized (keyframe reduction), humanoid only if retargeting is required |

### 10.3 Rules that keep budgets
- **Pool everything spawned** (track chunks, obstacles, coins, VFX, audio sources). Pools are pre-warmed at run start with sizes from config; growth during a run is logged as a warning in development builds.
- No `Instantiate`/`Destroy`, LINQ, string concatenation/formatting, boxing, closures or `foreach` over interfaces in per-frame code. UI text updates only when values change, using cached/non-allocating formatting.
- GPU instancing / SRP Batcher compatible materials; static batching for track chunk geometry.
- Addressables groups per world; the first world ships in the player.

### 10.4 Quality tiers ([ADR 0010](adr/0010-device-quality-tiers.md))

| | High (iPhone 12 / A14 and newer, iPad gen 13+, desktop) | Low (older or < 3.5 GB memory) |
|---|---|---|
| Unity quality level | `High` (the former "Ultra": LOD bias 2, forced anisotropic, unlimited skin weights) | `Low` (former "Medium", LOD bias 1) |
| URP asset | `URP-Realistic` (Graphics default, unchanged) | `URP-Realistic-Low`: MSAA 2x, 1024 shadow map, 30 m shadows, low soft shadows |
| Internal resolution | 1.7 MP (render scale 0.758 on iPhone 12) | 1.2 MP |
| Content | everything | `HeroTierContent` layers off (ADR 0011) |

`QualityTiers` applies the tier before the first scene loads and caps every scene at 60 fps. The editor keeps High.
Config: `Config/Perf/Resources/QualityTiers.asset`. Set-up: *JungleBooze > Perf > Set Up Quality Tiers*.

### 10.5 How performance is measured
- **Device benchmark**: `tools/build/device_bench.sh` (owner steps in the readiness report). About 15 minutes on the
  phone, on its own: hero basin landscape and portrait, Expedition bot, then a 10-minute soak. It logs one CSV row
  per second (frame p50/p95/p99, CPU/GPU ms, thermal state, footprint, battery, draws, GC) to `Documents/bench`.
  Get the logs with `--pull`.
- **Offline audit** (no phone): `JungleBooze.Editor.Perf.PerfAudit.Run`. It writes overdraw heat maps and per-draw
  shaded fragments, Metal ALU and sample counts per shader variant, texture formats and memory, and mesh memory. Then
  `tools/perf/gpu_estimate.py` turns them into an A14 estimate.
- **Calibration**: `DeviceBenchBuild.BuildMac`, run with `-jbUncapped -jbBenchSeconds 30`, gives the M4 full-clock
  frame time of the same build. GPU ms read under a 60 fps cap is inflated by clock scaling, so read device headroom
  from missed frames and the thermal state.

---

## 11. Testing and CI

- **EditMode** (`JungleBooze.Tests.EditMode`): all Core and simulation logic, config validation, save migrations, determinism (same seed → same sequence; jittery vs smooth frame pacing → identical result; record → replay → identical result).
- **PlayMode** (`JungleBooze.Tests.PlayMode`): scene wiring, frame loop, pools, allocation checks, performance measurements (Unity Performance Testing package).
- **Headless simulation** (Week 1+): batch-mode entry point in `JungleBooze.Editor` runs N seeded bot runs and writes JSON reports for balance-simulator.
- **Cloud compile check (no license):** `tools/ci/compile_check.sh` compiles every assembly with the real Unity 6000.3.25f1 assemblies and compiler (editor iOS, editor macOS, iOS player) and our shaders with DXC against URP's ShaderLibrary. Run it before handing over any C# or shader change. ADR 0005, `docs/ci/COMPILE_CHECK.md`.
- **CI:** `.github/workflows/test.yml` runs EditMode and PlayMode tests on every PR via GameCI on Linux. Needs secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`. PRs from forks do not get secrets and will fail the secrets check by design. The iOS build workflow (macOS runner) is owned by release-engineer.

---

## 12. Code quality

- `.editorconfig` at repo root defines naming and style (PascalCase types/methods, `_camelCase` private fields, `I` prefix for interfaces, braces always, block-scoped namespaces).
- `Assets/csc.rsp`: `-warnaserror+` (warnings are errors), `-nowarn:0649` (Unity serialized fields).
- Roslyn analyzers (planned, Week 1, each needs approval and a `docs/LICENSES.md` entry): the open-source Unity analyzer package (`Microsoft.Unity.Analyzers`, MIT) and a small in-house analyzer that bans `UnityEngine.Random`, `UnityEngine.Time` and `System.Random` inside `JungleBooze.Core` and simulation namespaces.

---

## 13. Source control

- Git + Git LFS (rules in `.gitattributes`): textures, models, audio, video, fonts and native binaries are stored in LFS. Watch the GitHub LFS storage/bandwidth quota; CI checks out LFS on every run.
- Unity YAML files use Unity's Smart Merge. One-time local setup:
  `git config merge.unityyamlmerge.driver '"<Unity editor path>/Data/Tools/UnityYAMLMerge" merge -p %O %B %A %A'`
- `.meta` files are always committed together with their asset. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `Build/`.

---

## 14. ADR index

| # | Title | Status |
|---|---|---|
| [0001](adr/0001-engine-and-render-pipeline.md) | Engine and render pipeline: Unity 6 LTS + URP (mobile) | Accepted |
| [0002](adr/0002-deterministic-simulation-core.md) | Deterministic simulation core | Accepted |
| [0003](adr/0003-first-playable-bootstrap.md) | First Playable bootstrap: settings applied by an editor script on first open | Accepted |
| [0004](adr/0004-realistic-look-on-mobile.md) | Realistic look on mobile: URP settings, lighting for streamed chunks, budgets, minimum device (AURELIA Phase 0) | Accepted for Phase 0; device floor provisional |
| [0005](adr/0005-cloud-compile-check.md) | Cloud compile check | Accepted |
| [0006](adr/0006-steering-input-format.md) | Steering input format (replay format 2) | Accepted |
| [0007](adr/0007-look-test-v2-world-structure-and-atmosphere.md) | Look test v2: path-space world, merge per segment, atmosphere | Accepted (look test) |
| [0008](adr/0008-water-mist-and-environment-integration.md) | Layered water and mist without a depth texture; environment asset integration contract | Accepted (look test) |
| [0009](adr/0009-painterly-style-switch.md) | Painterly style behind a style switch | Accepted |
| [0010](adr/0010-device-quality-tiers.md) | Device quality tiers and the iPhone benchmark | Accepted; thresholds provisional until the iPhone 12 run |
| (planned) | Save format, migrations and iCloud | Week 4 |
| (planned) | Composition root: hand-rolled vs VContainer (if needed) | When needed |
