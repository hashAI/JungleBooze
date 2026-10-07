"""Golden traces, schema junglebooze.golden-trace.v2, for the spec 004 fixed-pivot swing (tools/sim/golden/07..12).

    python3 -I pendulum_golden.py            # (re)write the v2 traces
    python3 -I pendulum_golden.py --check    # verify the files on disk still match pendulum_model.py

Schema v2 is ADDITIVE over v1 (v1 files 01..06 stay valid and byte-identical). New top-level fields: `vines`, `grab`,
`inputs`, `expect`; new per-tick fields: `swing {theta, omega}`, `launch {vx, vy, y0}`, `hand_y`. See README.md.
Positions are world z (path space) with the first vine at VINE_Z; the model itself works in pivot-relative metres.
Reference precision is float64; a C# float implementation must match within `tolerance`.
"""

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pendulum_model as pm  # noqa: E402
from pendulum_model import DEFAULT as P, SwingTrace, release_at, resolve_swipe, guide_chain, chain_catch  # noqa: E402

GOLDEN_DIR = os.path.join(HERE, "golden")
SCHEMA = "junglebooze.golden-trace.v2"
VINE_Z = 100.0
LANE_X = {0: -2.4, 1: 0.0, 2: 2.4}
TOL = dict(x=1e-4, y=1e-4, z=1e-4, theta=5e-6, omega=5e-6)


def r6(x):
    return round(float(x), 6)


def config_block():
    return dict(
        ropeLengthM=P.rope_length_m, grabPointHeightM=P.grab_point_height_m, swingGravityMps2=P.swing_gravity_mps2,
        catchMinSpeedMps=P.catch_min_speed_mps, catchMaxSpeedMps=P.catch_max_speed_mps,
        grabMaxAngleDeg=P.grab_max_angle_deg, handToFeetM=P.hand_to_feet_m, grabBlendMs=P.grab_blend_ms,
        swingMaxMs=P.swing_max_ms, goodStartMs=P.good_start_ms, perfectStartMs=P.perfect_start_ms,
        perfectWidthMs=P.perfect_width_ms, releaseBufferMs=P.release_buffer_ms,
        launchGravityMps2=P.launch_gravity_mps2, perfectImpulseMps=P.perfect_impulse_mps,
        goodImpulseMps=P.good_impulse_mps, poorImpulseMps=P.poor_impulse_mps, impulseAngleDeg=P.impulse_angle_deg,
        releaseMinForwardMps=P.release_min_forward_mps, releaseMinUpMps=P.release_min_up_mps,
        chainArriveFeetM=P.chain_arrive_feet_m, chainFlightMinS=P.chain_flight_min_s,
        chainFlightMaxS=P.chain_flight_max_s, chainSpacingM=P.chain_spacing_m)


def derived_block():
    return dict(pivotHeightM=P.pivot_height_m, goodStartTick=P.good_start_tick, perfectStartTick=P.perfect_start_tick,
                perfectEndTickExclusive=P.perfect_end_tick, releaseBufferTicks=P.release_buffer_ticks,
                swingMaxTicks=P.swing_max_ticks, grabBlendTicks=P.grab_blend_ticks,
                chasmRimZ=None, chasmFarEdgeZ=None)


def carried_ticks(t_off, sP, tr, n_carried, y_grab, x, first_events):
    """Ticks n = 0 .. n_carried-1 of the swing, global tick = t_off + n."""
    res_y = y_grab - tr.feet_y(0)
    out = []
    for n in range(n_carried):
        u = min(n / float(P.grab_blend_ticks), 1.0)
        feet = tr.feet_y(n) + res_y * (1.0 - u) ** 2
        out.append(dict(t=t_off + n, state="Carried", x=x, y=r6(feet), z=r6(sP + tr.hand_z(n)),
                        hand_y=r6(tr.hand_y(n) + res_y * (1.0 - u) ** 2),
                        swing=dict(theta=r6(tr.theta[n]), omega=r6(tr.omega[n])),
                        events=list(first_events) if n == 0 else []))
    return out


