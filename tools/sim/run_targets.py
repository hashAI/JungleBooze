"""Run spec 001 simulation targets S1-S9 and write the sim report.

Usage (from tools/sim):
    python3 -I run_targets.py s1 [--n 10000] [--workers 4]   # long: oracle over 360k segments
    python3 -I run_targets.py s2 | s3 | bots | s8 | s9
    python3 -I run_targets.py report                          # writes docs/sim-reports/2026-10-07-spec001.md
    python3 -I run_targets.py all                             # everything, then report

Results are cached as JSON in tools/sim/out/ so the report can be rebuilt without rerunning.
"""

import json
import math
import os
import sys
import time
from collections import Counter
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from runner_model import (  # noqa: E402
    RunnerConfig, Runner, make_obstacle, NONE, MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE, PAUSE_RESUMED,
    LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK, OUTCOMES, EXPIRED,
)
from test_course import (  # noqa: E402
    gauntlet_segment, run_course, fixed_course, TIERS, S1_SPEEDS,
)
from bots import OracleSolver, SkillBot, PROFILES, run_commands, make_runner  # noqa: E402

OUT = os.path.join(HERE, "out")
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
REPORT = os.path.join(REPO, "docs", "sim-reports", "2026-10-07-spec001.md")


def _save(name, data):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ".json"), "w") as f:
        json.dump(data, f, indent=1, sort_keys=True)


def _load(name):
    p = os.path.join(OUT, name + ".json")
    if not os.path.exists(p):
        return None
    with open(p) as f:
        return json.load(f)


# ---------------------------------------------------------------------------
# S1: oracle on gauntlet segments
# ---------------------------------------------------------------------------
def _s1_chunk(args):
    speed, tier, seeds = args
    oracle = OracleSolver(node_budget=300000, full_budget=600000)
    res = Counter()
    fails = []
    nodes_max = 0
    stretched = 0
    t0 = time.perf_counter()
    for seed in seeds:
        c = gauntlet_segment(seed, speed, tier)
        stretched += 1 if c.stretched else 0
        st, cmds, nodes = oracle.solve(c)
        nodes_max = max(nodes_max, nodes)
        res[st] += 1
        if st == "solved":
            # verify the found command list on a fresh runner (no search state reused)
            r = make_runner(c)
            for cmd in cmds:
                r.step(cmd)
            for _ in range(120):
                r.step()
            if r.dead or r.stumbles:
                res["verify_failed"] += 1
                fails.append(dict(seed=seed, status="verify_failed", course=c.describe(),
                                  start_lane=c.start_lane))
        else:
            fails.append(dict(seed=seed, status=st, course=c.describe(), start_lane=c.start_lane, nodes=nodes))
    return dict(speed=speed, tier=tier, counts=dict(res), fails=fails, nodes_max=nodes_max,
                stretched=stretched, seconds=time.perf_counter() - t0)


def s1(n=10000, workers=4):
    jobs = []
    chunk = 250
    for speed in S1_SPEEDS:
        for tier in sorted(TIERS):
            base = int(speed * 1000) * 100000 + tier * 10000000  # disjoint seed ranges per cell
            for k in range(0, n, chunk):
                jobs.append((speed, tier, list(range(base + k, base + min(n, k + chunk)))))
    t0 = time.perf_counter()
    cells = {}
    with Pool(workers) as pool:
        for i, r in enumerate(pool.imap_unordered(_s1_chunk, jobs)):
            key = "%s|%d" % (r["speed"], r["tier"])
            c = cells.setdefault(key, dict(speed=r["speed"], tier=r["tier"], counts=Counter(), fails=[],
                                           nodes_max=0, stretched=0, n=0))
            c["counts"].update(r["counts"])
            c["fails"].extend(r["fails"])
            c["nodes_max"] = max(c["nodes_max"], r["nodes_max"])
            c["stretched"] += r["stretched"]
            c["n"] += sum(v for k2, v in r["counts"].items() if k2 != "verify_failed")
            if i % 40 == 0:
                print("s1 %d/%d chunks, %.0fs" % (i + 1, len(jobs), time.perf_counter() - t0), flush=True)
    for c in cells.values():
        c["counts"] = dict(c["counts"])
    data = dict(n_per_cell=n, seconds=time.perf_counter() - t0, cells=sorted(cells.values(),
                key=lambda c: (c["speed"], c["tier"])), config_hash=RunnerConfig().config_hash())
    _save("s1", data)
    return data


