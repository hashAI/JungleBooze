# Agent Studio Plan: building a professional iOS game with AI agents

This is the plan for building **Jungle Adventure Runner** (working title) with a team of
AI agents. The agents handle planning, building, testing, checking, and
simulating. You handle **taste and identity**: who the hero is, what the world looks
and sounds like, and whether the game feels good in your hands. You also own the
**accounts and money** (Apple, bank, API keys).

The agent definitions (their prompts) are in `.claude/agents/`. The main session acts as the coordinator.
It launches each agent when its work comes up, collects the questions the agents need answered, and asks you in chat.
There are no forms to fill in.

---

## 1. Change of plan: iOS first

The earlier plan was Android first. For iOS first, these change:

| Item | What it means |
|---|---|
| Apple Developer Program | $99/year. You enroll yourself because it needs your legal identity, plus tax and banking forms. |
| A Mac | Unity can create the Xcode project anywhere, but **compiling, signing, and uploading need macOS + Xcode**. You have two options: (a) a Mac you own, or (b) a cloud Mac, such as GitHub Actions macOS runners, Codemagic, or Unity Build Automation. With (b), agents can do the whole build-to-TestFlight pipeline without you touching a Mac. |
| An iPhone | You need at least one real device for feel testing through TestFlight. An older model (for example iPhone 11/SE-class) is the best performance floor. |
| Store rules | Apple's review is stricter than Google's. A dedicated **Compliance agent** handles it (section 6). |
| Leaderboards | Use **Game Center**: free, no server, and players expect it on iOS. |
| Purchases | StoreKit through **Unity IAP**. You need a "Restore Purchases" button. |
| Ads | Ads need the **App Tracking Transparency** prompt, SKAdNetwork IDs, and a privacy manifest. AppLovin MAX or AdMob both work. |
| Build SDK | Apple requires builds made with a recent Xcode/iOS SDK. The Release agent checks the current minimum before each submission. |

---

## 2. What agents can and can't do (honest limits)

**Agents do well:**
- Writing and refactoring all C# game code, editor tools, shaders, and build scripts
- Writing tests and running them in CI (Unity Test Framework via GameCI)
- Running simulations: thousands of bot runs to tune difficulty, plus economy models to tune prices and rewards
- Generating assets through APIs (image generator, Tripo, Meshy, ElevenLabs) and processing them with Blender scripts
- Checking every asset against the budgets (polygon count, texture size, file size)
- Reviewing code, checking App Store compliance, and writing store metadata
- Running CI/CD with Fastlane to get builds onto TestFlight

**Agents can't (this is your part):**
- Feel whether a jump is satisfying or a swipe responds quickly enough on a real phone. Bots measure, and humans judge.
- Pick between art directions. Agents will show you 3 options. They shouldn't choose who your hero is.
- Sign Apple agreements, enter bank or tax details, or pay for tools
- Watch the Unity editor running in real time. Locally, the Unity MCP server gets close. In the cloud, agents work through code, CI, and batch-mode Unity.

**Where things run:**
- *Best setup:* the agent CLI on your Mac, with the Unity editor open and the Unity MCP server connected. Agents can inspect scenes, run play mode, and read the console.
- *Cloud setup (like this session):* agents write code and push it. GitHub Actions runs Unity tests in Linux (GameCI) and builds iOS on a macOS runner. Results come back to the agents as CI events.

---

## 3. The team: 15 agents in 5 departments

```
                         ┌──────────────┐
            YOU ◄──────► │   Producer   │  (orchestrator; the only agent that asks you things)
                         └──────┬───────┘
     ┌──────────────┬───────────┼─────────────┬──────────────────┐
  DESIGN         ENGINEERING   CONTENT       QUALITY            SHIP
  game-designer  tech-architect art-director  qa-engineer        monetization
  balance-sim    gameplay-eng   asset-pipeline perf-engineer      appstore-compliance
                 ui-engineer    audio-director code-reviewer      release-engineer
```

