# Cloud compile check (no Unity license)

`tools/ci/compile_check.sh` compiles all of our C# the way the Unity editor on the owner's Mac does, and compiles our
hand-written shaders with the HLSL compiler Unity uses for Metal. It runs in the Linux cloud container (or any
Linux machine) without a Unity license and without starting Unity. Use it before every commit that touches
`UnityProject/Assets/_Game/**`, so compile errors are caught before the owner opens Unity. Decision record: ADR 0005.

## Run it

```bash
tools/ci/compile_check.sh               # C# (3 configurations) + shaders
tools/ci/compile_check.sh editor-ios    # one C# configuration (+ shaders)
tools/ci/compile_check.sh --no-shaders  # C# only
tools/ci/compile_check.sh --fetch-only  # only download and extract the toolchain
```

Exit status 0 means: no errors and no warnings in our code (warnings are errors, as on the Mac). 1 means something
failed; every error is printed as `path(line,col): error CSxxxx: message` (or `file:line:col: error: message` for
shaders). 2 means a setup problem. On GitHub Actions errors are also printed as `::error` annotations.

Needs: `bash`, `curl`, GNU `tar` with `xz`, Python 3.11.4 or newer (standard library only). Nothing is installed
system-wide: everything goes into the cache directory.

- Cache: `$JB_CI_CACHE`, default `~/.cache/junglebooze-ci` (about 1.6 GB on disk). Delete it to start over.
  In the cloud container, point it at the session scratchpad so it is never inside the repo.
- First run in a fresh container downloads about 5.3 GB (streamed; nothing large is stored) and takes about
  10 minutes (measured: see the table below). Later runs use the cache: about 1 minute for C#, 15 s for shaders.
- To reuse archives you already downloaded: `JB_UNITY_TARBALL`, `JB_IOS_TARBALL`, `JB_VULKAN_SDK_TARBALL`.

| Download | Size | What is kept |
|---|---|---|
| Unity 6000.3.25f1 Linux editor (`download.unity3d.com`, changeset from `ProjectVersion.txt`) | 4.54 GB | `Editor/Data/Managed` (UnityEngine/UnityEditor assemblies), `NetStandard` reference assemblies, the bundled .NET runtime and Roslyn `csc`, Unity's source generators, built-in packages (URP, Core, Shader Graph, uGUI, Test Framework, NUnit): about 505 MB |
| iOS Build Support for the same editor (Linux) | 335 MB | `UnityEditor.iOS.Extensions*.dll` and the iOS player's `UnityEngine.*` assemblies: 9 MB |
| Registry packages from `Packages/packages-lock.json` (`packages.unity.com`, SHA-1 verified) | about 330 MB | package sources without `~` folders |
| LunarG Vulkan SDK 1.4.363.0 (pinned; source of DXC 1.9) | 367 MB | `dxc` + `libdxcompiler.so`: 38 MB |

The Unity version, changeset and package versions are read from the project, so the check follows Unity or package
upgrades automatically.

## What it checks

### C# (`tools/ci/compile_check.py`)

It reads every `.asmdef`/`.asmref` in `Assets` and in the packages of `packages-lock.json` and builds the same
assemblies Unity builds:

- **Which assemblies exist** per configuration: `includePlatforms`/`excludePlatforms`, `defineConstraints`,
  `versionDefines` (package and Unity version ranges), GUID references, folders Unity ignores (`.name`, `name~`).
- **References:** the assemblies an `.asmdef` lists, precompiled DLLs per their `.meta` (auto-referenced or listed in
  `precompiledReferences` with `overrideReferences`, plug-in platform settings), the .NET Standard 2.1 reference set
  (the project's API compatibility level), UnityEngine modules (minus modules whose `com.unity.modules.*` package is
  not in the project), UnityEditor modules in editor configurations, the iOS editor extensions for the iOS target.
- **Compiler and options:** the Roslyn `csc` shipped inside Unity 6000.3.25f1, run by the .NET runtime shipped with
  it; C# 9, `-nostdlib`, deterministic, Unity's always-suppressed warnings (CS0169, CS0649, CS0282, CS1701, CS1702),
  Unity's source generators, and `Assets/csc.rsp` (`-warnaserror+`) for our assemblies.
- **Scripting defines:** Unity 6000.3 version defines, `UNITY_EDITOR`/`UNITY_EDITOR_OSX`, `UNITY_IOS`/`UNITY_IPHONE`
  or `UNITY_STANDALONE_OSX`, `ENABLE_INPUT_SYSTEM` and `ENABLE_LEGACY_INPUT_MANAGER` (from Active Input Handling),
  `UNITY_INCLUDE_TESTS` for our assemblies in the editor, Player Settings scripting define symbols.
- Dependency order, independent assemblies in parallel; package assemblies are cached by the hash of their inputs.

Configurations:

| Name | Matches | Our assemblies compiled |
|---|---|---|
| `editor-ios` | Mac editor with the build target set to iOS (the normal setup) | Core, Services, Gameplay, UI, App, Editor, Tests.EditMode, Tests.PlayMode |
| `editor-osx` | Mac editor before switching the target (macOS standalone) | same 8 |
| `player-ios` | the scripts step of an iOS player build (no `UNITY_EDITOR`, IL2CPP define, iOS player engine assemblies, so editor-only engine API is an error) | Core, Services, Gameplay, UI, App |