# ---------------------------------------------------------------------------
# S2: success windows per archetype per speed
# ---------------------------------------------------------------------------
def _single_course(speed, arch, z_front):
    return fixed_course(speed, [("single", z_front, {1: arch})], start_lane=1)


def _clear(r, end_z):
    for _ in range(400):
        if r.dead or r.stumbles:
            return False
        if r.z - 0.25 > end_z + 0.5 and r.state in ("Running", "Sliding"):
            return True
        r.step()
    return not (r.dead or r.stumbles)


def _window(speed, arch, cmd, phase):
    dz = speed / 60.0
    z_front = 3.0 * speed + 0.5 + phase * dz
    course = _single_course(speed, arch, z_front)
    # contact tick when doing nothing
    r = make_runner(course)
    while not r.dead and r.tick < 2000:
        r.step()
    contact = r.death["tick"]
    ok = []
    base = make_runner(course)
    for t in range(0, contact + 3):
        rr = base.clone()
        rr.step(cmd)
        if _clear(rr, course.end_z):
            ok.append(t)
        base.step()
    return contact, ok


def s2():
    rows = []
    for speed in S1_SPEEDS:
        for arch, cmd, label in ((LOW_BARRIER, JUMP, "jump over low barrier"),
                                 (HIGH_BARRIER, SLIDE, "slide under high barrier"),
                                 (FULL_BLOCK, MOVE_RIGHT, "lane dodge full block")):
            per_phase = []
            for p in range(10):
                contact, ok = _window(speed, arch, cmd, p / 10.0)
                contiguous = bool(ok) and (ok[-1] - ok[0] + 1 == len(ok))
                per_phase.append(dict(phase=p / 10.0, contact=contact, n=len(ok),
                                      first=(contact - ok[0]) if ok else None,
                                      last=(contact - ok[-1]) if ok else None, contiguous=contiguous))
            rows.append(dict(speed=speed, archetype=arch, action=label,
                             min_window=min(x["n"] for x in per_phase),
                             max_window=max(x["n"] for x in per_phase),
                             latest_before_contact_max=max(x["last"] for x in per_phase if x["last"] is not None),
                             latest_before_contact_min=min(x["last"] for x in per_phase if x["last"] is not None),
                             all_contiguous=all(x["contiguous"] for x in per_phase),
                             phases=per_phase))
    gap_rows = []
    for speed in S1_SPEEDS:
        for glen in (2.0, 3.0, 4.0):
            ns = []
            for p in range(10):
                z0 = 3.0 * speed + 0.5 + p / 10.0 * speed / 60.0
                course = fixed_course(speed, [("gap", z0, glen)])
                base = make_runner(course)
                ok = []
                for t in range(0, int(z0 / (speed / 60.0)) + 20):
                    rr = base.clone()
                    rr.step(JUMP)
                    if _clear(rr, course.end_z):
                        ok.append(t)
                    base.step()
                    if base.dead:
                        break
                ns.append(len(ok))
            gap_rows.append(dict(speed=speed, gap_m=glen, min_window=min(ns), max_window=max(ns)))
    _save("s2", dict(rows=rows, gap_rows=gap_rows))
    return rows, gap_rows


