# Release guide: getting the game onto your iPhone with TestFlight

Owner: release-engineer. Audience: the owner, no technical background needed.

This guide sets up your Mac once. After that, one command builds the game, signs it, and sends it to TestFlight,
Apple's app for installing test builds on your iPhone. The first build is needed for the G3 feel check (end of week 2).

Each step is labeled:
- **[Owner does this]**: you do it by hand, once.
- **[Automated]**: the build script does it. Nothing for you to do.
- **[Team does this]**: the agent team does it before your first build.

Plan about 2 hours for steps 1–9, plus waiting time for Apple to approve your developer membership (step 3).
**Start step 3 first.** Approval can take from a few hours to several days, and nothing can be uploaded until it is done.

---

## Apple's current minimum (checked 2026-10-06)

| Requirement | Current rule | Source |
|---|---|---|
| Xcode version for uploads | **Xcode 26 or later**, with the **iOS 26 SDK** or later (in force since 28 April 2026) | [Apple: Upcoming requirements](https://developer.apple.com/news/upcoming-requirements/) |
| Lowest iOS version an app may target | iOS 13 or later (since 9 September 2026). Our game targets iOS 15 (Unity 6.3 minimum), so it is fine | same page |
| Announced next step | From **April 2027**: built with the **iOS 27 SDK** or later and targeting iOS 15 or later | [Apple: Submitting to the App Store](https://developer.apple.com/app-store/submitting/) |

**How to check it yourself:**
1. Open the two Apple pages above and look for the newest entry that mentions "Xcode" or "SDK".
2. On your Mac, open **Terminal** and type `xcodebuild -version`. The first line (for example `Xcode 26.1`) must be at
   least the version Apple names.
3. Or just run `tools/build/build_ios.sh --check`. It compares your Xcode with the numbers in
   `tools/build/apple-minimums.env` and stops with a clear message if Xcode is too old.

The release engineer re-checks Apple's pages before every submission and updates `tools/build/apple-minimums.env`.

---

## Step 1: Install the basic tools [Owner does this]

1. **Update macOS** (Apple menu > System Settings > General > Software Update). The newest Xcode needs a recent macOS.
   The Xcode page in the Mac App Store shows the minimum macOS.
2. **Install Xcode** from the Mac App Store (search "Xcode"; it is free and large, about 1 hour to download).
   - Open Xcode once. Accept the license and let it install its extra parts.
   - If it asks which platforms to install, tick **iOS**.
   - Open **Terminal** (press Cmd+Space, type "Terminal", press Enter) and run:
     ```
     sudo xcode-select -s /Applications/Xcode.app
     ```
     It asks for your Mac password. Nothing appears while you type; that is normal.
3. **Install Homebrew** (a free tool installer). Go to [brew.sh](https://brew.sh), copy the one line shown there,
   paste it into Terminal and press Enter. At the end it prints two or three "Next steps" commands. Run them too.
4. **Install Ruby, Git LFS and the GitHub tool** by running this in Terminal:
   ```
   brew install ruby git-lfs gh
   git lfs install
   ```
   macOS's built-in Ruby is too old. The build script finds Homebrew's Ruby by itself.

## Step 2: Install Unity 6 with iOS support [Owner does this]

1. Download **Unity Hub** from [unity.com/download](https://unity.com/download) and sign in with your Unity account
   (the same free Personal account used for the GitHub test checks).
2. In Unity Hub, open **Installs > Install Editor**. Install exactly the version written in
   `UnityProject/ProjectSettings/ProjectVersion.txt` (currently `6000.3.0f1`). If that exact version is not listed,
   use **Archive** > "download archive", or ask the team.
3. When it asks for modules, tick **iOS Build Support**. (Already installed? Installs > gear icon next to the version >
   Add modules > iOS Build Support.)
4. Leave Unity in its default place (`/Applications/Unity/Hub/Editor/...`). The script looks for it there.

## Step 3: Join the Apple Developer Program [Owner does this, start first]

1. Go to [developer.apple.com/programs/enroll](https://developer.apple.com/programs/enroll/) and enroll with your Apple
   Account (two-factor authentication must be on). It costs 99 USD per year.
2. Choose **Individual** or **Organization** (see the open question about this in the status board). Organization needs
   a D-U-N-S number and usually takes longer.
3. When approved, open [developer.apple.com/account](https://developer.apple.com/account) > **Membership details** and
   write down your **Team ID** (10 characters, for example `ABCDE12345`).

## Step 4: Register the app [Owner does this]

1. **Bundle ID.** This is the app's permanent internal ID. It cannot be changed after the first upload. The team will
   propose the format and you decide it (open question). Then go to developer.apple.com >
   **Certificates, Identifiers & Profiles > Identifiers > +** > App IDs > App. Enter a description (for example
   "Jungle runner") and the **Explicit** bundle ID you chose. Leave the capabilities as they are and click Register.
2. **Create the app in App Store Connect.** Go to [appstoreconnect.apple.com](https://appstoreconnect.apple.com) > Apps > **+** > New App:
   - Platforms: iOS
   - Name: a working name. It must be unique on the App Store and must **not** contain "Booze". You can change it later.
     Nobody outside your testers sees it until release.
   - Primary language: English (U.S.)
   - Bundle ID: the one from step 4.1
   - SKU: any private label, for example `jungle-runner-001`
   - User access: Full Access

## Step 5: Create the App Store Connect API key [Owner does this]

The key lets the build script upload without your Apple password or two-factor codes.

1. App Store Connect > **Users and Access > Integrations > App Store Connect API**. If asked, request access and accept.
2. Under **Team Keys**, click **+** (Generate API Key). Name: `Build Mac`. Access: **Admin**. Admin is needed once to
   create the signing certificate in step 9. You may later replace it with an "App Manager" key just for uploads.
3. Write down the **Issuer ID** (shown above the list) and the **Key ID** (in the list).
4. Click **Download API Key**. You can download it **only once**. Move the file `AuthKey_XXXXXXXXXX.p8` into the hidden
   folder `~/.junglebooze/` (create it with `mkdir -p ~/.junglebooze` in Terminal, then in Finder press
   Cmd+Shift+G and type `~/.junglebooze`).
5. Never put this file in the project folder, email it, or paste it in a chat. Also keep a copy in your password manager.

## Step 6: Create a private repository for signing certificates [Owner does this]

Apple requires every build to be signed. The signing certificate and profile are stored **encrypted** in a separate,
**private** GitHub repository, so any Mac you use later can get them.

1. In Terminal, sign in to GitHub (choose GitHub.com, HTTPS, and "Login with a web browser"):
   ```
   gh auth login
   gh auth setup-git
   ```
2. Create the private repository (the name is up to you):
   ```
   gh repo create ios-certificates --private
   ```
   Its address is `https://github.com/<your GitHub name>/ios-certificates.git`.
3. Pick a long **encryption password** for it and save it in your password manager. This is `MATCH_PASSWORD` in step 8.

## Step 7: Get the project onto your Mac [Owner does this]

If you have not cloned the project yet:
```
mkdir -p ~/Projects && cd ~/Projects
gh repo clone <your GitHub name>/JungleBooze
cd JungleBooze
```
Open the project once in Unity Hub (Projects > Add > choose the `UnityProject` folder), wait for the import to
finish, then **quit Unity**. Builds cannot run while Unity has the project open. (This is also the G0 task in the
status board. Tell the team when it is done so they can commit the settings Unity creates.)

## Step 8: Fill in your private settings file [Owner does this]

1. Create the file:
   ```
   tools/build/build_ios.sh --init
   ```
   This creates `~/.junglebooze/release.env`, outside the project, readable only by you.
2. Open it: `open -e ~/.junglebooze/release.env` and fill in:

   | Setting | Where it comes from |
   |---|---|
   | `JB_BUNDLE_ID` | Step 4.1 |
   | `APPLE_TEAM_ID` | Step 3.3 |
   | `ASC_KEY_ID`, `ASC_ISSUER_ID` | Step 5.3 |
   | `ASC_KEY_PATH` | Full path of the `.p8` file, for example `/Users/<you>/.junglebooze/AuthKey_ABC123.p8` |
   | `MATCH_GIT_URL` | Step 6.2 |
   | `MATCH_PASSWORD` | Step 6.3 |

3. Save and close. Check everything:
   ```
   tools/build/build_ios.sh --check
   ```
   It tests Xcode, Unity, Ruby, your settings, the project folder and the App Store Connect key, and tells you exactly
   what to fix if something is wrong. It builds nothing. (It also checks the project is on `main`; see
   "Before the first build" below.)

## Step 9: Create the signing certificate (one time) [Owner starts it, the rest is automated]

Do this after steps 5, 6 and 8.
```
tools/build/build_ios.sh --certs-setup
```
**[Automated]** It creates an "Apple Distribution" certificate and an App Store provisioning profile for your bundle
ID, encrypts them with your `MATCH_PASSWORD`, and stores them in the private repository. If macOS asks for your
**login keychain password**, that is your Mac login password; choose "Always Allow".

Every later build only *reads* the certificates (it never creates new ones by accident). Apple allows only a few
distribution certificates per account, so do not run `--certs-setup` again unless the team asks you to.

## Step 10: Build and upload [Owner runs one command, the rest is automated]

```
cd ~/Projects/JungleBooze
git checkout main && git pull
tools/build/build_ios.sh
```

**[Automated]**, in this order:
1. Checks Xcode against Apple's minimum, Unity and its iOS module, Ruby/fastlane, and your settings.
2. Checks the project folder has no local changes, is on `main`, and matches GitHub exactly. Builds always come from
   reviewed, merged code.
3. Picks the next build number (the latest build on TestFlight + 1) and reads the version from the `VERSION` file.
4. Downloads the signing certificate (read-only).
5. Unity creates the Xcode project (5–30 minutes).
6. Xcode compiles, archives and signs the app.
7. Uploads to TestFlight with a "What to Test" note and waits until Apple has processed it (10–30 minutes).

The Mac stays awake during the build. Logs are saved in `UnityProject/Builds/logs/`. If anything fails, the script
says what happened and where the log is. Send that log file to the team.

## Step 11: Install on your iPhone with TestFlight [Owner does this]

1. App Store Connect > your app > **TestFlight > Internal Testing > +** to create a group, for example "Feel check".
   Add yourself (and anyone else on your App Store Connect team, up to 100 people). Turn on
   **automatic distribution** so new builds reach the group automatically.
2. On the iPhone, install **TestFlight** from the App Store and sign in with the same Apple Account.
   Accept the invitation email.
3. When a build finishes processing, it appears in TestFlight. Tap **Install**. TestFlight builds expire after 90 days.
4. Outside testers (friends, G7 beta) need an external group and a short Apple review. The team sets that up in week 6.

---

## Before the first build [Team does this]

These are not your tasks, but the first build cannot succeed without them:
- A `main` branch with the merged, reviewed game code, and green "Unity tests" on GitHub.
- The project settings Unity creates on first open (step 7) committed: iOS platform, iPhone only, portrait, IL2CPP,
  ARM64, Metal, minimum iOS 15 (see `docs/ARCHITECTURE.md` section 2.1). Also `Packages/packages-lock.json`.
- At least one scene in the build scene list (File > Build Profiles), for example the gray-box test course.
- App icon placeholder and launch screen for the TestFlight build.

## Version and build numbers [Automated]

- **Version** (what people see, for example `0.1.0`) is in the `VERSION` file at the top of the project. The team raises
  it for milestones (semantic versioning: major.minor.patch).
- **Build number** goes up by one with every upload. It is read from TestFlight, so it never repeats, no matter which
  Mac or runner builds.
- Each build writes a small record (`UnityProject/Builds/logs/build-<number>.txt`) with the exact commit it came from.

## Crash reports

Not set up yet. Apple's own crash reports (Xcode > Window > Organizer > Crashes) already work for TestFlight builds,
because the build uploads its debug symbols to Apple. A dedicated crash reporting service needs approval from the
tech-architect and a privacy declaration by appstore-compliance first.

## Optional: start builds from GitHub [Owner does this, only if wanted]

You can let GitHub start builds on your Mac (for example from your phone) through `.github/workflows/build-ios.yml`.
It is off by default. To turn it on:
1. GitHub > the repository > **Settings > Actions > Runners > New self-hosted runner > macOS**. Run the shown
   commands on the Mac, logged in as your normal user. When asked for labels, add `junglebooze-mac`. Then run
   `./svc.sh install` and `./svc.sh start` in the runner folder so it starts with the Mac.
2. **Settings > Secrets and variables > Actions > Variables > New variable**: `IOS_SELF_HOSTED_RUNNER` = `true`.
3. **Actions > iOS build (owner's Mac) > Run workflow** on `main`. The Mac must be on and awake.

Your passwords and keys stay on the Mac (the workflow reads `~/.junglebooze/release.env`). The workflow builds only
from `main`, only after the tests passed for that exact commit, and only when you press "Run workflow".
**Keep the repository private** while a runner is registered.

## Troubleshooting

| Message | What to do |
|---|---|
| "Xcode NN is too old" | Mac App Store > Updates > Xcode. Then run `--check` again |
| "Full Xcode is not selected" | `sudo xcode-select -s /Applications/Xcode.app` |
| "Unity ... was not found" / "iOS Build Support is not installed" | Step 2 |
| "Unity is open" | Quit Unity, run again |
| "The project folder has changes that are not on GitHub" | Do not delete anything. Send the list to the team |
| "builds must come from 'main'" | `git checkout main && git pull`, run again |
| "App Store Connect key file not found" | Check `ASC_KEY_PATH` in `~/.junglebooze/release.env` (step 8) |
| A password prompt for "login keychain" | Your Mac login password, "Always Allow" |
| Anything else | Send the log file named in the message to the team |

## Files

| File | Purpose |
|---|---|
| `tools/build/build_ios.sh` | The one command you run |
| `tools/build/release.env.example` | Template for your private settings file |
| `tools/build/apple-minimums.env` | Apple's current minimum Xcode/SDK numbers |
| `fastlane/Fastfile` | Build steps (lanes `preflight`, `beta`, `certs`) |
| `fastlane/Appfile`, `fastlane/Matchfile`, `Gemfile`, `Gemfile.lock` | Tool settings (no secrets) |
| `UnityProject/Assets/_Game/Editor/Build/BuildScript.cs` | Unity batch-mode entry point that creates the Xcode project |
| `VERSION` | App version shown to players |
| `.github/workflows/build-ios.yml` | Optional builds from GitHub on your Mac |
