# JungleBooze: Jungle Adventure Runner (iOS)

A 3D endless runner for iOS built with Unity 6 LTS + C#. The game is built by a team of AI agents,
defined in `.claude/agents/`. Read `docs/AGENT_PLAN.md` for the full plan.

## Ground rules for every agent
1. **The owner decides identity and taste.** Hero, companion, art style, name, icon, prices, and "is it fun" are
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
10. **Honest reporting.** Report failing tests, missed budgets, and skipped steps exactly as they are.
11. **No AI attribution in git or GitHub. This is a hard rule.** Commit messages, author/committer fields, trailers
    (no `Co-Authored-By`, no session links), PR titles and bodies, review comments, tags, and release notes
    never contain the name of the AI tool, its vendor, or any model name. Commits are authored as the owner.

## Conventions
- C#: `PascalCase` types/methods, `_camelCase` private fields, one type per file, namespaces `JungleBooze.<Layer>`.
- Assemblies: `JungleBooze.Core`, `.Gameplay`, `.UI`, `.Services`, `.Editor`, `.Tests.EditMode`, `.Tests.PlayMode`.
- Commits: imperative mood, reference the issue (`Add lane switching (#12)`).
- Docs: decisions → `design/DECISIONS.md`; technical decisions → `docs/adr/NNNN-title.md`.