def swing_and_flight(t_off, sP, v_in, z_world, y_grab, x, swipe_tick, extra_first_events, guide_to=None):
    """One swing from the grab (global tick t_off) through release and flight.
    Returns (ticks, info). If guide_to is a pivot-relative z of the next vine the release is guided (chain)."""
    tr = SwingTrace(v_in, z_world - sP)
    k, grade = resolve_swipe(swipe_tick, tr.k_apex)
    auto = k is None
    if auto:
        rel = release_at(tr, tr.k_apex, "Poor", pm.DEFAULT, auto=True)
        k = tr.k_apex
        n_carried = k          # ticks 0..k-1 carried; tick k is the release tick with the state after k integrations
        a = k
    else:
        rel = release_at(tr, k, grade)
        n_carried = k          # ticks 0..k-1 carried; tick k releases with the state after k-1 integrations
        a = k - 1
    ticks = carried_ticks(t_off, sP, tr, n_carried, y_grab, x, extra_first_events)
    g = P.launch_gravity_mps2
    vx, vy = rel.vx, rel.vy
    chain = None
    if guide_to is not None:
        T, vx, vy = guide_chain(rel, guide_to)
        chain = dict(T=r6(T), vx=r6(vx), vy=r6(vy))
    launch = dict(vx=r6(vx), vy=r6(vy), y0=r6(rel.y0), vxRaw=r6(rel.vx_raw), vyRaw=r6(rel.vy_raw))
    info = dict(release_tick=k, grade=("Poor" if auto else grade), auto=auto, k_apex=tr.k_apex, anchor=a,
                v_c=tr.v_c, release=rel, chain=chain, trace=tr)
    # flight ticks: i = 0 only exists for the auto release (the release tick itself shows the state position)
    i = 0 if auto else 1
    first = True
    land_i = None
    catch_i = None
    while True:
        t = i / 60.0
        z = rel.z0 + vx * t
        y = rel.y0 + vy * t - 0.5 * g * t * t
        gt = t_off + a + i
        ev = []
        if first:
            ev.append(dict(type="VineReleased", grade=info["grade"], auto=auto))
        if guide_to is not None:
            c = chain_flight_check(rel, z, y, guide_to)
            if c:
                catch_i = i
                ticks.append(dict(t=gt, state="Falling", x=x, y=r6(y), z=r6(sP + z), launch=launch if first else None,
                                  events=ev + [dict(type="VineGrabbed", next=True)]))
                break
        if y <= 0.0:
            land_i = i
            ticks.append(dict(t=gt, state="Landed", x=x, y=0.0, z=r6(sP + z), launch=launch if first else None,
                              events=ev + [dict(type="Landed", fromVine=True)]))
            break
        ticks.append(dict(t=gt, state="Falling", x=x, y=r6(y), z=r6(sP + z), launch=launch if first else None,
                          events=ev))
        first = False
        i += 1
        if i > 600:
            raise RuntimeError("flight did not end")
    for tk in ticks:
        if tk.get("launch") is None:
            tk.pop("launch", None)
    info["land_i"] = land_i
    info["catch_i"] = catch_i
    info["launch"] = launch
    info["last"] = ticks[-1]
    return ticks, info


def chain_flight_check(rel, z, y, zb):
    return y > 0.0 and abs(z - zb) <= P.zone_half_m and y < P.grab_max_feet_y_m


def doc_base(name, desc, seed, vines, extra):
    d = dict(schema=SCHEMA, name=name, spec="docs/specs/004-fixed-pivot-swing.md", description=desc, seed=seed,
             generator="tools/sim/pendulum_golden.py (pendulum_model.py, float64)",
             config=config_block(), derived=derived_block())
    d["derived"].pop("chasmRimZ")
    d["derived"].pop("chasmFarEdgeZ")
    d["precision"] = "float64 reference; compare a float32 implementation within `tolerance`"
    d["vines"] = vines
    d.update(extra)
    d["tolerance"] = TOL
    return d


def single_swing(name, desc, seed, v_in, grab_dz, y_grab, swipe_tick, speed_mult=1.0, over_chasm=True):
    sP = VINE_Z
    x = LANE_X[1]
    z_g = sP + grab_dz
    ticks, info = swing_and_flight(0, sP, v_in, z_g, y_grab, x, swipe_tick,
                                   [dict(type="VineGrabbed", vIn=r6(v_in), catchSpeed=r6(info_vc(v_in)))])
    rel = info["release"]
    expect = dict(catchSpeedMps=r6(info["v_c"]), apexTick=info["k_apex"], releaseTick=info["release_tick"],
                  grade=info["grade"], peakThetaDeg=r6(math.degrees(max(info["trace"].theta))),
                  landingTickAfterGrab=info["anchor"] + info["land_i"], landingZ=r6(sP + rel.z_land),
                  landingPastFarEdgeM=r6(rel.z_land - P.far_edge_m),
                  chasm=dict(rimZ=sP - P.chasm_rim_before_pivot_m, farEdgeZ=sP + P.far_edge_m, overChasm=over_chasm))
    doc = doc_base(name, desc, seed,
                   [dict(id=1, z=sP, lane=1, row=0, over_chasm=over_chasm)],
                   dict(grab=dict(tick=0, vIn=r6(v_in), z=r6(z_g), y=r6(y_grab), x=x, vineId=1),
                        speed=dict(speedMultiplier=speed_mult),
                        inputs=([] if swipe_tick is None else [dict(tick=swipe_tick, cmd="Jump", note="release swipe (swing tick)")]),
                        expect=expect))
    doc["ticks"] = ticks
    return doc


