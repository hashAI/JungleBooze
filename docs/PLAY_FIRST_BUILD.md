# Play the First Playable on your Mac

This guide takes you from a clean Mac to playing the game in the Unity editor, running the automated tests, and
(optionally) putting the game on your iPhone. No programming needed. Plan about 1 to 2 hours, mostly waiting for
downloads.

Written for: MacBook with Apple M4 chip, 24 GB memory. Owner: tech-architect. Technical background: ADR 0003
(`docs/adr/0003-first-playable-bootstrap.md`).

---

## Quick start (already set up Unity before? start here)

1. In Terminal, in your `JungleBooze` folder: `git pull`, then `git lfs pull`. The second command downloads the
   models, textures and audio, which are stored with Git LFS. Without it the art files are tiny text stubs and the game
   shows gray boxes and plays no sound. (GitHub Desktop pulls LFS files by itself.)
2. Open the **`UnityProject`** folder in Unity Hub with **Unity 6000.3.25f1** (the Hub offers it, or pick the 6000.3
   version you have). Close Unity before pulling.
3. Wait for the first import: 10 to 20 minutes the first time (the character and environment models are large).
4. What to expect on first open:
   - The usual setup dialogs (see Step 4 below). Answer **Yes** and **Restart now** when asked.
   - The Console (Window > General > Console) shows `Environment prefabs built: N of 17.` once the environment models
     have imported. This is automatic; you do not need to click anything. If it says fewer than 17, or you see red
     errors, run **JungleBooze > Art > Build Environment Prefabs** from the menu once more and send the Console text.
   - The Pista and Duko models need no steps: they are loaded straight from `Art/Characters/Resources`.
5. Open the scene **Run** (`Assets/_Game/Scenes/Run`) and press **Play**. Click once inside the Game view, then press
   any key or tap to start. Controls are in the table in Step 5. Sound plays from the start (menu music, then the
   jungle music); turn your Mac volume up.

### How to see the winding route

The run now uses the winding (generated) route by default in the editor and development builds. Look for the small
label at the bottom-left of the Game view: `Route: Generated (F4)  |  F3 hitboxes`.

- Click once inside the Game view first (keys only work while it has focus).
- Press **F4** to change the route for the **next** run: Generated, Debug curve, Straight, then Generated again. The
  label adds `(next run)` until you start a new run (die and restart, or start from the menu). Your choice is remembered.
- On a phone development build, put four fingers on the screen to do the same as F4.
- Use **Straight** to compare against the old flat route. **Debug curve** is a fixed gentle S-curve for checking the camera.
- Press **F3** to show the hitboxes.

### If a model faces the wrong way

Open `UnityProject/Assets/_Game/Scripts/Gameplay/Views/RunnerView.cs` and change `ModelYawFixDeg` (near the top,
default `0f`) for Pista, or the same constant in `CompanionView.cs` for Duko. Use `180f` if the model shows its back
or runs backward, `90f` or `-90f` if it is sideways. Save, wait for Unity to recompile, press Play again.

### Known issues in this build (be honest with yourself when judging it)

- The models and their animations were generated and prepared without Unity. **Nobody has seen them in Unity yet.**
  Expect to find wrong sizes, wrong facing, odd animation blends or stretched textures; each is a quick fix once seen.
  Pista's clips are run, jump, slide, stumble and idle; Duko's are Idle (perched), Flap (flying) and TailWag (cheer).
- The art has a painterly look from the AI generator. Whether it fits the "Inkbound Pulp" style is your call.
- The Meshy (model generator) commercial licence terms are **not verified** (see `docs/LICENSES.md`). Do not ship
  until they are.
- If a model or prefab is missing or fails to load, the game falls back to the gray-box shapes for that piece, so
  the game is always playable.
- If the environment looks pink or black: Edit > Project Settings > Graphics must show **URP-Mobile**
  (JungleBooze > Setup > Run Project Setup fixes it). The environment prefabs get a URP material automatically.

---

## What you need

