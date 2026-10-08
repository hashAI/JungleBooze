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

3. **Automatic handoff (owner-approved, revised 2026-10-08).** Goal: the best use of tokens without ever lowering game
   quality. When the coordinating session's context has grown large (about 40% full; the session estimates this, it
   has no exact gauge), it hands off to a fresh session:
   (a) Prefer handing off right after running agents report. A new session runs in a new container, so files from an
   agent still running are lost unless committed; if the context is getting too full to wait, commit the agents' work
   in progress and list each interrupted task (with its prompt essentials) in the Handoff note so it is relaunched.
   (b) Update `docs/STATUS.md` with a "Handoff" note (just finished, in progress, what to launch next, pending owner
   answers), (c) commit and push, (d) start a new cloud session on the same repo and branch with the prompt
   "Continue the project: read docs/STATUS.md and design/DECISIONS.md, then carry on from the Handoff note",
   (e) create a new resume watchdog bound to the new session and delete the old one (id in `docs/STATUS.md`),
   (f) send the owner the new session's link, and stop working in the old session.
   A pending owner question doesn't block the handoff; it moves into the Handoff note and the new session asks it.

4. **Current mandate (owner, 2026-10-08): build AURELIA to completion, autonomously.** The core vision document
   `design/aurelia/BLUEPRINT.md` (with `design/aurelia/vision_board.png`) is binding: never move away from it.
   Follow the phase plan in `docs/STATUS.md`. Use your own recommendations instead of asking the owner; mark them
   `[ASSUMED]` and log them in `docs/STATUS.md`. Test constantly yourself: compile, run tests, render previews,
   and look at the results before calling anything done.
   **Budgets are the exception: always ask the owner** before spending money or API credits, with the expected cost.
   Keep going on free work while a budget answer is pending.
   When a usage limit is hit, wait for the reset and resume automatically (resume watchdog routine, id in `docs/STATUS.md`).
   Installing tools in the cloud container is allowed (e.g. Blender); installs vanish when the container is reclaimed.
   The owner's Mac and iPhone are where the owner plays and judges each phase.
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
