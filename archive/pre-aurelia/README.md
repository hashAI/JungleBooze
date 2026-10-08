# Archive: pre-AURELIA vision

On 2026-10-08 the owner switched the game to AURELIA (`design/aurelia/BLUEPRINT.md`, binding). The files here belong
to the earlier "Jungle Adventure Runner" vision: a lane-based runner with a wild jungle kid, Duko the macaw, four
worlds (Jungle, River, Mountains, Ancient Ruins) and the stylized "Inkbound Pulp" art style.

**Do not build from these files.** They are kept for history and for ideas that may be reused on purpose.
When something here conflicts with the AURELIA documents or `design/DECISIONS.md`, the AURELIA side wins.

| File | Was | Replaced by |
|---|---|---|
| `docs/GDD.md` | Master game design (lanes, macaw, 4 worlds) | `design/aurelia/BLUEPRINT.md`; a new AURELIA GDD is written in Phase 1 |
| `docs/specs/001-player-movement.md` | Lane movement spec (FP1 code was built from it) | New movement spec from blueprint Parts V–VI (Phase 1) |
| `docs/specs/002-track-obstacles-coins.md` | Lane track, obstacles, coins, generator | New chunk/route specs from blueprint Parts VII–VIII, XLI–XLIII |
| `docs/sim-reports/2026-10-07-spec001.md` | Bot report against spec 001 | New reports once the AURELIA movement spec exists |
| `design/STYLE_GUIDE.md` | Binding "Inkbound Pulp" style guide | `design/aurelia/ART_DIRECTION.md` |
| `design/HERO_CONCEPTS.md`, `design/STYLE_BOARDS.md` | G2 option sheets for the old hero, macaw and style | `design/aurelia/ART_DIRECTION.md` |
| `design/prompts/*` | Prompt library for the stylized hero, macaw and style boards | `design/prompts/aurelia/` |
| `design/prompts/duko_realistic.md` | Realistic Duko prompts | Duko dropped by the owner (2026-10-08); companions come later as discoverable creatures |
| `design/concepts/2026-10-07/` | Stylized hero and macaw concept images | `design/concepts/2026-10-08/` (realistic Pista) |

Still in place on purpose:
- The FP1 Unity code (`UnityProject/Assets/_Game/Scripts`) and `tools/sim/` model the archived spec 001. They are
  reviewed piece by piece in Phase 1 and reused where they fit the blueprint (deterministic core, run lifecycle,
  save, audio playback); lane-specific code is replaced, not patched.
- The old Duko voice lines and jungle music under `UnityProject/Assets/_Game/Audio` stay until AURELIA audio exists.
