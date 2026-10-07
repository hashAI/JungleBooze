"""Fixed text sections of the spec 001 sim report (issues, assumptions, recommendations).

Kept in code so `report.py` can rebuild the whole report from `out/`. Placeholders in braces are filled
from measured results by report.py.
"""

SPEC_ISSUES = """\
## Contradictions and ambiguities found in spec 001

Each item says what the reference model does today (`[MODEL-CHOICE]` in `runner_model.py`). The C# runner should
do the same until game-designer rules otherwise, so the golden traces stay valid.

**Gameplay-relevant (need a designer decision)**

1. **Coyote time lets HERO run across short gaps with no jump at all.** Ground is checked under the 0.5 m footprint and
   coyote lasts 5 ticks, so a gap shorter than about `0.5 m + 6 x speed/60` is bridged without a jump: 2 m gaps from
   17.5 m/s, 3 m gaps at the 33.6 m/s boost speed (S2 gap table: "no jump needed"). Spec 001 section 15 still asks
   for 2-4 m gaps, which conflicts with this. **Spec 002 (5.4, F6) already fixes it inside the speed bands**
   (`gapLengthM >= (coyoteTicks + 1) x vMax/60 + 0.5 + 0.25` = 2.85 m at 21 m/s), and the measurements here agree
   with that formula. **Still open:** the Speed Boost (33.6 m/s, spec 002 says it is outside every band) runs across
   the 3.0 m FP1 gap without jumping. See R1.
2. **Gap length vs tier spacing is undefined in spec 001.** A 4 m gap at 8 m/s takes 0.50 s to cross, longer than the
   tier-6 spacing (0.45 s = 3.6 m front to front), so the next group would sit inside the gap. The gauntlet pushes the
   next group to 0.5 m past the gap (`stretched` counts in S1). Spec 002 adds `gapLandingClearM` (1.0 m) and metre
   spacing per tier, which covers this for the generator.
3. **A 4 m gap at 8-10 m/s has the narrowest window in the game:** 15 ticks (250 ms) of take-off at 8 m/s and 21 at
   10 m/s, counting coyote. It is the main killer of the new bot (S6 table). Spec 002 F6 already limits 4.0 m gaps to
   tier 2+ and keeps tutorial speed out of the bands; the `gaps<=3m` rows above measure the effect. See R2.
4. **Jump-from-slide or stand-up while boxes already overlap has no collision category.** 9.3 classifies a contact by
   the axis that started overlapping last, but a hitbox that grows inside a tick (stand-up, jump out of a slide
   under a high barrier) overlaps "from the start". Model: lethal (`inside`), the same as rising into a high barrier.
   Related: 6.4.5 "would overlap" is checked against the end-of-tick box in words, but a swept test (9.1) then sees
   the stand-up tick as a contact. Model checks the standing box over the whole z sweep of the tick.
5. **Side stumble with no active lane move** (for example a mover pushing into HERO, week 3) has no bounce target in
   9.4.2. Model: bounce to `OccupiedLane` (no visible bounce).
6. **Edge forgiveness "moving away from L" (9.5) is undefined for a queued double move and during a stumble bounce.**
   Model: active, non-bounce lane move whose direction points away from L's center.

**Accounting (I9) and same-tick rules**

7. **I9 "exactly one outcome per flag" conflicts with buffering.** A buffered Jump is `Buffered` when received and
   later `Expired`, `Invalidated` or fired, which is two outcomes. S5 then measures "Expired / buffered jumps", which
   needs the number of jumps that entered the buffer as its own counter. Model: the final fate is the outcome
   (fired = `Executed`, replaced by a newer Jump = `Superseded`, `Expired`, `Invalidated`; `Buffered` only if still
   pending when the run ends) plus a separate `bufferedEntered` counter.
8. **No outcome is named for:** a buffered Jump cleared by `PauseResumed` (model: `Invalidated`), an older buffered
   Jump replaced by a newer one (model: `Superseded`), the opposite swipe that cancels a queued move in L4 (model:
   `Executed`), and a stumble-bounce queued lateral replaced by a newer one (L2; model: the first one stays
   `Queued`).
9. **Are `PauseResumed` and `CompanionAssist` "command flags" for I9?** Model: no; only MoveLeft, MoveRight, Jump and
   Slide are counted (AC-29 sum).
10. **Slide while `Falling` with `Y >= 0`** (the tick coyote runs out, or the landing tick of a jump with no ground)
    is not covered by 6.3/6.4. Model: `Ignored`. Jump in the same situation is buffered (matches AC-30).
11. **When does a lateral queued during a stumble bounce start** (L2 "when the bounce ends"): the same tick or the
    next? Model: the next tick's step 8.

**Tick boundaries the spec leaves implicit (pinned by the model and the golden traces)**

12. **Slide length:** "lasts 39 ticks" does not say whether the start tick counts. Model: `Sliding` on ticks
    `s..s+38` (39 ticks, short hitbox in all of them), `Running` on `s+39`. AC-23 "ends 39 ticks after the second
    command" is read the same way.
13. **Fast-fall duration counting:** model drops `Y` on the command tick itself, so from the apex the command tick
    plus 5 more = landing on `c+5` (6 ticks inclusive). If the C# runner does not drop on the command tick it lands
    one tick later (7 inclusive) and AC-19 fails.
14. **Jump `Y` is 0 on the start tick** (`n = 0`), while a lane move changes `X` on its command tick. So a jump's
    first visible movement is one tick later than a swipe's. AC-55 (≤ 1 frame) still holds, but the asymmetry should
    be intended.
15. **Daze boundary:** AC-40 tests 179 and 181 ticks, not 180. Model: a stumble on tick `s` leaves HERO dazed through
    `s+179`; a contact on `s+180` is a new stumble.
16. **AC-02 "7.5 m/s at tick 15"** assumes the base speed stays at 10.0, but the curve has already risen to
    10.0044 m/s at 1.1 m, giving 7.503. The C# test needs a tolerance of about 0.01 (or a flat curve).
17. **`coyoteMs` valid range 0-150 ms**, but `ticks = max(1, ...)` turns 0 ms into 1 tick, so coyote can never be
    switched off. Either the range starts at 17 ms or coyote is exempt from the `max(1, ...)` rule.
18. **"Jump while ... Falling above the surface is buffered" (6.2.5)** vs AC-30 (Jump on `e+6`, when `Y` is still
    exactly 0, is buffered). Model: `Y >= 0` buffers, `Y < 0` ignores.
19. **Fast-fall into a gap** (6.3.6): the fall speed after missing the ground is not given. Model: gravity with the
    fast-fall speed as the initial downward velocity.

**Targets section (15)**

20. S4 to S6 reference `bot-player.md`, which does not exist. The skill profiles decide those results almost
    entirely (see the "decision errors off" columns).
21. S1 "segments placed single or in pairs" does not define a pair (two lanes in one row, or two rows back to back),
    the group mix, or the segment length. The gauntlet definitions used here are in `tools/sim/test_course.py`.
22. S2 lane-dodge wording ("succeeds with the input 3 ticks or fewer before front contact, expected 2") is read as
    "the latest input that still dodges is at most 3 ticks before the contact tick".
23. S8 and S9 measure the C# runner; they cannot be run until it exists. The model-side S8 only proves the harness.
"""