| # | Agent | Job in one line | Main outputs |
|---|---|---|---|
| 1 | **producer** | Breaks the plan into milestones and tasks, sends work to agents, enforces gates, and brings decisions to you | `docs/ROADMAP.md`, GitHub issues, weekly status |
| 2 | **game-designer** | Turns your vision into exact mechanics and numbers | `docs/GDD.md`, feature specs, tuning ScriptableObjects |
| 3 | **balance-simulator** | Proves the design works with simulations before players see it | Bot-run reports, economy model, difficulty curves |
| 4 | **tech-architect** | Sets structure, conventions, and budgets. Approves big technical choices | `docs/ARCHITECTURE.md`, ADRs, asmdefs, CI layout |
| 5 | **gameplay-engineer** | Builds runner, lanes, vine swing, obstacles, power-ups, companion | C# in `Assets/_Game/Scripts/Gameplay` + tests |
| 6 | **ui-engineer** | Builds HUD, menus, shop, settings, onboarding, safe areas, accessibility | UI Toolkit/uGUI screens + tests |
| 7 | **art-director** | Owns the style guide and prompt library. Prepares options for you. Rejects off-style assets | `design/STYLE_GUIDE.md`, mood boards, review notes |
| 8 | **asset-pipeline** | Generates 3D, rig, animation, and texture assets, cleans them in Blender, and imports them within budget | Blender scripts, import presets, asset validator |
| 9 | **audio-director** | Creates SFX and music, mixes them, and tracks licenses | `Assets/_Game/Audio`, `docs/LICENSES.md` |
| 10 | **qa-engineer** | Writes test plans and automated tests, triages bugs, and makes the release/no-release call | Test suites, bug reports, release QA sign-off |
| 11 | **performance-engineer** | Keeps 60 fps on the lowest supported iPhone and controls memory, battery, and app size | Benchmark scene, budget reports |
| 12 | **code-reviewer** | Reviews every change for bugs, architecture drift, and test gaps | PR reviews |
| 13 | **monetization-engineer** | Integrates ads, IAP, ATT, analytics, and remote config without making the game feel worse | Ads/IAP services, frequency caps, consent flow |
| 14 | **appstore-compliance** | Makes sure Apple accepts the app on the first try, and owns metadata and privacy labels | `docs/APP_STORE_CHECKLIST.md`, privacy manifest, review notes |
| 15 | **release-engineer** | Runs CI/CD, signing, versioning, Fastlane, TestFlight, and crash reporting | `.github/workflows`, `fastlane/`, release notes |

The store listing (screenshots, preview video, and ASO text) is shared by art-director and appstore-compliance, so it has no separate agent.

---

## 4. How work flows: one feature, start to finish

Every feature, for example "vine swinging", goes through the same pipeline:

```
1. producer      → creates issue, links GDD section, assigns
2. game-designer → spec: behavior, numbers, edge cases, "feel" targets, acceptance criteria
3. balance-sim   → (if numbers involved) simulate; adjust spec until targets are met
4. tech-architect→ approves approach if it adds systems/dependencies (else skip)
5. engineer      → implements on a branch + EditMode/PlayMode tests
6. qa-engineer   → runs suites, adds regression tests, exploratory bot runs
7. perf-engineer → benchmark scene must stay within budget
8. code-reviewer → adversarial review; must approve
9. CI            → tests + iOS build must be green
10. producer     → merges; if the feature has a FEEL gate → TestFlight build → YOU play and rate
```

**Definition of Done (every change):**
- Acceptance criteria from the spec are met, with a test for each one where possible
- All tests pass in CI, and the iOS build succeeds
- No new compiler warnings or analyzer errors
- Performance and asset budgets are still met
- The code-reviewer approved it
- Docs are updated if behavior or architecture changed

---

## 5. Simulation: how agents "play" the game

Agents can't feel the game, but they can measure it at scale:

1. **Deterministic core.** The gameplay is driven by a seed and a fixed timestep. The same seed and the same inputs always give the same run, so bugs can be reproduced exactly.
2. **Bot player.** `BotInputProvider` uses the same input interface as touch. It has skill levels (reaction time, error rate) that act like a new, average, or expert player.
3. **Headless batch runs.** Unity runs in batch mode, does 1,000+ runs per skill level, and records the data: run length, cause of death, coins, near-misses, vine swing success.
4. **Reports.** balance-simulator turns the data into pass/fail checks against design targets, for example:
   - New player median first run is 25–45 seconds
   - No obstacle pattern that is impossible even with perfect reactions (checked by solver)
   - No single cause of death is behind more than 35% of deaths
   - A first character unlock takes about 3–5 sessions without paying
5. **Economy model.** A spreadsheet/Python model of coin income vs. prices tests how fast players progress with and without ads/IAP.
6. **Fairness fuzzing.** A generator tests the track chunks for unfair spawns: overlaps, unreachable coins, or a gap you can't clear after a slide.

Simulations tell you whether the game is *fair and well-paced*. Only you and your testers can tell whether it's *fun*. Both are required gates.

---

## 6. Getting accepted on the App Store

**appstore-compliance** owns `docs/APP_STORE_CHECKLIST.md` and runs it before every submission. These are the common rejection reasons for this kind of game:

- **2.1 Completeness:** no crashes, no placeholder art or text, every button works, and a working demo path in the review notes
- **2.3 Metadata:** screenshots show real gameplay, and the name/keywords don't use trademarks (no "Tarzan", "Subway Surfers", etc.)
- **3.1.1 IAP:** all digital goods go through StoreKit, there's a **Restore Purchases** button, and any random reward (loot box) shows its odds
- **4.3 Spam:** this matters for your *portfolio* plan. Reskinned copies of the same game get rejected. Each portfolio game needs real, different gameplay.
- **5.1.1 Privacy:** privacy policy URL, App Privacy labels that match the SDKs, `PrivacyInfo.xcprivacy` (including the ones from ad SDKs), the ATT prompt before any tracking, and no account required to play
- **5.1.2 / Kids:** don't choose the Kids category. Ad SDK rules there are much stricter. Pick an age rating (likely 4+ or 9+) that fits the ads shown.
- **Technical:** current Xcode/SDK, all iPhone screen sizes and safe areas, export compliance key (`ITSAppUsesNonExemptEncryption = NO`), and no private APIs

