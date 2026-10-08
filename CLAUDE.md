# JungleBooze: AURELIA, an endless exploration runner (iOS)

A 3D endless exploration runner for iOS built with Unity 6 LTS + C#. The binding vision is
`design/aurelia/BLUEPRINT.md`. Files from the earlier lane-based "Jungle Adventure Runner" vision are in
`archive/pre-aurelia/`: history only, never build from them. The game is built by a team of AI agents,
defined in `.claude/agents/`. Read `docs/AGENT_PLAN.md` for the full plan.

## Starting a session
1. Read `docs/STATUS.md` first. It shows the current milestone, each agent's state, open owner questions,
   assumptions, and what hasn't been verified. Then read `design/DECISIONS.md`.
2. The coordinating session updates `docs/STATUS.md` (agent table + log line) every time it launches an agent,
   receives an agent's report, or records an owner decision, and commits it with that work.

3. **Local development (owner, 2026-10-09).** Work happens on the owner's M4 Mac in `/Users/hash/Projects/JungleBooze`,
   not in cloud containers. Unity 6000.3.25f1 (with iOS Build Support), Xcode, Homebrew and git-lfs are installed;
   API keys live in `~/.config/junglebooze/secrets.env` (never print or commit them). Installing tools on the Mac is
   allowed. Disk space is tight (~24 GB free on 2026-10-09): keep scratch output out of the repo and clean up builds.
   Headless checks: `tools/ci/unity.sh compile` and `tools/ci/unity.sh test` (EditMode/PlayMode).
   When a session's context grows large (about 40% full), it writes a "Handoff" note in `docs/STATUS.md`
   (just finished, in progress, what to launch next), commits, and the next local session carries on from it.
   Commit agent work after every report so nothing is lost if a session stops.
   **Token economy (owner, 2026-10-09):** the coordinating session stays lean and delegates most work to subagents
   (`.claude/agents/`); it plans, launches, verifies and commits. It may end itself and start fresh (via the Handoff
   note) whenever carrying on would cost more than a clean start.

4. **Current mandate (owner, 2026-10-09): build AURELIA into a professional, fully polished iOS game, autonomously.**
   The core vision document `design/aurelia/BLUEPRINT.md` (with `design/aurelia/vision_board.png`) is binding: never
   move away from it. Gameplay must feel professional. Build so it passes Apple App Review with no surprises
   (`docs/APP_STORE_CHECKLIST.md`). Follow the phase plan in `docs/STATUS.md`. Think, test, validate and improve
   freely; use your own recommendations instead of asking the owner; mark them `[ASSUMED]` and log them in
   `docs/STATUS.md`. Test constantly yourself: compile, run tests, render previews/screenshots, and look at the
   results before calling anything done.
   **Money is the exception: always ask the owner** before spending money or API credits, with the expected cost.
   Keep going on free work while a budget answer is pending.
   The old lane-based direction lives only in `archive/pre-aurelia/`. It must never leak back into the game.
   **Keep the owner updated with pictures:** send renders, previews and screenshots (SendUserFile) whenever there is
   something visual worth seeing, with a one-line caption.

## Ground rules for every agent
1. **The owner decides identity and taste.** Hero, creatures/companions, art style, name, icon, prices, and "is it fun" are
   the owner's calls. Prepare options, but never decide these yourself. Check `design/DECISIONS.md` first.
   Subagents never ask the owner directly: end your report with an **Open questions for owner** section
   (each with 2–4 options and a recommendation). The coordinating session asks the owner and logs the answer.
   Where a question doesn't block you, proceed with your recommended default and mark it `[ASSUMED]`.
2. **Specs before code.** Gameplay work starts from a game-designer spec with acceptance criteria.
3. **Tests with every change.** Gameplay logic is plain C# with EditMode tests. Behavior inside scenes gets PlayMode tests.
4. **Data-driven.** Tuning numbers live in ScriptableObjects under `Assets/_Game/Config`, never hard-coded in code.
5. **Deterministic core.** Gameplay runs from a seed and a fixed timestep. No `UnityEngine.Random` or
   `Time.deltaTime` in simulation code. Use the injected `IRandom` / `ITimeSource`.
6. **Mobile budgets are hard limits.** 60 fps on the lowest supported iPhone. Budgets are in `docs/ARCHITECTURE.md`.
   No allocations in the per-frame hot path. Pool everything spawned.
7. **Original IP only.** No trademarked names, characters, or brands (no Tarzan, no real car brands, no
   "Subway Surfers"). Record every third-party asset/SDK and its license in `docs/LICENSES.md`.
8. **Apple-safe by default.** Anything touching ads, tracking, purchases, or user data must be checked
   against `docs/APP_STORE_CHECKLIST.md` by appstore-compliance.
9. **Small branches, reviewed.** Every change goes through code-reviewer, and CI must be green before merge.
10. **Quality over schedule (owner's rule).** Never cut quality, polish, or content to hit a date. If something isn't
    at full quality, it isn't done, and the launch waits. Report schedule risk early instead of shipping a lighter version.
11. **Honest reporting.** Report failing tests, missed budgets, and skipped steps exactly as they are.
12. **No AI attribution in git or GitHub. This is a hard rule.** Commit messages, author/committer fields, trailers
    (no `Co-Authored-By`, no session links), PR titles and bodies, review comments, tags, and release notes
    never contain the name of the AI tool, its vendor, or any model name. Commits are authored as the owner.

## Conventions
- C#: `PascalCase` types/methods, `_camelCase` private fields, one type per file, namespaces `JungleBooze.<Layer>`.
- Assemblies: `JungleBooze.Core`, `.Gameplay`, `.UI`, `.Services`, `.App` (composition root), `.Editor`, `.Tests.EditMode`, `.Tests.PlayMode`.
- Commits: imperative mood, reference the issue (`Add route split chunks (#12)`).
- Docs: decisions → `design/DECISIONS.md`; technical decisions → `docs/adr/NNNN-title.md`.