# ---------------------------------------------------------------------------
# S3: jump-then-jump over two full-width low barrier rows at 21 m/s
# ---------------------------------------------------------------------------
def s3(speed=21.0):
    oracle = OracleSolver(node_budget=300000, full_budget=300000)
    allrow = {0: LOW_BARRIER, 1: LOW_BARRIER, 2: LOW_BARRIER}
    rows = []
    sp = 0.20
    while sp <= 0.9001:
        feas = []
        for p in range(10):
            z1 = 2.0 * speed + p / 10.0 * speed / 60.0
            c = fixed_course(speed, [("row", z1, allrow), ("row", z1 + sp * speed, allrow)])
            st, _, _ = oracle.solve(c)
            feas.append(st == "solved")
        rows.append(dict(spacing_s=round(sp, 2), feasible_phases=sum(feas), phases=len(feas)))
        sp = round(sp + 0.05, 2)
    # explicit two-jump scan: Jump commands only, second jump may be buffered
    two = []
    sp = 0.30
    while sp <= 0.9001:
        ok_phases = 0
        one_jump_phases = 0
        for p in range(5):
            z1 = 2.0 * speed + p / 5.0 * speed / 60.0
            c = fixed_course(speed, [("row", z1, allrow), ("row", z1 + sp * speed, allrow)])
            ok, single = _two_jump_scan(c)
            ok_phases += ok
            one_jump_phases += single
        two.append(dict(spacing_s=round(sp, 2), two_jump_phases=ok_phases, one_jump_phases=one_jump_phases,
                        phases=5))
        sp = round(sp + 0.01, 2)
    _save("s3", dict(speed=speed, rows=rows, two_jump=two))
    return rows, two


def _two_jump_scan(course):
    """Returns (two separate jumps can clear both rows, one jump clears both rows)."""
    hd = 0.25
    g1 = course.groups[0]
    base = make_runner(course)
    # contact tick with row 1 when doing nothing
    r = base.clone()
    while not r.dead:
        r.step()
    c1 = r.death["tick"]
    single = False
    two = False
    pre = base.clone()
    for j1 in range(max(0, c1 - 45), c1 + 1):
        while pre.tick < j1:
            pre.step()
        a = pre.clone()
        a.step(JUMP)
        # does the first jump alone clear both rows?
        b = a.clone()
        if _clear(b, course.end_z):
            single = True
            continue  # this j1 needs no second jump; only count real jump-then-jump below
        # clear row 1 first
        b = a.clone()
        while not (b.dead or b.stumbles) and b.z - hd <= g1.z1 + 0.01:
            b.step()
        if b.dead or b.stumbles:
            continue
        for j2 in range(j1 + 1, j1 + 80):
            x = a.clone()
            while x.tick < j2 and not (x.dead or x.stumbles):
                x.step()
            if x.dead or x.stumbles:
                break
            x.step(JUMP)
            if _clear(x, course.end_z):
                # require that a second jump actually started
                if _jumps_started(course, j1, j2) == 2:
                    two = True
                    break
        if two and single:
            break
    return two, single


def _jumps_started(course, j1, j2):
    r = Runner(course.cfg, track=course.track, obstacles=course.obstacles, record_events=True)
    r.target_lane = course.start_lane
    r.x = course.cfg.lane_center(course.start_lane)
    for _ in range(j2 + 80):
        cmd = JUMP if r.tick in (j1, j2) else NONE
        r.step(cmd)
    return sum(1 for e in r.events if e[1] == "JumpStarted")


# ---------------------------------------------------------------------------
# S4-S7: skill bots
# ---------------------------------------------------------------------------
BOT_SETUPS = [
    dict(target="S4", bot="expert", speed=10.0, tier=1, duration_s=60, runs=1000),
    dict(target="S5", bot="average", speed=10.0, tier=1, duration_s=60, runs=1000),
    dict(target="S6", bot="new", speed=8.0, tier=1, duration_s=30, runs=1000),
]

# Informative matrix (no target): bots at each tier's spacing and a speed that fits the tier
MATRIX_SPEED = {1: 10.0, 2: 12.0, 3: 13.5, 4: 15.5, 5: 17.5, 6: 21.0}