def info_vc(v_in):
    return pm.catch_speed(v_in)


def chain_two():
    sA, sB = VINE_Z, VINE_Z + P.chain_spacing_m
    xa, xb = LANE_X[0], LANE_X[2]
    ticksA, ia = swing_and_flight(0, sA, 21.0, sA - 1.1, 1.2, xa, 47,
                                  [dict(type="VineGrabbed", vIn=21.0, catchSpeed=16.0)], guide_to=P.chain_spacing_m)
    last = ia["last"]
    t_b = last["t"]
    # tick t_b is the grab tick of vine B (n = 0): replace the flight record with the carried record
    z_b, y_b, vx_b = last["z"], last["y"], ia["chain"]["vx"]
    ticksA = ticksA[:-1]
    ticksB, ib = swing_and_flight(t_b, sB, vx_b, z_b, y_b, xb, 40,
                                  [dict(type="VineGrabbed", vIn=r6(vx_b), catchSpeed=r6(pm.catch_speed(vx_b)),
                                        chain=True)])
    ticks = ticksA + ticksB
    expect = dict(catchSpeedA=r6(ia["v_c"]), releaseTickA=ia["release_tick"], gradeA=ia["grade"], guidedFlight=ia["chain"],
                  grabTickB=t_b, grabZB=r6(z_b), grabYB=r6(y_b), arrivalVxB=r6(vx_b), catchSpeedB=r6(ib["v_c"]),
                  releaseTickB=ib["release_tick"], gradeB=ib["grade"], landingTick=ticks[-1]["t"],
                  landingZ=ticks[-1]["z"])
    doc = doc_base("10_vine_chain_two",
                   "Chain of two vines 18 m apart (zigzag lanes 0 and 2). Entry 21 m/s clamps to 16 m/s; Perfect release at "
                   "swing tick 47 is guided to vine B (T clamped to [0.85, 1.5] s, arrival feet 1.5 m, gravity 16); B is "
                   "caught automatically on the first tick the zone test passes; second swing starts at the arrival vx "
                   "(clamped to 13..16); Good release at swing tick 40; free landing on the pad.",
                   1010, [dict(id=1, z=sA, lane=0, row=0, over_chasm=False),
                          dict(id=2, z=sB, lane=2, row=1, over_chasm=False)],
                   dict(grab=dict(tick=0, vIn=21.0, z=r6(sA - 1.1), y=1.2, x=xa, vineId=1),
                        speed=dict(speedMultiplier=1.0),
                        inputs=[dict(tick=47, cmd="Jump", note="release swipe A (swing tick of A)"),
                                dict(tick=t_b + 40, cmd="Jump", note="release swipe B (global tick; swing tick 40 of B)")],
                        expect=expect))
    doc["ticks"] = ticks
    return doc