---

## 7. Quality standards ("professional")

**Code**
- Unity 6 LTS and C#. Assembly definitions for each layer (Core, Gameplay, UI, Services, Editor, Tests)
- No game logic in MonoBehaviours where it can be avoided. Plain C# classes that can be tested
- Interfaces for ads, IAP, analytics, save data, and input, so tests and bots can swap them out
- Data-driven tuning: all numbers in ScriptableObjects, never hard-coded in code
- Object pooling for everything spawned. No allocations in the per-frame hot path
- Roslyn analyzers + `.editorconfig` checked in CI. Warnings count as errors

**Player experience**
- 60 fps on the lowest supported device. Cold start under 5 seconds. Download under 200 MB (smaller is better)
- First run: playing within 10 seconds of opening, with no sign-up or wall of menus
- No ads in the first session. Interstitials capped, and rewarded ads are always opt-in
- Haptics, left/right-handed friendly, colorblind-safe danger signals, sound/music toggles
- Saves are crash-safe (atomic writes), and progress backs up to iCloud

---

## 8. Where you come in: the human gates

The producer stops and asks you at these points, and only these. Each decision is recorded in `design/DECISIONS.md` so agents never ask the same thing twice.

| Gate | When | What you decide | How agents prepare it |
|---|---|---|---|
| **G0 Accounts** | Week 0 | Apple Developer, Unity account, API keys (Tripo, Meshy, ElevenLabs, image gen), Mac / cloud Mac | Producer gives you a step-by-step checklist |
| **G1 Creative brief** | Week 0 | Hero, companion, tone, worlds, asked in chat as they come up | game-designer drafts defaults and lists the open questions |
| **G2 Art direction** | Week 1 | Pick 1 of 3 style boards. Approve the hero and companion concept sheets | art-director makes 3 boards + 3 hero variants |
| **G3 Feel check #1** | End of week 2 | Play the gray-box build on TestFlight. Rate swipe response, speed, and jump | TestFlight build + short survey |
| **G4 Hook check** | End of week 3 | Is vine swinging fun? Would *you* post a clip of it? | Build + captured gameplay clips |
| **G5 Monetization** | Week 4 | Ad tolerance, prices, what's for sale | monetization + balance-sim show options with projected effects |
| **G6 Name & icon** | Week 5 | Final app name, icon, subtitle | 5 names checked for trademark conflicts + 3 icons |
| **G7 Beta** | Week 6 | Recruit 10–20 TestFlight testers. Read the summary | Playtest survey + analytics summary |
| **G8 Ship** | Week 7 | Final go / no-go before submitting | Compliance checklist all green + QA sign-off |

---

## 9. Timeline (iOS first, about 7 weeks + review)

| Week | Build | Gates |
|---|---|---|
| 0 | Accounts, Unity project, CI, agent setup, creative brief | G0, G1 |
| 1 | Movement, lanes, jump, slide, deterministic core, bot player. Style boards | G2 |
| 2 | Endless track chunks, obstacles, coins, first simulations, gray-box TestFlight | G3 |
| 3 | Vine swinging, power-ups, companion, game over/continue | G4 |
| 4 | Menus, shop, unlocks, save/iCloud, Game Center. Real art starts replacing gray-box | G5 |
| 5 | Ads, IAP, ATT, audio, VFX, haptics, polish, app icon | G6 |
| 6 | Performance pass, device matrix, external TestFlight beta, store listing | G7 |
| 7 | Bug fixes, compliance run, submit | G8 |

Apple review usually takes 1–3 days. Plan for one possible rejection-and-resubmit cycle.

---

## 10. Repository layout (target)

```
.claude/agents/          agent definitions (this plan)
CLAUDE.md                rules every agent follows
design/                  DECISIONS.md, STYLE_GUIDE.md, concept art refs
docs/                    AGENT_PLAN.md, GDD.md, ARCHITECTURE.md, ROADMAP.md, adr/, APP_STORE_CHECKLIST.md
UnityProject/
  Assets/_Game/{Scripts/{Core,Gameplay,UI,Services},Art,Audio,Prefabs,Scenes,Config}
  Assets/_Game/Tests/{EditMode,PlayMode}
  Packages/ ProjectSettings/
tools/
  blender/               headless cleanup/decimate/export scripts
  assetgen/              Tripo/Meshy/ElevenLabs API scripts
  sim/                   economy model, sim report generators
fastlane/                iOS signing + TestFlight upload
.github/workflows/       test, build-ios, release
```

---

## 11. Next steps

1. **Agents:** tech-architect sets up the Unity project, asmdefs, and CI. game-designer drafts `GDD.md` with defaults and open questions.
2. **You:** answer the questions in chat as the coordinator brings them. art-director then makes the 3 style boards.
3. Work then proceeds through the weekly milestones above.