| Item | Why | Notes |
|---|---|---|
| About 40 GB free disk space | Unity (~15 GB with iOS support), the project cache (~5 GB), Xcode (~15 GB, only for the iPhone step) | Check in Apple menu > About This Mac > More Info > Storage |
| A Unity account (free) | Unity Hub needs it to install and license the editor | Unity Personal is free and is what we use |
| Access to the GitHub repository `hashAI/JungleBooze` | To download the project | Same GitHub account you use for this project |
| Optional: an iPhone, its cable, a free Apple ID, Xcode 26 | Only for "Play on your iPhone" | The phone needs iOS 15 or later |

---

## Step 1: Install Unity Hub

1. Go to **unity.com/download** and download **Unity Hub for macOS**.
2. Open the downloaded file and drag **Unity Hub** into **Applications**. Start it.
3. Sign in with your Unity account (or create one).
4. If the Hub asks about a license, choose **Get a free Personal license**. (You can also do this later in
   Unity Hub > Settings (gear icon) > Licenses > Add > Get a free personal license.)

## Step 2: Install Unity 6.3 LTS with iOS Build Support

The project is pinned to the **Unity 6.3 LTS** line (the owner's Mac uses **6000.3.25f1**). The file
`UnityProject/ProjectSettings/ProjectVersion.txt` may say an older patch such as `6000.3.0f1`. Any newer patch of the same line (`6000.3.something`) is fine and preferred, because patches only
fix bugs.

1. In Unity Hub, click **Installs** (left side) > **Install Editor**.
2. On the **Official releases** tab, pick the newest version that starts with **6000.3** (shown as
   **Unity 6.3 (LTS)**). Use **at least 6000.3.8f1**: earlier 6.3 versions lack a fix Apple will require for iPhone
   apps.
   - Do **not** pick 6000.0, 6000.4, 6000.5 or newer streams.
   - If you see a choice of chip, pick **Apple silicon**.
   - Only if the team asks for an exact version: open the **Archive** tab, click "download archive", find the
     version under Unity 6.3, and click **Install** (it opens in the Hub).
3. On the **Add modules** screen, tick **iOS Build Support**. You can skip everything else
   (Documentation is optional). Click **Install**.
4. Wait until the install finishes (20 to 40 minutes).

Write down the exact version you installed (for example `6000.3.12f1`). The team will ask for it if anything goes
wrong.

## Step 3: Download the project

The easiest way is **GitHub Desktop** (free; it also handles the large-file storage the project uses).

1. Download it from **desktop.github.com**, drag it into Applications, start it, and sign in to GitHub.
2. **File > Clone repository**, pick `hashAI/JungleBooze`, choose a folder you can find again (for example
   `Documents/Projects`), and click **Clone**.
3. At the top, click **Current branch** and select the branch the team named in its message (also listed in
   `docs/STATUS.md`).
4. To get the newest version later: click **Fetch origin**, then **Pull origin**. Close Unity before pulling.

<details>
<summary>Alternative: Terminal</summary>

```bash
xcode-select --install          # installs git if it is missing (skip if already installed)
brew install git-lfs && git lfs install   # needs Homebrew (brew.sh); large-file support
git clone https://github.com/hashAI/JungleBooze.git
cd JungleBooze
git checkout <branch name from the team>
git pull                        # later, to update
```
</details>

## Step 4: Open the project

1. In Unity Hub, click **Projects** > **Add** (top right) > **Add project from disk**.
2. Select the **`UnityProject`** folder inside the `JungleBooze` folder (not `JungleBooze` itself). Click **Open**.
3. If the Hub shows a version warning (the project says 6000.3.0f1, you installed a newer 6000.3), click the version
   next to the project and pick the 6000.3 version you installed. If Unity asks whether to upgrade or continue, choose
   **Continue** / **Change version**. This is a safe patch update.
4. Click the project to open it. **The first open takes 5 to 15 minutes**: Unity downloads packages and builds its
   cache in `UnityProject/Library`. Later opens take under a minute.

### What happens during the first open

These dialogs are expected. Answer as shown:

| Dialog | Answer | What it does |
|---|---|---|
| Input System: "...native platform backends for the new input system are not enabled... Do you want to enable the backends?" | **Yes** | Turns on keyboard, mouse and touch input. Unity restarts by itself. |
| "JungleBooze project setup" with a list of changes and "Restart now?" | **Restart now** | Our setup script (see below). The restart makes the input change take effect. |
| "JungleBooze project setup" with a list and an OK button | **OK** | Same script; nothing needed a restart. |
| "Enter Safe Mode?" | **Enter Safe Mode** | Means the code did not compile. Go to **Troubleshooting > Compile errors**. |

The setup script (menu **JungleBooze > Setup > Run Project Setup**) does this once, automatically:

- Sets input handling to support keyboard, mouse and touch (needs the one restart).
- Creates the mobile graphics settings (`Assets/_Game/Config/Rendering/URP-Mobile.asset`) and turns them on.
- Sets the iPhone basics: portrait only, iPhone only, minimum iOS 15, a placeholder app name "Jungle Runner" and
  placeholder bundle id `com.pistaduko.junglerunner`. The real name and id are your decision later.
- Creates the game scene `Assets/_Game/Scenes/Run.unity`, adds it to the build list, and opens it.
- Writes `ProjectSettings/JungleBoozeSetup.txt` so it does not run again. A summary also appears in the
  **Console** window (Window > General > Console), lines starting with `[JungleBooze setup]`.

If you never saw the setup dialog, run it yourself from the menu bar: **JungleBooze > Setup > Run Project Setup**.

## Step 5: Play

1. Make sure the **Run** scene is open: the title bar shows "Run". If not, in the **Project** window go to
   `Assets > _Game > Scenes` and double-click **Run**.
   The scene looks empty before Play. That is normal: the game builds the track, character and HUD when it starts.
   A "No cameras rendering" message in the Game view before Play is also normal.
2. Make the Game view phone-shaped: click the **Game** tab, open the aspect drop-down (it may say "Free Aspect"),
   and pick a portrait size. If none exists, click **+** and add **1170 x 2532** (iPhone portrait), type
   "Fixed Resolution".
3. Press **Play** (the triangle at the top center). **Click once inside the Game view** so it receives the keyboard.
4. Press **Play** again to stop. Anything you change while playing is undone when you stop.

### Controls

When you press Play, Pista stands at the start and the screen says **"Swipe or press a key to run"**. Your first
key press, click or swipe only starts the run; it does not move her.

| Action | Keyboard (editor) | Mouse (editor) | Touch (iPhone) |
|---|---|---|---|
| Start the run (from the prompt) | Any movement key, Space or Enter | Click anywhere in the Game view | Tap or swipe |
| Move one lane left | Left arrow or A | Drag left | Swipe left |
| Move one lane right | Right arrow or D | Drag right | Swipe right |
| Jump | Up arrow, W or Space | Drag up | Swipe up |
| Slide | Down arrow or S | Drag down | Swipe down |
| Grab a vine | Jump (Up, W or Space) just before the glowing vine, in its lane | Drag up | Swipe up |
| On a vine: let go (time it with the ring) | Up arrow, W or Space | Drag up | Swipe up |
| On a vine: aim at the next vine / landing lane | Left / right arrow or A / D | Drag left / right | Swipe left / right |
| On a vine: slide when you land | Down arrow or S | Drag down | Swipe down |
| Lift (when the violet LIFT meter is full) | E or Left Shift | Double-click (two quick clicks without moving) | Double tap |
| Continue screen: continue (free or for coins) | Space or Enter | Click **Free continue** / **Continue** | Tap **Free continue** / **Continue** |
| Continue screen: skip to Game Over | Esc or P | Click **Skip** | Tap **Skip** |
| Pause | P or Esc | Click the **pause** button (top right) | Tap the **pause** button (top right) |
| Resume (3-2-1 countdown, then the run continues) | P or Esc | Click **Resume** | Tap **Resume** |
| Game Over: run again on a new track | Space, Enter or R | Click **Run again** | Tap **Run again** |
| Game Over: run the same track again | T | Click **Same track** | Tap **Same track** |
| End the run now (testing aid, development builds only) | K | — | — |

Notes:
- A "drag" means: hold the left mouse button, move quickly (within a quarter second) at least a few millimeters,
  then let go. Short, quick drags work best, like a swipe on the phone.
- The Game Over buttons ignore presses for the first 0.4 seconds, so a late swipe cannot restart by accident.
- The game also pauses by itself when Unity's Game view loses focus (for example when you click another window).
  Press P or click **Resume** to continue.
- The track is generated from a seed: dark wooden barriers (jump over the low ones, slide under the hanging
  ones), tall blocks (change lane), a rolling boulder that shifts one lane, and ravines (dark gaps in the ground,
  marked by a red and black strip before the edge; jump across them). Gold coins with a turquoise gem float in lines
  and arcs; run through them to collect them.
- The first hit is a **stumble** (Pista hops, the screen flashes orange, "Stumble!" appears); a second hit while
  still dazed, or falling into a ravine, ends the run. The Game Over screen names what got you.
- Top-left shows distance and score, top-right the coin count. "Near miss!" appears when you graze an obstacle.
- **Vines** (about 25–35 s into a run, then every 35–70 s): a yellow signpost at the path edge and a coin line
  lead to a rope hanging over one lane, with a white glow and a pulsing gold ring at the grab point. Run in that
  lane and jump just before it; Pista catches it by herself while in the air (a jump started up to 0.45 s before
  it still counts). She swings forward for 1.4 seconds (the camera widens and the first moment runs a bit slower).
  A ring around her hand fills up: let go when it reaches the **gold** part for a **PERFECT!** (+400, a high
  launch through a ring of 25 coins and a gold trail); elsewhere in the second half is **GOOD** (+150, 10 coins);
  letting go too early is remembered for 0.15 s; if you never let go she drops off at the end (+50, no coins).
  Nothing can hit her on the vine.
- Later in a run (from about 600 m) some vines hang over a **chasm** (a long dark gap): missing that vine ends the
  run ("Missed vine"). A plain jump is too short to cross it. Vines over solid ground only cost you the bonus.
- **Chains** (from about 600 m): two or three vines in a row. While swinging, swipe toward the lane of the next vine
  (its glow grows when aimed at); a GOOD or PERFECT release then flies you straight to it. Each PERFECT in a chain
  raises the bonus multiplier (x1, x1.5, x2).
- **Duko, the macaw** (the real model; gray-box shapes if the model is missing) flies just ahead of and above Pista.
  He calls out big moments in a speech bubble (and a voice line): **"Vine!"** about 2 s before a vine section (he swoops
  over the vine lane), **"Look out!"** about 1.5 s before a rolling boulder or a signature hazard (he swoops over that
  lane), and a cheer (**"Shiny!"**, **"Wow!"**, **"Yeah!"**) for a PERFECT release or when you pass your best
  distance. After a death he perches on Pista's head.
- **LIFT meter** (violet bar under the score): near misses +5%, every 25-coin streak +5%, GOOD release +10%, PERFECT
  +25%. When it is full it pulses and says how to trigger it. **Lift**: Duko grabs Pista's wrists and carries her
  2.5 m up for 4 seconds; nothing can hurt her, left/right still change lanes (up/down are ignored), and coins from
  all three lanes within 10 m fly to her. He sets her down in the last 0.6 s on a clear stretch, and she stays safe
  for 0.5 s after. A double tap on a vine waits until she lands. A full meter waits for you; it never fires by itself.
- **Continue** (after a death, before Game Over): a 5-second screen. In your **first session** Duko gives one free
  continue. Otherwise it costs coins from your wallet: 300 for the first continue in a run, 600 for the second (max 2
  per run); the screen only appears when you can use it. Pista comes back at a safe spot (after a ravine or a missed
  vine chasm: on the ground past it), the obstacle that got her is gone, the next 1.5 s of track is clear, and she
  is safe for 2 s. A 3-2-1 countdown runs first. "Watch ad" is a placeholder and stays disabled until ads exist.
- **Run again** uses a new track; **Same track** replays the same seed (same obstacles and coins). Use **K**
  (development builds) to end a run on purpose.

## Step 6: Run the automated tests

1. Menu **Window > General > Test Runner**.
2. Click the **EditMode** tab, then **Run All**.
3. Every test should get a green tick. A red cross means a failing test: click it, copy the message at the bottom
   of the Test Runner window, and send it to the team.
4. Optional: the **PlayMode** tab has a few more tests; they take longer.

## Step 7 (after the first successful open): send the generated settings back

Unity created settings files that should be saved in the repository so everyone, including the automatic test
server, uses the same settings. In GitHub Desktop:

1. Open the **Changes** tab. You will see new or changed files under `UnityProject/ProjectSettings/`,
   `UnityProject/Packages/packages-lock.json`, `UnityProject/Assets/_Game/Config/Rendering/`,
   `UnityProject/Assets/_Game/Scenes/`, and a few `UnityProject/Assets/*.asset` files from the graphics package.
2. Type a summary such as `Add project settings from first open` and click **Commit to (branch)**, then
   **Push origin**.

If you would rather not, just tell the team; they can explain or guide you through it.

---

## Verify on the Mac after the review and smoothness pass (2026-10-07, all uncompiled)
Compile first (warnings are errors), then check these by hand and report what fails:
1. First run, first mistake: the tutorial rescues you with no death sound and no dead pose (a hint appears instead).
2. Swipe slowly, then flick: a flick after a slow drag still swipes. Tapping Pause or a menu button never moves Pista.
3. Settings: only Music, Sound effects and Reduce motion show (Haptics is hidden on purpose). Turn Reduce motion on
   during a run: no vine slow-motion, no camera tilt or shake, no shards or speed lines (applies at once).
4. Die, then look at the Continue screen: the "Your coins" number is the wallet plus this run's coins, and Continue is
   enabled when that covers the price. After Continue, Game Over and the main menu show a wallet without double counting.
5. Press Home during a run, wait, force-quit, relaunch: the run's coins and best score are saved.
6. Leave the app for a minute during the Continue offer or a pause: the timer did not burn.
7. Obstacles: walk a long run and watch for flicker or missing obstacles (pieces now keep a slot per obstacle id). The
   boulder art should fill its box better (visual only; its hitbox is unchanged).
8. Smoothness: on an iPhone 11 / SE 2 development build, check the Profiler steps per frame (expect 1) and compare with
   GPU skinning on (Player settings) and the renderer intermediate texture on Auto (URP-Mobile-Renderer). Hitch hunt at
   the first coin, obstacle, vine, power-up, gateway and restart; the prewarm (RunPrewarm) runs behind the main menu.
   If you see a one-frame flash or a "Camera.Render" warning at startup, send it: the off-screen prewarm is the
   riskiest new piece.
9. Console (Development build): no per-gateway "World segment" lines in a release build; no "event buffer overflow" errors.

## Optional: play on your iPhone (free Apple ID)

You need **Xcode 26** (free in the Mac App Store; about 15 GB; it may ask you to update macOS first) and your
iPhone's cable. A free Apple ID is enough for your own phone. Limits of a free Apple ID: the app stops opening after
**7 days** (just build again), and at most 3 such apps per phone.

### A. Prepare the iPhone (once)
1. Connect the iPhone to the Mac with the cable. On the phone, tap **Trust** and enter the passcode.
2. Turn on **Developer Mode** (iOS 16 or later): iPhone **Settings > Privacy & Security > Developer Mode** > on.
   The phone restarts and asks you to confirm. (The option appears only after the phone has been connected to Xcode
   once; if you don't see it, do step C first and come back.)

### B. Export the Xcode project from Unity
1. In Unity: **File > Build Profiles**. Select **iOS** in the list and click **Switch Platform** (several minutes the
   first time).
2. Check that **Scene List** contains `Assets/_Game/Scenes/Run` with its tick on.
3. Click **Build**. Create and choose the folder `UnityProject/Builds/iOS` (git ignores this folder). Wait until
   Unity finishes; Finder opens the folder.

### C. Sign and run with Xcode
1. Double-click **`Unity-iPhone.xcodeproj`** in that folder to open it in Xcode.
2. **Xcode > Settings > Accounts** > **+** > **Apple ID**, and sign in.
3. In the left column click the blue **Unity-iPhone** project icon, then the **Unity-iPhone** target, then the
   **Signing & Capabilities** tab:
   - Tick **Automatically manage signing**.
   - **Team**: choose "*Your Name* (Personal Team)".
   - If Xcode says the bundle identifier is not available, change **Bundle Identifier** to something unique,
     for example `com.pistaduko.junglerunner.yourname`.
4. At the top of the window, choose your iPhone as the run destination (next to "Unity-iPhone").
5. Press the **Run** button (triangle). The first build takes a few minutes.
6. The first time, the iPhone refuses to open the app ("Untrusted Developer"). On the phone:
   **Settings > General > VPN & Device Management** > your Apple ID under "Developer App" > **Trust**. Then tap the
   app icon (named "Jungle Runner").

---

## Troubleshooting

### Compile errors on first open ("Enter Safe Mode?", red errors in the Console)
The game code did not compile on your Unity version. This is the most likely first-open problem, because the code
was written without a Unity editor available. It is not your fault and you cannot break anything.

1. Click **Enter Safe Mode** (or **Ignore**; both are fine).
2. Send the team the complete log file. In Finder press **Cmd+Shift+G**, paste `~/Library/Logs/Unity/`, and send
   **`Editor.log`** (attach the file; it is plain text).
3. Also send the Unity version (Unity menu > **About Unity**) and the Console's first red error: open
   **Window > General > Console**, click the first red line, and copy the text shown in the lower half
   (select it and press Cmd+C).
4. Quit Unity. After the team pushes a fix: **Pull origin** in GitHub Desktop and open the project again.

### Package errors ("An error occurred while resolving packages", "Cannot find package")
1. Check the internet connection and that you are signed in to Unity Hub.
2. Quit Unity. Delete the folder `UnityProject/Library` (it is only a cache and is rebuilt). Open the project again.
3. If one package keeps failing, send `Editor.log` (see above) and the text of the error. As a workaround the team
   may ask you to open **Window > Package Manager**, select that package, and click **Update** to the version Unity
   suggests.

### Everything is pink (or black) when playing
Pink means the graphics settings and the materials do not match.
1. Check **Edit > Project Settings > Graphics**: **Default Render Pipeline** should say **URP-Mobile**.
   If it is empty, run **JungleBooze > Setup > Run Project Setup**, then press Play again.
2. Still pink: use **JungleBooze > Setup > Fallback: Use Built-in Renderer** and press Play. Tell the team which of
   the two worked and send `Editor.log`.

### The input restart prompt, or keyboard does nothing
- If Unity asks to enable the input backends or to restart: answer **Yes / Restart now**.
- If you clicked No: **Edit > Project Settings > Player > Other Settings > Active Input Handling** > **Both**, then
  let Unity restart.
- Keys do nothing while playing: click once inside the Game view (it needs focus), then try again.

### The setup dialog never appeared
Run **JungleBooze > Setup > Run Project Setup** from the menu bar. If the **JungleBooze** menu is missing, the code
did not compile: see "Compile errors".

### Pressing Play shows an empty or blue screen and nothing happens
Make sure the **Run** scene is open (Step 5.1). Then look in the Console for red errors and send them with
`Editor.log`.

### Xcode problems
- "Signing for Unity-iPhone requires a development team": pick your Personal Team (Step C.3).
- "Failed to register bundle identifier": change the bundle identifier as in Step C.3.
- "Could not launch ... Untrusted Developer": trust the developer on the phone (Step C.6).
- "Developer Mode disabled": Step A.2.
- Build errors in Xcode: copy the red error lines (or use Xcode's **Report navigator**, last build, "Export") and send
  them to the team.
