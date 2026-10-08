# ADR 0005: Cloud compile check without a Unity license

- Status: Accepted
- Date: 2026-10-08
- Deciders: tech-architect (owner direction 2026-10-08: test constantly ourselves; tools may be installed in the cloud
  container; no spending)
- Hard to undo: **no**. It is a script plus a cache directory; nothing in the Unity project depends on it

## Context

The owner compiles and plays on a Mac. Until now the cloud sessions checked C# only against hand-written stubs of the
Unity and URP APIs, so errors against the real API (and against URP 17.3 shader includes) reached the owner first.
There is no Unity license in the cloud environment, so the editor cannot run in batch mode (no import, no tests).

## Decision

Add `tools/ci/compile_check.sh` (+ `compile_check.py`, `shader_check.py`, standard-library Python only):

1. Download the exact editor version from `ProjectVersion.txt` (Linux editor, Unity's public download server) and keep
   only what compilation needs: managed UnityEngine/UnityEditor assemblies, .NET Standard reference assemblies, the
   bundled .NET runtime and Roslyn `csc`, Unity's source generators, built-in package sources. The iOS Build Support
   module adds the UnityEditor.iOS extensions and the iOS player's engine assemblies.
2. Download registry packages at the exact versions in `packages-lock.json` (SHA-1 checked).
3. Re-implement Unity's assembly-definition rules (platforms, define constraints, version defines, references,
   precompiled plug-in settings) and compile in dependency order with Unity's own compiler and options and our
   `csc.rsp`, in three configurations: editor with iOS target, editor with macOS target, iOS player.
4. Compile our hand-written shaders with DXC (the compiler Unity uses for Metal) against the real URP/Core
   ShaderLibrary, with Unity's Metal defines, over a linear set of keyword variants. DXC comes from a pinned LunarG
   Vulkan SDK (open source).

Details, limits and the run log: `docs/ci/COMPILE_CHECK.md`.

## Alternatives considered

- **Keep the stubs:** cheap, but they drift from the real API and miss signature, overload and namespace errors.
  Rejected; the stubs are not needed any more.
- **Run the Unity editor in batch mode (GameCI image):** the most faithful (import + tests), but needs a license
  secret. Recommended as the next step (see "What a Unity license in the cloud would add" in COMPILE_CHECK.md); this
  check stays useful even then because it needs no license and runs in about a minute.
- **Decompile Unity's script-compilation program to copy its exact command lines:** more fidelity on defines, but
  brittle across Unity versions. Rejected; the define list is small and documented.

## Consequences

- Every agent can (and should) run the check before handing over C# or shader changes. The first run in a fresh
  container downloads about 5.8 GB and takes about 10 minutes; later runs take about a minute.
- Fidelity limits are documented (hand-made define list, implicit uGUI references for packages, no shader ShaderLab
  parsing or Metal compile, tests not executed). When the Unity version or packages change, the check follows
  automatically; when the project starts using new scripting define symbols, update the list in `compile_check.py`.
- Nothing is shipped in the app. The downloaded Unity files stay in a local cache and are not redistributed.