def _bot_run(setup, seed):
    course = run_course(seed, setup["speed"], setup["tier"], setup["duration_s"],
                        gap_max=setup.get("gap_max", 4.0))
    bot = SkillBot(PROFILES[setup["bot"]], seed)
    plan = bot.plan(course)
    max_ticks = int(setup["duration_s"] * 60) + 90
    r, stream, _ = run_commands(course, plan, max_ticks)
    survived = (not r.dead)
    death = None
    if r.dead:
        d = r.death
        lane = course.obstacle_lane.get(d["obstacle_id"]) if d["obstacle_id"] is not None else None
        old_lane_60 = False
        if lane is not None and lane != d["target_lane"]:
            old_lane_60 = abs(d["x"] - course.cfg.lane_center(lane)) >= course.cfg.edge_forgiveness_m - 1e-9
        if d["cause"] == "Fell":
            cause = "Fell"
        elif d["after_stumble"]:
            cause = "Tripped twice (%s)" % d["archetype"]
        else:
            cause = "Hit %s (%s)" % (d["archetype"], d["entry"])
        # group being passed at death: first group whose far end is not yet behind HERO's back
        gi = None
        if d["cause"] == "Fell":
            for i, g in enumerate(course.groups):
                if g.kind == "gap" and g.z0 <= d["z"]:
                    gi = i
        else:
            for i, g in enumerate(course.groups):
                if g.z1 + 0.6 >= d["z"] - 0.25:
                    gi = i
                    break
        death = dict(cause=cause, tick=d["tick"], tripped_twice=bool(d["after_stumble"]),
                     old_lane_60=old_lane_60, group=gi,
                     error_injected=(gi in bot.error_groups) if gi is not None else False)
    return dict(seed=seed, survived=survived, ticks=r.tick, death=death, stumbles=len(r.stumbles),
                outcomes=r.outcomes, flags=r.flags_received, buffered_entered=r.buffered_entered,
                groups=len(course.groups), stretched=course.stretched)


def _bot_job(args):
    setup, seeds = args
    return [_bot_run(setup, s) for s in seeds]


def _summ(setup, runs):
    n = len(runs)
    surv = sum(1 for r in runs if r["survived"])
    deaths = [r["death"] for r in runs if r["death"]]
    causes = Counter(d["cause"] for d in deaths)
    oc = Counter()
    for r in runs:
        oc.update(r["outcomes"])
    buffered = sum(r["buffered_entered"] for r in runs)
    flags = sum(r["flags"] for r in runs)
    fail_seeds = [r["seed"] for r in runs if not r["survived"]][:15]
    return dict(setup=setup, n=n, survived=surv, survive_rate=surv / float(n), deaths=len(deaths),
                causes=dict(causes), tripped_twice=sum(1 for d in deaths if d["tripped_twice"]),
                old_lane_60=sum(1 for d in deaths if d["old_lane_60"]),
                deaths_error_injected=sum(1 for d in deaths if d["error_injected"]),
                causes_execution=dict(Counter(d["cause"] for d in deaths if not d["error_injected"])),
                stumbles=sum(r["stumbles"] for r in runs), outcomes=dict(oc), flags=flags,
                accounting_ok=(sum(oc.values()) == flags), buffered_entered=buffered,
                expired=oc.get(EXPIRED, 0),
                expired_share=(oc.get(EXPIRED, 0) / float(buffered)) if buffered else 0.0,
                fail_seeds=fail_seeds,
                median_ticks=sorted(r["ticks"] for r in runs)[n // 2])


def _seed_slot(st):
    """Variants reuse the seeds of the setup they compare against."""
    order = {("S4", "expert"): 0, ("S5", "average"): 1, ("S6", "new"): 2,
             ("S6-gap3", "new"): 2, ("S5-gap3", "average"): 1,
             ("no-decision-errors", "expert-noerr"): 0, ("no-decision-errors", "average-noerr"): 1,
             ("no-decision-errors", "new-noerr"): 2}
    key = (st["target"], st["bot"])
    if key in order:
        return order[key]
    return 10 + ("expert", "average", "new").index(st["bot"]) * 10 + st["tier"]


def bots(workers=4, matrix_runs=300):
    setups = list(BOT_SETUPS)
    for bot in ("expert", "average", "new"):
        for tier, sp in sorted(MATRIX_SPEED.items()):
            setups.append(dict(target="matrix", bot=bot, speed=sp, tier=tier, duration_s=60, runs=matrix_runs))
    # variants for recommendations (same seeds as the target setups)
    setups.append(dict(target="S6-gap3", bot="new", speed=8.0, tier=1, duration_s=30, runs=1000, gap_max=3.0))
    setups.append(dict(target="S5-gap3", bot="average", speed=10.0, tier=1, duration_s=60, runs=1000, gap_max=3.0))
    for prof, er in (("expert", 0.0), ("average", 0.0), ("new", 0.0)):
        setups.append(dict(target="no-decision-errors", bot=prof + "-noerr",
                           speed=10.0 if prof != "new" else 8.0, tier=1,
                           duration_s=60 if prof != "new" else 30, runs=1000))
    jobs = []
    for si, st in enumerate(setups):
        base = 7000000 + _seed_slot(st) * 100000
        seeds = list(range(base, base + st["runs"]))
        for k in range(0, len(seeds), 100):
            jobs.append((st, seeds[k:k + 100]))
    with Pool(workers) as pool:
        out = pool.map(_bot_job, jobs)
    per = {}
    for (st, _), chunk in zip(jobs, out):
        key = "%s|%s|%s|%s|%s" % (st["target"], st["bot"], st["tier"], st["speed"], st.get("gap_max", 4.0))
        per.setdefault(key, (st, []))[1].extend(chunk)
    summaries = [_summ(st, runs) for st, runs in per.values()]
    # write per-run rows for the three target setups
    rows = []
    for st, runs in per.values():
        if st["target"] != "matrix":
            for r in runs:
                rows.append(dict(target=st["target"], bot=st["bot"], speed=st["speed"], tier=st["tier"],
                                 spacing_s=TIERS[st["tier"]]["min_spacing_s"], **r))
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "bot_runs.jsonl"), "w") as f:
        for row in rows:
            f.write(json.dumps(row, sort_keys=True) + "\n")
    _save("bots", dict(summaries=summaries, config_hash=RunnerConfig().config_hash()))
    return summaries


