# ADR 0001: Engine and render pipeline: Unity 6 LTS + URP (mobile)

- Status: Accepted
- Date: 2026-10-06
- Deciders: tech-architect (owner informed via coordinator)
- Hard to undo: **yes** (engine); render pipeline is hard to change once art is authored

## Context

JungleBooze is a 3D endless runner, iOS first, Android later, built mostly by code-writing agents working through text, CI and batch mode. It must hold 60 fps on an older iPhone, ship under 200 MB, and serve as a template for later portfolio games. The team needs mature iOS support (StoreKit, Game Center, ATT, privacy manifests), a large pool of documentation and examples, headless test/CI support, and stable long-term support.

## Decision

1. **Engine: Unity 6 LTS, stream 6000.3** (pinned in `UnityProject/ProjectSettings/ProjectVersion.txt`; currently `6000.3.0f1`, to be bumped to the latest 6000.3 patch on first editor open).
   - Stay on the 6000.3 LTS stream until launch. Patch upgrades go in their own PR with full CI. Moving to a newer LTS stream needs a new ADR.
2. **Render pipeline: Universal Render Pipeline (URP)**, configured for mobile:
   - Forward renderer (not Forward+ or Deferred), MSAA 2x or off depending on GPU budget, HDR off, render scale 1.0 with dynamic resolution as a fallback lever.
   - Main light only, no additional per-pixel lights; baked/ambient lighting for the environment; character shadow via blob/projected shadow instead of realtime shadow maps [ASSUMED, art-director may request a cheap single-cascade shadow if budget allows].
   - Post-processing limited to color grading (LUT) and optional cheap bloom; no SSAO, motion blur, depth of field.
   - SRP Batcher on; materials restricted to URP Simple Lit / Unlit / Shader Graph or custom mobile shaders; shader variant stripping on.
   - Depth/opaque textures off unless a specific effect needs them.
3. **Scripting**: IL2CPP, ARM64, .NET Standard 2.1, Metal only, Linear color space.
4. **Packages** (Unity-maintained only for Week 0): URP, Input System, Test Framework, Performance Testing, Addressables, uGUI. Versions are in `UnityProject/Packages/manifest.json` and must be confirmed against what the editor resolves for 6000.3 (they could not be checked from the setup environment).

## Options considered

| Option | Pros | Cons |
|---|---|---|
| **Unity 6 LTS + URP** (chosen) | Mature iOS pipeline (IL2CPP → Xcode), Unity IAP/StoreKit, Game Center plugins, every major ad SDK supports it, GameCI for headless tests, huge example base, LTS support window into late 2027 | Licensing/runtime-fee history requires watching terms; Editor is heavy; closed source |
| Unity 6 LTS + Built-in pipeline | Simplest, many legacy assets | Deprecated direction; no SRP Batcher benefits; poor long-term choice for a template |
| Unity 6 LTS + HDRP | High fidelity | Not supported on mobile |
| Godot 4 | Open source, light, MIT license | Weaker iOS monetization SDK ecosystem (ads mediation, IAP), smaller mobile 3D performance track record, less CI tooling |
| Unreal Engine 5 | Top-end visuals | Large binaries, heavy for an older iPhone, C++ slows agent iteration, overkill for a stylized runner |
| Native (Swift + SceneKit/Metal) | Smallest app, best platform fit | No Android path, much more engine work, no ecosystem of game tooling |

## Consequences

- Positive: proven path to App Store; testable C# code; CI on Linux for tests and macOS for builds; Addressables for content updates.
- Negative: a Mac (owned or cloud) is required to compile and sign iOS builds; Unity account and license secrets are needed for CI; package/editor upgrades must be managed deliberately.
- Follow-ups:
  - First editor open: apply settings listed in `docs/ARCHITECTURE.md` section 2.1 and commit them with the URP asset.
  - Confirm `ProjectVersion.txt` patch number and package versions; commit `Packages/packages-lock.json`.
  - Confirm the GameCI Docker image exists for the pinned editor version.
  - Record Unity and every package in `docs/LICENSES.md` (Unity Companion License / Unity Terms of Service).