def missed_over_chasm():
    """Spec 001 movement (runner_model) over the 16 m chasm; the jump is too late to be in the zone: no grab, fall."""
    from runner_model import Runner, RunnerConfig, TestTrackQuery, JUMP, NONE, names_from_commands
    speed = 10.0
    sP = VINE_Z
    rim, far = sP - P.chasm_rim_before_pivot_m, sP + P.far_edge_m
    cfg = RunnerConfig(fixed_speed_mps=speed, use_start_ramp=False)
    r = Runner(cfg, track=TestTrackQuery([(rim, far, -50.0, 50.0)]), record_events=True)
    r.z = rim - 6.0
    jump_tick = 80   # too late: the window for 10 m/s is jump ticks 163..186 counted from rim - 30 m; here from rim - 6 m: see expect
    w = pm.takeoff_window(speed)
    # express in this trace's own tick frame (start z = rim - 6): shift by the 24 m (= 144 ticks) difference of the start line
    shift = int(round((30.0 - 6.0) / speed * 60))
    late_tick = (w["hi"] + 12) - shift
    ticks = []
    ev_i = len(r.events)
    for _ in range(late_tick + 90):
        t = r.tick
        cmd = JUMP if t == late_tick else NONE
        r.step(cmd)
        evs = []
        for (et, etype, payload) in r.events[ev_i:]:
            evs.append(dict(type=etype, **dict(("from" if k == "frm" else k, v) for k, v in payload.items())))
        ev_i = len(r.events)
        ticks.append(dict(t=t, cmd=names_from_commands(cmd), x=r6(r.x), y=r6(r.y), z=r6(r.z), state=r.state, events=evs))
        if r.dead:
            break
    expect = dict(jumpTick=late_tick, grabbed=False, died=r.dead, deathCause="Fell (sim classifies MissedVine: vine row within [-40, +16] m, not grabbed)",
                  deathTick=r.death["tick"] if r.death else None, grabWindowJumpTicks=[w["lo"] - shift, w["hi"] - shift])
    doc = doc_base("11_vine_missed_over_chasm",
                   "10 m/s on spec 001 movement over the 16 m chasm (rim at pivot-4, far edge pivot+12), vine at z=100. Jump on tick "
                   "%d is later than the grab window, the hero leaves the zone airborne-low/falls: no grab, the hero falls into the "
                   "chasm (Died Fell; the vine layer relabels it MissedVine)." % late_tick,
                   1011, [dict(id=1, z=sP, lane=1, row=0, over_chasm=True)],
                   dict(grab=None, speed=dict(fixedSpeedMps=speed, useStartRamp=False, speedMultiplier=1.0), startZ=r6(rim - 6.0),
                        track=dict(kind="gaps", gaps=[dict(z0=rim, z1=far, x0=-50.0, x1=50.0)]),
                        inputs=[dict(tick=late_tick, cmd="Jump")], expect=expect))
    doc["ticks"] = ticks
    return doc


def scenarios():
    return [
        lambda: single_swing("07_vine_perfect_release",
                             "Entry 15 m/s (catch 15), grab at z = pivot - 1.2 with feet y 1.2, Perfect release swipe at swing tick 47 "
                             "(state after 46 integrations), free flight, landing on the pad (7 m past the far edge).",
                             1007, 15.0, -1.2, 1.2, 47),
        lambda: single_swing("08_vine_poor_auto_release",
                             "Entry 14 m/s (catch 14), no swipe: Poor auto release on the first tick with omega <= 0 "
                             "(swing tick 85) with the floors vx 6 and vy 2; lands past the far edge.",
                             1008, 14.0, -1.2, 1.0, None),
        lambda: single_swing("09a_vine_catch_clamp_entry_8",
                             "Entry 8 m/s is pulled up to the catch minimum 13 m/s (omega0 = 13/14); Good release at swing tick 27 "
                             "(earliest Good).", 1091, 8.0, -1.2, 0.8, 27),
        lambda: single_swing("09b_vine_catch_clamp_entry_21",
                             "Entry 21 m/s is slowed to the catch maximum 16 m/s; Good release at swing tick 60 (late).",
                             1092, 21.0, -1.1, 1.3, 60),
        chain_two,
        missed_over_chasm,
        lambda: single_swing("12_vine_boost_entry",
                             "Speed Boost entry: 16.25 m/s x 1.6 = 26 m/s arrives at the grab; the catch clamp makes it 16 m/s and the "
                             "multiplier plays no further role (identical swing to entry 21 at the same grab). Perfect release at tick 45.",
                             1012, 26.0, -1.1, 1.0, 45, speed_mult=1.6),
    ]


def build_all():
    return [(f()["name"], f()) for f in scenarios()]


def main(check=False):
    os.makedirs(GOLDEN_DIR, exist_ok=True)
    bad = 0
    for name, doc in build_all():
        path = os.path.join(GOLDEN_DIR, name + ".json")
        text = json.dumps(doc, indent=1, sort_keys=False) + "\n"
        if check:
            try:
                with open(path) as f:
                    if f.read() != text:
                        print("MISMATCH", path)
                        bad += 1
            except IOError:
                print("MISSING", path)
                bad += 1
        else:
            with open(path, "w") as f:
                f.write(text)
            print("wrote", path)
    return bad


if __name__ == "__main__":
    sys.exit(1 if main(check="--check" in sys.argv) else 0)