ASSUMPTIONS = """\
## Assumptions [ASSUMED]

- **Bot skill profiles** (no `bot-player.md` yet), fixed before any result was seen and not tuned to targets:

  | Bot | Reaction | Timing noise (1 sigma) | Decision error per obstacle group |
  |---|---|---|---|
  | expert | 250 ms (15 ticks) | 33 ms (2 ticks) | 0.2% |
  | average | 400 ms (24 ticks) | 67 ms (4 ticks) | 0.5% |
  | new | 600 ms (36 ticks) | 100 ms (6 ticks) | 2.0% |

  Bots read the course 34 m ahead (GDD visible distance), answer each group with the GDD colour language (low = jump,
  high = slide, full block = nearest lane that is not a full block, gap = jump), aim at the middle of each success
  window and add Gaussian noise. A decision error is a freeze, the wrong vertical answer or a wrong lane. Lane changes
  wait until the previous group is passed. Bots never use reversals, fast-fall or slide restarts on purpose.
- **Gauntlet:** group mix 45% single / 35% pair / 20% gap; pair = two lanes of one row blocked by random reference
  archetypes; gaps full width, 2-4 m uniform; spacing front face to front face; 3 groups per S1 segment; HERO starts in
  a random lane; constant speed with the start ramp off.
- **Survival** = alive when the course ends (60 s or 30 s of running at constant speed).
- Collision contact times are found with a linear sweep of every box edge inside the tick; a tie within 1e-9 of a
  tick counts as a tie (stumble).
"""

RECOMMENDATIONS = """\
## Recommended changes (for game-designer; none applied)

| # | Change | Where | Expected effect |
|---|---|---|---|
| R1 | Decide what a Speed Boost does over gaps: at 33.6 m/s the 3.0 m FP1 gap is crossed by coyote time alone. Options: (a) boosted HERO auto-jumps gaps, (b) the generator never places 3.0 m gaps where a boost can be active, (c) coyote only applies when the ground does not come back within the coyote distance. Inside the speed bands, keep spec 002 F6 (`>= 2.85 m at 21 m/s`); the measurements here confirm it. | power-ups spec (week 3), spec 002 F6 | No "run across a gap" case at any speed; gaps always mean "jump". |
| R2 | Keep spec 002 F6 as written (4.0 m gaps tier 2+ only, max gap from `vMin`), and also make spec 001 section 15 use the spec 002 gap lengths instead of "2-4 m". Optionally add an S2 target "gap take-off window >= 20 ticks at the band's slowest speed". | spec 001 section 15, spec 002 | New-bot survival in S6 rises from {s6} to {s6g} with gaps <= 3 m (same seeds); average bot (S5) from {s5} to {s5g}. |
| R3 | Write `bot-player.md` with skill profiles that include a per-decision error rate. With the assumed 0.2% expert error rate, S4 (99%) cannot pass on a 24-obstacle minute whatever the movement tuning; for 99% the expert error rate must be <= about 0.06% per obstacle group. | game-designer / bot-player spec | S4-S6 become meaningful tests of movement instead of tests of the bot assumptions. |
| R4 | Keep the low-barrier depth <= 0.6 m and the player hitbox depth <= 0.5 m at tutorial speed: the 8 m/s jump window is {jmin} ticks against the 15-tick target, a 1-tick margin. A 0.7 m barrier would fail S2a at 8 m/s. | spec 002 obstacle sizes | S2a stays green when final obstacle sizes land. |
| R5 | Resolve spec issues 4, 7, 8, 12 and 13 in spec 001 before the C# collision stage (B), because they change tick-exact results that the golden traces and EditMode tests pin. | spec 001 | C# and the reference model agree tick for tick. |
"""