# ---------------------------------------------------------------------------
# S8: determinism (record -> replay, with pauses)
# ---------------------------------------------------------------------------
def s8(n=1000):
    import random
    mism = []
    t0 = time.perf_counter()
    for i in range(n):
        seed = 9100000 + i
        tier = 1 + i % 6
        speed = (8.0, 10.0, 13.5, 17.5, 21.0, 33.6)[i % 6]
        course = run_course(seed, speed, tier, 45)
        prof = PROFILES[("expert", "average", "new")[i % 3]]
        plan = SkillBot(prof, seed).plan(course)
        rng = random.Random(seed)
        if i % 2 == 0:  # put a resume-after-pause into half the runs
            t = rng.randrange(30, 45 * 60)
            plan[t] = plan.get(t, 0) | PAUSE_RESUMED
        r1, stream, h1 = run_commands(course, plan, 45 * 60 + 90, hashes=True)
        # replay from the recorded stream on a fresh runner (sparse frames like InputRecording)
        rec = dict((t, c) for t, c in enumerate(stream) if c)
        r2, _, h2 = run_commands(course, rec, 45 * 60 + 90, hashes=True)
        if h1 != h2 or r1.state_hash() != r2.state_hash():
            mism.append(seed)
    data = dict(n=n, identical=n - len(mism), mismatched_seeds=mism, seconds=time.perf_counter() - t0)
    _save("s8", data)
    return data


def s9(ticks=200000):
    course = run_course(424242, 13.5, 3, 300)
    plan = SkillBot(PROFILES["expert"], 424242).plan(course)
    r = make_runner(course)
    t0 = time.perf_counter()
    n = 0
    while n < ticks and not r.dead:
        r.step(plan.get(r.tick, NONE))
        n += 1
    dt = time.perf_counter() - t0
    data = dict(python_mean_step_us=dt / n * 1e6, ticks=n)
    _save("s9", data)
    return data


if __name__ == "__main__":
    args = sys.argv[1:]
    what = args[0] if args else "all"
    n = 10000
    workers = 4
    if "--n" in args:
        n = int(args[args.index("--n") + 1])
    if "--workers" in args:
        workers = int(args[args.index("--workers") + 1])
    if what in ("s1", "all"):
        print(json.dumps(s1(n, workers)["seconds"]))
    if what in ("s2", "all"):
        s2()
    if what in ("s3", "all"):
        s3()
    if what in ("bots", "all"):
        bots(workers)
    if what in ("s8", "all"):
        s8()
    if what in ("s9", "all"):
        s9()
    if what in ("report", "all"):
        from report import write_report
        write_report(REPORT, OUT)
        print("wrote", REPORT)
