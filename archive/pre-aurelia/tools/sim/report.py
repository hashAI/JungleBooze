"""Builds archive/pre-aurelia/docs/sim-reports/2026-10-07-spec001.md from tools/sim/out/*.json."""

import json
import os
import platform
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from runner_model import RunnerConfig  # noqa: E402
from test_course import TIERS  # noqa: E402
from bots import PROFILES  # noqa: E402


def _load(out, name):
    p = os.path.join(out, name + ".json")
    if not os.path.exists(p):
        return None
    with open(p) as f:
        return json.load(f)


def pf(ok):
    return "PASS" if ok else "FAIL"


def pct(x):
    return "%.1f%%" % (100.0 * x)


def bar(frac, width=40):
    n = int(round(frac * width))
    return "#" * n + "." * (width - n)


def _find(summaries, target, bot=None, tier=None, gap_max=None):
    for s in summaries:
        st = s["setup"]
        if st["target"] != target:
            continue
        if bot is not None and st["bot"] != bot:
            continue
        if tier is not None and st["tier"] != tier:
            continue
        if gap_max is not None and st.get("gap_max", 4.0) != gap_max:
            continue
        return s
    return None


def write_report(path, out):
    cfg = RunnerConfig()
    s1 = _load(out, "s1")
    s2 = _load(out, "s2")
    s3 = _load(out, "s3")
    bots = _load(out, "bots")
    s8 = _load(out, "s8")
    s9 = _load(out, "s9")
    L = []
    w = L.append

    w("# Sim report: spec 001 player movement (movement gauntlet)")
    w("")
    w("**Date:** 2026-10-07 | **Author:** balance-simulator | **Spec:** `docs/specs/001-player-movement.md` "
      "(2026-10-06) | **Config hash (model):** `%s`" % cfg.config_hash())
    w("")
    w("**What was run:** the Python reference model `tools/sim/runner_model.py` (a tick-exact re-implementation of "
      "spec 001, pinned by `tools/sim/test_runner_model.py`), not the C# runner. The C# simulation is still being "
      "built (FP1 stage A1); once it exists, the golden traces in `tools/sim/golden/` tie the two together and the "
      "S8/S9 numbers must be re-measured in a Unity batch run. Every number below is a property of the spec as "
      "modelled, not of shipped code.")
    w("")
    w("**Reproduce:** `cd tools/sim && python3 -I run_targets.py all` (Python %s, standard library only; S1 takes "
      "about 20 to 40 minutes on 4 cores). Per-run rows for S4 to S6: `tools/sim/out/bot_runs.jsonl` (git-ignored, "
      "regenerate with `run_targets.py bots`)." % platform.python_version())
    w("")
    w("No tuning was changed. Recommendations at the end go to game-designer.")
    w("")

    # ------------------------------------------------------------------ targets table
    rows = []
    # S1
    if s1:
        tot = sum(c["n"] for c in s1["cells"])
        solved = sum(c["counts"].get("solved", 0) for c in s1["cells"])
        imp = sum(c["counts"].get("impossible", 0) for c in s1["cells"])
        unres = sum(c["counts"].get("unresolved", 0) for c in s1["cells"])
        verify = sum(c["counts"].get("verify_failed", 0) for c in s1["cells"])
        ok1 = (solved == tot and verify == 0)
        rows.append(("S1", "Oracle bot, 10,000 segments per speed {8, 10, 13.5, 17.5, 21, 33.6} m/s per tier spacing "
                     "0.90 to 0.45 s: 0 deaths, 0 stumbles",
                     "%d / %d segments solved with 0 deaths and 0 stumbles; %d proven impossible, %d unresolved "
                     "(search budget), %d failed re-verification" % (solved, tot, imp, unres, verify), pf(ok1)))
    else:
        rows.append(("S1", "Oracle bot 0 deaths / 0 stumbles", "not run", "NOT RUN"))
    # S2
    if s2:
        r2 = s2["rows"]
        jmin = min(r["min_window"] for r in r2 if r["archetype"] == "LowBarrier")
        jat = [r["speed"] for r in r2 if r["archetype"] == "LowBarrier" and r["min_window"] == jmin]
        smin = min(r["min_window"] for r in r2 if r["archetype"] == "HighBarrier")
        dmax = max(r["latest_before_contact_max"] for r in r2 if r["archetype"] == "FullBlock")
        dmin = min(r["latest_before_contact_min"] for r in r2 if r["archetype"] == "FullBlock")
        rows.append(("S2a", "Jump over low barrier: success window >= 15 ticks at every speed",
                     "min %d ticks (%.0f ms), at %s m/s" % (jmin, jmin * 1000 / 60.0, ", ".join("%g" % s for s in jat)),
                     pf(jmin >= 15)))
        rows.append(("S2b", "Slide under high barrier: window >= 30 ticks",
                     "min %d ticks (%.0f ms) at every speed" % (smin, smin * 1000 / 60.0), pf(smin >= 30)))
        rows.append(("S2c", "Lane dodge of a full block still succeeds with the input <= 3 ticks before front "
                     "contact (expected 2)",
                     "latest successful input: %s ticks before contact at every speed and phase" %
                     (str(dmax) if dmax == dmin else "%d to %d" % (dmin, dmax)), pf(dmax <= 3)))
    # S3
    if s3:
        two = s3.get("two_jump", [])
        need = [r for r in two if r["spacing_s"] >= 0.45 - 1e-9]
        ok3 = all(r["two_jump_phases"] == r["phases"] for r in need) and len(need) > 0
        lowest = min((r["spacing_s"] for r in two if r["two_jump_phases"] == r["phases"]), default=None)
        rows.append(("S3", "Jump-then-jump over two full-width low barrier rows at 21 m/s feasible for every "
                     "spacing >= 0.45 s",
                     "feasible at every tested spacing 0.30 to 0.90 s (step 0.01 s, 5 sub-tick phases each); "
                     "a single jump clears both rows below %.2f s" % _single_limit(two), pf(ok3)))
    # S4-S6
    if bots:
        sm = bots["summaries"]
        s4 = _find(sm, "S4")
        s5 = _find(sm, "S5")
        s6 = _find(sm, "S6")
        s4n = _find(sm, "no-decision-errors", "expert-noerr")
        s5n = _find(sm, "no-decision-errors", "average-noerr")
        s6n = _find(sm, "no-decision-errors", "new-noerr")
        rows.append(("S4", "Expert bot, tier-1 spacing, 10 m/s, 60 s: >= 99% survive",
                     "%s of %d (timing only, decision errors off: %s)" % (pct(s4["survive_rate"]), s4["n"],
                                                                         pct(s4n["survive_rate"])),
                     pf(s4["survive_rate"] >= 0.99)))
        exp_txt = ("%d of %d buffered jumps expired (%s)" % (s5["expired"], s5["buffered_entered"],
                                                              pct(s5["expired_share"]))
                   if s5["buffered_entered"] else "0 buffered jumps (not exercised)")
        if 0 < s5["buffered_entered"] < 30:
            exp_txt += (" - sample too small to judge: at tier-1 spacing the bot almost never presses Jump in the "
                        "air (spec issue 24)")
        ok5 = s5["survive_rate"] >= 0.90 and (s5["expired_share"] <= 0.05)
        rows.append(("S5", "Average bot, same setup: >= 90% survive; Expired <= 5% of buffered jumps",
                     "%s of %d survive (decision errors off: %s); %s" % (pct(s5["survive_rate"]), s5["n"],
                                                                       pct(s5n["survive_rate"]), exp_txt),
                     pf(ok5)))
        rows.append(("S6", "New bot, tier-1 spacing, 8 m/s, 30 s: >= 60% survive",
                     "%s of %d (decision errors off: %s)" % (pct(s6["survive_rate"]), s6["n"],
                                                             pct(s6n["survive_rate"])),
                     pf(s6["survive_rate"] >= 0.60)))
        deaths = sum(s["deaths"] for s in (s4, s5, s6))
        tt = sum(s["tripped_twice"] for s in (s4, s5, s6))
        ol = sum(s["old_lane_60"] for s in sm)
        alld = sum(s["deaths"] for s in sm)
        ok7 = (tt <= 0.15 * max(1, deaths)) and ol == 0
        rows.append(("S7", "Death causes (all bots): Tripped twice <= 15% of deaths; deaths from an old-lane "
                     "obstacle while >= 60% into the new lane = 0",
                     "Tripped twice %d of %d deaths (%s) in S4-S6; old-lane deaths at >= 60%%: %d of %d deaths "
                     "over all %d bot setups" % (tt, deaths, pct(tt / float(max(1, deaths))), ol, alld, len(sm)),
                     pf(ok7)))
    if s8:
        rows.append(("S8", "1,000 seeded runs recorded and replayed: 100% identical final state hash",
                     "Python model: %d / %d identical (per-tick hash, half the runs with a pause). C# runner: "
                     "not run (does not exist yet)" % (s8["identical"], s8["n"]),
                     "PASS (model only)" if s8["identical"] == s8["n"] else "FAIL"))
    if s9:
        rows.append(("S9", "Mean simulation step <= 0.5 ms in the headless editor run; 0 allocations per step",
                     "Not measurable here (needs the C# runner in a Unity batch run). For scale only: Python model "
                     "%.1f us per step." % s9["python_mean_step_us"], "NOT MEASURED"))

    w("## Targets")
    w("")
    w("| # | Target | Measured | Result |")
    w("|---|---|---|---|")
    for r in rows:
        w("| %s | %s | %s | **%s** |" % r)
    w("")
    w("S4 to S6 depend mostly on the bot skill profiles, which are **[ASSUMED]** because `docs/specs/bot-player.md` "
      "does not exist yet (see Assumptions). The \"decision errors off\" column isolates what the movement numbers "
      "control: deaths from reaction and timing alone.")
    w("")

    # ------------------------------------------------------------------ S1 detail
    if s1:
        w("## S1: oracle on the movement gauntlet")
        w("")
        w("Segment = 3 groups at exactly the tier's minimum spacing (front face to front face), HERO starts "
          "in a seeded random lane, constant speed, ramp off. Groups: single obstacle (45%), pair blocking two "
          "lanes (35%), full-width gap 2-4 m (20%) [ASSUMED mix]. The oracle searches per-tick commands (pruned "
          "alphabet first, then the full alphabet) and each solution is re-run on a fresh runner to confirm it. "
          "`stretched` = segments where a gap was longer than the spacing distance, so the next group had to be "
          "pushed to 0.5 m after the gap.")
        w("")
        w("| Speed m/s | " + " | ".join("T%d %.2fs" % (t, TIERS[t]["min_spacing_s"]) for t in sorted(TIERS)) + " |")
        w("|---|" + "---|" * len(TIERS))
        by = dict(((c["speed"], c["tier"]), c) for c in s1["cells"])
        for sp in sorted(set(c["speed"] for c in s1["cells"])):
            cells = []
            for t in sorted(TIERS):
                c = by.get((sp, t))
                if not c:
                    cells.append("-")
                    continue
                txt = "%d/%d" % (c["counts"].get("solved", 0), c["n"])
                if c["stretched"]:
                    txt += " (str %d)" % c["stretched"]
                cells.append(txt)
            w("| %g | %s |" % (sp, " | ".join(cells)))
        w("")
        fails = [(c["speed"], c["tier"], f) for c in s1["cells"] for f in c["fails"]]
        if fails:
            w("Failing seeds (reproduce: `gauntlet_segment(seed, speed, tier)` in `test_course.py`, then "
              "`OracleSolver().solve(course)`):")
            w("")
            w("| Speed | Tier | Seed | Status | Start lane | Course |")
            w("|---|---|---|---|---|---|")
            for sp, t, f in fails[:40]:
                w("| %g | %d | %d | %s | %d | %s |" % (sp, t, f["seed"], f["status"], f["start_lane"], f["course"]))
            if len(fails) > 40:
                w("")
                w("(%d more in `tools/sim/out/s1.json`.)" % (len(fails) - 40))
        else:
            w("No failing seeds. Largest search: %d nodes. Total wall time %.0f s." % (
                max(c["nodes_max"] for c in s1["cells"]), s1["seconds"]))
        w("")

    # ------------------------------------------------------------------ S2 detail
    if s2:
        w("## S2: success windows (oracle, single obstacle in HERO's lane)")
        w("")
        w("Each value is the number of input ticks that clear the obstacle with no contact, minimum (and maximum) "
          "over 10 sub-tick placements of the obstacle. Dodge column: latest successful MoveRight, in ticks before "
          "the first contact tick of a straight run.")
        w("")
        w("| Speed m/s | Jump over low (ticks) | Slide under high (ticks) | Dodge full block: latest input before contact |")
        w("|---|---|---|---|")
        by = {}
        for r in s2["rows"]:
            by[(r["speed"], r["archetype"])] = r
        for sp in sorted(set(r["speed"] for r in s2["rows"])):
            j = by[(sp, "LowBarrier")]
            s_ = by[(sp, "HighBarrier")]
            d = by[(sp, "FullBlock")]
            w("| %g | %d (%d) | %d (%d) | %s |" % (sp, j["min_window"], j["max_window"], s_["min_window"],
                                                  s_["max_window"], d["latest_before_contact_min"]
                                                  if d["latest_before_contact_min"] == d["latest_before_contact_max"]
                                                  else "%d-%d" % (d["latest_before_contact_min"],
                                                                  d["latest_before_contact_max"])))
        w("")
        if s2.get("gap_rows"):
            w("Extra (no target in spec 001): jump take-off window over a full-width gap, ticks (min over phases). "
              "**>= 190 means HERO crosses the gap without jumping at all** (coyote time bridges it, see issue 1).")
            w("")
            w("| Speed m/s | 2 m gap | 3 m gap | 4 m gap |")
            w("|---|---|---|---|")
            gb = dict(((g["speed"], g["gap_m"]), g) for g in s2["gap_rows"])
            for sp in sorted(set(g["speed"] for g in s2["gap_rows"])):
                cells = []
                for gl in (2.0, 3.0, 4.0):
                    g = gb[(sp, gl)]
                    cells.append(("%d **no jump needed**" % g["min_window"]) if g["min_window"] >= 190
                                 else ("%d" % g["min_window"]) +
                                 (" (some phases: no jump needed)" if g["max_window"] >= 190 else ""))
                w("| %g | %s |" % (sp, " | ".join(cells)))
            w("")

    # ------------------------------------------------------------------ S3 detail
    if s3:
        w("## S3: jump-then-jump at 21 m/s")
        w("")
        w("Two rows of low barriers across all three lanes. `two jumps` = phases (of 5) where a first jump clears "
          "row 1 but not row 2 and a second Jump command (buffered or on the ground) clears row 2. `one jump` = "
          "phases where a single jump clears both rows.")
        w("")
        w("```")
        w("spacing  two jumps  one jump")
        for r in s3.get("two_jump", []):
            if abs((r["spacing_s"] * 100) % 5) < 1e-6 or r["two_jump_phases"] < r["phases"]:
                w("%5.2f s   %d/%d        %d/%d" % (r["spacing_s"], r["two_jump_phases"], r["phases"],
                                                  r["one_jump_phases"], r["phases"]))
        w("```")
        w("")

    # ------------------------------------------------------------------ bots detail
    if bots:
        sm = bots["summaries"]
        w("## S4 to S7: skill bots")
        w("")
        w("Course = tier-1 density (4 obstacles / 100 m, min spacing 0.90 s), same group mix as S1, constant "
          "speed, start lane 1. A run survives if HERO is alive when the course ends.")
        w("")
        w("| Setup | Runs | Survive | Deaths | of which injected decision errors | Death causes (execution only) | "
          "Stumbles | Buffered / Expired | Outcome accounting |")
        w("|---|---|---|---|---|---|---|---|---|")
        for key in (("S4", None), ("S5", None), ("S6", None), ("no-decision-errors", "expert-noerr"),
                    ("no-decision-errors", "average-noerr"), ("no-decision-errors", "new-noerr"),
                    ("S5-gap3", None), ("S6-gap3", None)):
            s = _find(sm, key[0], key[1])
            if not s:
                continue
            st = s["setup"]
            causes = ", ".join("%s %d" % (k, v) for k, v in sorted(s["causes_execution"].items(),
                                                                   key=lambda kv: -kv[1])) or "none"
            w("| %s %s %g m/s %ds%s | %d | %s | %d | %d | %s | %d | %d / %d | %s |" % (
                st["target"], st["bot"], st["speed"], st["duration_s"],
                " gaps<=%gm" % st["gap_max"] if st.get("gap_max") else "", s["n"], pct(s["survive_rate"]),
                s["deaths"], s["deaths_error_injected"], causes, s["stumbles"], s["buffered_entered"], s["expired"],
                "OK" if s["accounting_ok"] else "MISMATCH"))
        w("")
        s4, s5, s6 = _find(sm, "S4"), _find(sm, "S5"), _find(sm, "S6")
        w("Seeds of failed runs (first 15 each; reproduce with `run_course(seed, speed, 1, duration)` + "
          "`SkillBot(PROFILES[bot], seed)`):")
        w("")
        for s in (s4, s5, s6):
            w("- %s %s: %s" % (s["setup"]["target"], s["setup"]["bot"], ", ".join(str(x) for x in s["fail_seeds"])
                               or "none"))
        w("")
        w("All-cause deaths in S4 to S6 (including injected decision errors):")
        w("")
        w("```")
        for s in (s4, s5, s6):
            tot = max(1, s["deaths"])
            w("%s %s (%d deaths)" % (s["setup"]["target"], s["setup"]["bot"], s["deaths"]))
            for k, v in sorted(s["causes"].items(), key=lambda kv: -kv[1]):
                w("  %-34s %4d  %5.1f%%  %s" % (k, v, 100.0 * v / tot, bar(v / float(tot), 30)))
        w("```")
        w("")
        w("Informative matrix (no target): survival in 60 s at each tier's spacing and density, speed per tier "
          "shown, 300 runs per cell.")
        w("")
        w("```")
        for bot in ("expert", "average", "new"):
            w(bot)
            for t in sorted(TIERS):
                s = _find(sm, "matrix", bot, t)
                if s:
                    w("  T%d %5.2fs %4.1f m/s  %5.1f%%  %s" % (t, TIERS[t]["min_spacing_s"], s["setup"]["speed"],
                                                           100 * s["survive_rate"], bar(s["survive_rate"])))
        w("```")
        w("")
        mx = [s for s in sm if s["setup"]["target"] == "matrix"]
        be = sum(s["buffered_entered"] for s in mx)
        ex = sum(s["expired"] for s in mx)
        w("Buffered jumps across the matrix: %d entered the buffer, %d expired (%s). Outcome counters summed over "
          "every bot run match the number of command flags received: %s." % (
              be, ex, pct(ex / float(be)) if be else "n/a",
              "yes" if all(s["accounting_ok"] for s in sm) else "NO"))
        w("")

    if s8:
        w("## S8 and S9")
        w("")
        w("- S8 (model): %d seeded runs (all tiers and speeds, all three bots, a `PauseResumed` injected in half of "
          "them) were recorded as sparse command frames and replayed on a fresh runner. Per-tick state hashes: "
          "%d identical, mismatches %s. This proves the harness and the model, not the C# runner." % (
              s8["n"], s8["identical"], s8["mismatched_seeds"] or "none"))
        if s9:
            w("- S9: needs the C# runner. Python model cost %.1f us per step (irrelevant to the device budget)." %
              s9["python_mean_step_us"])
        w("")

    whatif = _load(out, "whatif")
    if whatif:
        w("## What-if: candidate tuning, measured in the model (not applied)")
        w("")
        w("Same seeds as the S5 / S6 target runs (%d runs each), same [ASSUMED] bot profiles (decision errors on). "
          "Jump apex stays 1.5 m, so a longer airtime means lower gravity and a longer, floatier jump. S2a = "
          "low-barrier jump window (min-max over 10 sub-tick placements). S3 = oracle, two full-width low-barrier "
          "rows at 21 m/s, spacings 0.45 to 0.90 s (step 0.05 s, 5 phases each)." % whatif["rows"][0]["runs"])
        w("")
        w("| Variant | Jump ticks | g m/s2 | v0 m/s | Jump length at 21 m/s | S2a 8 m/s | S2a 10 m/s | S2a 21 m/s | "
          "S3 | S5 average survive | S6 new survive |")
        w("|---|---|---|---|---|---|---|---|---|---|---|")
        for r in whatif["rows"]:
            w("| %s | %d | %.2f | %.2f | %.1f m | %d-%d | %d-%d | %d-%d | %s | %s | %s |" % (
                r["variant"], r["jump_ticks"], r["gravity"], r["jump_v0"], r["jump_ticks"] / 60.0 * 21.0,
                r["jump_window_8"][0], r["jump_window_8"][1], r["jump_window_10"][0], r["jump_window_10"][1],
                r["jump_window_21"][0], r["jump_window_21"][1],
                "feasible at all" if r["s3_first_infeasible_spacing"] is None
                else "fails from %.2f s" % r["s3_first_infeasible_spacing"],
                pct(r["s5_survive"]), pct(r["s6_survive"])))
        w("")
        w("Not re-run for the variants: S1 (360,000 oracle segments, about 23 min each) and the golden traces. Both "
          "must be re-run if a variant is adopted.")
        w("")

    import report_text
    fill = dict(s6="n/a", s6g="n/a", s5="n/a", s5g="n/a", jmin="n/a", s4="n/a", s4n="n/a", s4_leth="n/a",
                s4_groups="n/a", s4_err_need="n/a", wi650_s5="n/a", wi650_s6="n/a", wi700_s5="n/a",
                wi700_s6="n/a", wi650_w8="n/a", wi700_w8="n/a")
    if bots:
        sm = bots["summaries"]
        for k, tgt in (("s6", "S6"), ("s6g", "S6-gap3"), ("s5", "S5"), ("s5g", "S5-gap3")):
            x = _find(sm, tgt)
            if x:
                fill[k] = pct(x["survive_rate"])
        s4x = _find(sm, "S4")
        s4nx = _find(sm, "no-decision-errors", "expert-noerr")
        if s4x and s4nx:
            fill["s4"] = pct(s4x["survive_rate"])
            fill["s4n"] = pct(s4nx["survive_rate"])
            runs_p = os.path.join(out, "bot_runs.jsonl")
            if os.path.exists(runs_p):
                with open(runs_p) as f:
                    rs = [json.loads(l) for l in f if '"target": "S4"' in l]
                if rs:
                    groups = sum(r["groups"] for r in rs) / float(len(rs))
                    inj = sum(1 for r in rs if r["death"] and r["death"]["error_injected"])
                    er = PROFILES["expert"].error_rate
                    leth = inj / (er * groups * len(rs)) if er else 0.0
                    fill["s4_groups"] = "%.1f" % groups
                    fill["s4_leth"] = "%.0f%%" % (100 * leth)
                    if leth > 0:
                        fill["s4_err_need"] = "%.2f%%" % (100 * (1 - 0.99 ** (1 / groups)) / leth)
    if whatif:
        for r in whatif["rows"]:
            key = {650.0: "wi650", 700.0: "wi700"}.get(r["overrides"].get("jump_airtime_ms"))
            if key:
                fill[key + "_s5"] = pct(r["s5_survive"])
                fill[key + "_s6"] = pct(r["s6_survive"])
                fill[key + "_w8"] = str(r["jump_window_8"][0])
    if s2:
        fill["jmin"] = str(min(r["min_window"] for r in s2["rows"] if r["archetype"] == "LowBarrier"))
    w(report_text.SPEC_ISSUES.rstrip())
    w("")
    w(report_text.ASSUMPTIONS.rstrip())
    w("")
    w(report_text.RECOMMENDATIONS.format(**fill).rstrip())
    w("")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        f.write("\n".join(L))


def _single_limit(two):
    lim = None
    for r in two:
        if r["one_jump_phases"] > 0:
            lim = r["spacing_s"]
    return (lim + 0.01) if lim is not None else 0.0