Package warnings are not shown (Unity does not stop on them either); package errors are.

### Shaders (`tools/ci/shader_check.py`)

For each `.shader` in `Assets/_Game/Art/Shaders` and each pass, it joins the `HLSLINCLUDE` and `HLSLPROGRAM` blocks,
defines what Unity's shader compiler defines for iOS/Metal (`SHADER_API_METAL`, `SHADER_API_MOBILE`,
`UNITY_COMPILER_DXC`, `UNITY_NO_DXT5nm`, `SHADER_TARGET`, `UNITY_VERSION`, the stage), resolves
`Packages/com.unity.render-pipelines.*` includes against the real URP 17.3 / Core ShaderLibrary from the editor, and
compiles the vertex and fragment entry points with DXC to SPIR-V (Unity's Metal path is DXC to SPIR-V to Metal).
This catches wrong ShaderLibrary function names and signatures, missing struct fields, undefined macros and HLSL
errors in our code. Keywords: every `shader_feature` combination with the `multi_compile` defaults, then each
`multi_compile` option (shadows, soft shadows, fog, instancing) on its own with every feature on (84 stage compiles
today). Stage-specific keywords (`_vertex`/`_fragment`) are defined only in their stage. Errors anywhere fail the
check; warnings are reported only for our files (precision-conversion warnings from `half` are suppressed).

## What it does not check (limits)

- **No license, no editor:** nothing is imported, no scene or asset is opened, no test runs. EditMode and PlayMode
  tests are compiled but **not executed**.
- **No full shader compile:** no ShaderLab parsing (Properties, render states, tags), no SPIR-V to Metal translation,
  no Metal compiler, not every keyword combination, no Shader Graph. A shader that passes can still fail at import
  in Unity, and the look on device is untested.
- **Serialized names and strings** (`SerializedObject.FindProperty("m_MSAA")`, material property names, asset paths,
  menu paths) are not checked by the compiler. They were checked by hand against URP 17.3.0 on 2026-10-08 (see
  "Run log").
- **Approximations** (each one would only make the check stricter or equal, except the last two):
  - Scripting defines are a hand-made list of what Unity 6000.3 sets (Unity generates them at run time). Our code
    only uses `ENABLE_INPUT_SYSTEM`, `ENABLE_LEGACY_INPUT_MANAGER`, `UNITY_EDITOR`, `UNITY_STANDALONE`, `UNITY_IOS`,
    `UNITY_ANDROID`; if code starts using other symbols, check that the list in `compile_check.py` has them.
  - uGUI: Unity 6000.3 lets package assemblies use `UnityEngine.UI`/`UnityEditor.UI` without listing them (URP Core
    and the Input System rely on it). The check grants this to package assemblies only; our assemblies must list
    `UnityEngine.UI` themselves (stricter than, or equal to, Unity).
  - Our assemblies get only direct references, as in Unity.
  - Editor configurations use the Linux editor's managed assemblies. They are the same IL as the Mac editor's,
    except platform-specific editor extensions: the macOS standalone extension assembly is not referenced (code that
    uses `UnityEditor.OSXStandalone` would fail here but not on the Mac).
  - Roslyn analyzers shipped in packages (`RoslynAnalyzer` label) are not run; none of our packages has one today.
- Scripts outside any `.asmdef` (Assembly-CSharp) are listed but not compiled; we have none.
- `.editorconfig` naming rules are not enforced (Unity does not pass `.editorconfig` to the compiler either).

## What a Unity license in the cloud would add

With a Unity license file available as a secret (`UNITY_LICENSE`, the contents of an activated `.ulf`, plus
`UNITY_EMAIL`/`UNITY_PASSWORD`; Unity Personal is enough, the owner already chose Personal for CI), the same Linux
editor this check downloads can run headless:

- **Run the EditMode tests** in the cloud: `Unity -batchmode -nographics -projectPath UnityProject -runTests
  -testPlatform EditMode -testResults results.xml` (about 5 minutes on a warm Library). This is the biggest gain:
  today our tests only compile here and only run on the owner's Mac.
- Run PlayMode tests (most need no GPU with `-nographics`; rendering tests do).
- Import the project for real: catches asset/meta errors, ScriptableObject and scene serialization problems, and
  shader import errors (full ShaderLab and Unity's own shader compiler for the active platform).
- Run editor tools in batch mode (for example the look-test scene builder) and the balance bot runs.

The existing `.github/workflows/test.yml` already expects these secrets. Inside this cloud container the same license
would be passed as an environment secret. Cost: none for Unity Personal; the owner has to create the `.ulf` once
(GameCI activation guide). Risk: a Personal license is tied to the owner's account and must not be committed.

## Run log

| Date | Result | Notes |
|---|---|---|
| 2026-10-08 | C#: PASS in all 3 configurations, 0 errors, 0 warnings. Shaders: PASS, 4 shaders, 84 stage compiles, 0 errors, 0 warnings | First run. Look-test code (`Scripts/App/LookTest`, `Editor/LookTest`) compiled clean against the real URP 17.3.0. All 21 URP serialized field names used by `LookTestPipelineSetup`/`ProjectBootstrap` exist in URP 17.3.0, and the enum values written as integers match. Shader property names set from C# all exist in our shaders; every non-texture property is in `UnityPerMaterial` (SRP Batcher). Self-test: a planted C# error and warning, and a planted shader error, were all caught with correct file and line |
