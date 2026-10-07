# ADR 0003: First Playable bootstrap: project settings applied by an editor script on first open

- Status: Accepted
- Date: 2026-10-07
- Deciders: tech-architect (autonomous FP1 mandate; owner informed via coordinator)
- Hard to undo: no (settings can be changed in the editor at any time; the script only fills in new-project defaults)

## Context

The repository was set up without a Unity editor. `UnityProject/ProjectSettings/` contains only `ProjectVersion.txt`,
and there are no scene, render pipeline or settings assets. First Playable (FP1) requires the owner to open the
project on their Mac, press Play and play, with as few manual steps as possible.

Unity's settings files (`ProjectSettings/*.asset`) and scenes (`*.unity`) are YAML with version-specific fields,
internal file IDs and GUIDs. Writing them by hand without an editor to check them risks a broken or silently
"upgraded" project, and some values (for example the render pipeline reference) need GUIDs of assets that do not
exist yet. When a `ProjectSettings` file is missing, Unity generates a correct default for the installed version.

Settings we need on top of Unity's defaults (from `docs/ARCHITECTURE.md` section 2.1 and ADR 0001):

| Setting | Unity default (new, empty project) | Needed |
|---|---|---|
| Active Input Handling | Input Manager (Old) | Both (editor keyboard/mouse via the Input System; old manager kept so uGUI's default input module and any legacy calls keep working in FP1) |
| Render pipeline | Built-in (none assigned) | URP asset tuned for mobile |
| Bundle id | `com.DefaultCompany.UnityProject` | placeholder `com.pistaduko.junglerunner` [ASSUMED] |
| Product name | `UnityProject` | placeholder `Jungle Runner` [ASSUMED]; never the word "booze" |
| Orientation | Auto Rotation | Portrait |
| iOS target device | iPhone + iPad | iPhone only |
| Minimum iOS | Unity's minimum (iOS 13 on 6000.3) | 15.0 |
| Color space | depends on template | Linear |
| Run scene and build list | none | `Assets/_Game/Scenes/Run.unity`, first and enabled |

## Decision

1. **Do not hand-write any `ProjectSettings/*.asset` or `*.unity` file.** Unity generates them on first open.
2. **An editor script applies our settings on first open:**
   `UnityProject/Assets/_Game/Editor/Setup/ProjectBootstrap.cs` (`[InitializeOnLoad]`, assembly `JungleBooze.Editor`),
   with the pure decision rules in `ProjectSetupRules.cs` (no UnityEditor dependency, unit-testable).
   - Runs once per checkout: after a successful run it writes `ProjectSettings/JungleBoozeSetup.txt`
     (`setupVersion=1`). Raising `ProjectBootstrap.SetupVersion` makes every checkout run it again.
   - Work happens in `EditorApplication.delayCall`, after all `[InitializeOnLoad]` code (including the Input System
     package's own first-run prompt) and only when the editor is not compiling or importing.
   - Each step is idempotent and only replaces values that still look like new-project defaults
     (for example, a bundle id that starts with `com.DefaultCompany.`). Deliberate later changes are left alone.
   - Shows one dialog listing what it changed; asks to restart the editor if the input setting changed.
   - Menu `JungleBooze > Setup > Run Project Setup` runs it again at any time.
   - Never runs automatically in batch mode, so CI test runs are not changed behind their back.
     CI or scripts can call `-executeMethod JungleBooze.Editor.Setup.ProjectBootstrap.RunFromCommandLine`.
3. **APIs used (all public, checked against the Unity C# reference and URP 17.3 source):**
   - Active Input Handling: no public API exists. The script sets the serialized field `activeInputHandler`
     (0 = Old, 1 = New, 2 = Both) on the `PlayerSettings` object through `SerializedObject`, the same field the
     Input System package changes. A restart is required for it to take effect.
     **Manual fallback:** Edit > Project Settings > Player > Other Settings > Active Input Handling = Both, then restart.
   - Render pipeline: `ScriptableObject.CreateInstance<UniversalRendererData>()`,
     `UniversalRenderPipelineAsset.Create(rendererData)`, `AssetDatabase.CreateAsset`, then
     `GraphicsSettings.defaultRenderPipeline = asset`. Mobile tuning is written through serialized fields verified in
     URP 17.3 (`m_SupportsHDR`, `m_MSAA`, `m_RenderScale`, `m_RequireDepthTexture`, `m_RequireOpaqueTexture`,
     `m_MainLightShadowsSupported`, `m_AdditionalLightsRenderingMode`, `m_SoftShadowsSupported`, `m_UseSRPBatcher`;
     renderer `m_RenderingMode`, `postProcessData`). Any field that is missing in a later URP version is reported and
     skipped instead of failing.
   - Player: `PlayerSettings.productName`, `SetApplicationIdentifier(NamedBuildTarget.iOS, ...)`,
     `defaultInterfaceOrientation`, `iOS.targetDevice`, `iOS.targetOSVersionString`, `colorSpace`.
   - Editor: `EditorSettings.serializationMode = ForceText`, `VersionControlSettings.mode = "Visible Meta Files"`.
   - Scene: `EditorSceneManager.NewScene(EmptyScene, Single)` + `SaveScene`; `EditorBuildSettings.scenes`.
4. **Render pipeline for FP1: URP, with a one-click fallback to built-in.** ADR 0001 chose URP; FP1 gray-box art is
   created at runtime, so starting on URP costs nothing now and avoids converting materials later. If URP creation
   fails the script reports an error and the project stays on the built-in pipeline (still playable).
   `JungleBooze > Setup > Fallback: Use Built-in Renderer` exists for pink/black rendering on the owner's machine.
   Mobile settings: Forward, HDR off, MSAA 2x, render scale 1.0, no depth/opaque texture, no realtime shadows
   (blob shadow under characters per ADR 0001), additional lights off, SRP Batcher on.
5. **The Run scene is empty.** The presentation layer (FP1 stage C) builds camera, light, track, player and HUD at
   runtime when Play is pressed. The scene exists so there is something to open and to put in the build list.
6. **Minimum iOS 15.0.** Apple currently allows iOS 13+ as a deployment target and has announced iOS 15+ from
   April 2027; Unity 6000.3 supports iOS 13+. The floor device (iPhone 11 / SE 2nd gen) runs iOS 26, so 15.0
   costs no players we care about and matches the announced Apple rule early.
7. **Placeholders** (owner decides the real values later): product name `Jungle Runner`, bundle id
   `com.pistaduko.junglerunner`. Company name is left at Unity's default.

## Deliberately not done by the script (FP1)

- **Enter Play Mode Options (domain reload off)**: deferred. FP1 code has not been checked for static state; a
  wrong static would make the second Play behave differently. Re-enable after the code-reviewer confirms there is
  no mutable static state.
- **Quality levels** (reduce to one "Mobile" level): needs editing `QualitySettings.asset` arrays through
  `SerializedObject`; low value for FP1. The URP asset is assigned as the default pipeline and no quality level
  overrides it, so every level renders with it.
- **Switching the active build target to iOS**: needs iOS Build Support installed and reimports everything; the
  owner does it once in Build Profiles when building to a phone (see `docs/PLAY_FIRST_BUILD.md`).
- **Managed stripping level, Metal-only, IL2CPP, ARM64**: Unity's iOS defaults already match (IL2CPP and Metal are
  the only options on iOS; ARM64 is the default). Stripping stays at the default until a `link.xml` exists.

## Options considered

| Option | Pros | Cons |
|---|---|---|
| **Editor bootstrap script** (chosen) | Uses Unity's own APIs, so files are valid for whatever 6000.3 patch the owner installs; idempotent; re-runnable; visible report | Settings exist only after the first open; the generated files must be committed afterwards |
| Hand-written `ProjectSettings/*.asset` and `Run.unity` | Settings in git from the start; CI sees them immediately | Format and field set vary by version; GUIDs/file IDs for the URP asset cannot be produced correctly without the editor; a mistake breaks the first open in ways the owner cannot diagnose |
| Written step-by-step manual instructions only | No code | Many clicks for a non-developer owner; easy to miss one; not repeatable for later games |
| Built-in pipeline for FP1, URP later | Nothing to create | Every material made later must be converted; contradicts ADR 0001; the template for later games would start on the deprecated path |

## Consequences

- Positive: the owner opens the project, accepts at most one restart, and the Run scene is open and ready to play.
  The same script is the template for later games.
- Negative: until someone commits the files generated on the owner's Mac (`ProjectSettings/*`,
  `Assets/_Game/Config/Rendering/*`, `Assets/_Game/Scenes/Run.unity*`, `Packages/packages-lock.json`, and URP's
  `Assets/UniversalRenderPipelineGlobalSettings.asset` / `Assets/DefaultVolumeProfile.asset`), CI runs on Unity
  defaults. CI tests do not depend on these settings.
- The Input System package may show its own prompt ("enable the backends?") on first open. Clicking **Yes** sets
  input handling to Both and restarts; the bootstrap then has nothing to change for input.
- Follow-ups:
  - Commit the generated settings after the owner's first open (coordinator).
  - EditMode tests for `ProjectSetupRules` (could not be added in this stage because `Tests/` was being edited by
    other agents).
  - Week 1: enable Enter Play Mode Options; reduce quality levels to one; move URP global settings assets under
    `Assets/_Game/Config/Rendering/`.
