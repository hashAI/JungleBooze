"""Generate golden traces (tools/sim/golden/*.json) from runner_model.py.

    python3 -I golden.py            # (re)write all traces
    python3 -I golden.py --check    # verify the files on disk still match the model

Schema: see README.md ("Golden trace format"). C# EditMode tests replay `ticks[].cmd_bits`
through the runner simulation and compare lane / x / y / state per tick.
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from runner_model import (  # noqa: E402
    RunnerConfig, Runner, FlatTrackQuery, TestTrackQuery, names_from_commands,
    MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE, PAUSE_RESUMED, NONE,
)

GOLDEN_DIR = os.path.join(HERE, "golden")
SCHEMA = "junglebooze.golden-trace.v1"

AUTHORING_FIELDS = (
    "lane_count", "lane_width_m", "start_lane", "lane_switch_ms", "lane_switch_ease_exponent",
    "lane_queue_start_fraction", "edge_forgiveness_fraction", "jump_apex_height_m", "jump_airtime_ms",
    "fast_fall_min_speed_mps", "fast_fall_max_ms", "slide_ms", "input_buffer_ms", "coyote_ms",
    "player_hitbox_width_m", "player_hitbox_depth_m", "standing_height_m", "sliding_height_m",
    "fall_death_depth_m", "stumble_bounce_ms", "stumble_daze_ms",
)
DERIVED_FIELDS = (
    "lane_switch_ticks", "lane_queue_start_tick", "jump_airtime_ticks", "fast_fall_max_ticks",
    "slide_ticks", "input_buffer_ticks", "coyote_ticks", "stumble_bounce_ticks", "stumble_daze_ticks",
    "gravity_mps2", "jump_velocity_mps",
)


def _camel(s):
    parts = s.split("_")
    return parts[0] + "".join(p.title() for p in parts[1:])


# name, description, seed, start_lane, gaps, script {tick: cmd}, n_ticks, pauses [(before_tick, note)]
def scenarios():
    lead = 6  # ground ends under the footprint at z = 5.25 for the coyote trace
    return [
        dict(name="01_lane_switch_and_reverse",
             description="MoveRight 1->2 (7 ticks, first X change on the command tick); MoveLeft 2->1 on "
                         "tick 10; MoveRight on tick 12 reverses back to lane 2 from the current X (L5).",
             seed=1001, start_lane=1, gaps=[], script={0: MOVE_RIGHT, 10: MOVE_LEFT, 12: MOVE_RIGHT},
             ticks=24, pauses=[]),
        dict(name="02_double_lane_queue_bump_cancel",
             description="From lane 0: MoveRight on ticks 0 and 1 (second is queued, starts on tick 4, lane-2 "
                         "center on tick 10 = 11 ticks); third MoveRight on tick 2 is Bumped (L7). Then MoveLeft "
                         "on 20 and 21 (queued) and MoveRight on 22 cancels the queued move only (L4): ends in lane 1.",
             seed=1002, start_lane=0, gaps=[], script={0: MOVE_RIGHT, 1: MOVE_RIGHT, 2: MOVE_RIGHT,
                                                       20: MOVE_LEFT, 21: MOVE_LEFT, 22: MOVE_RIGHT},
             ticks=34, pauses=[]),
        dict(name="03_jump_buffered_and_expired",
             description="Jump on tick 0: apex 1.5 m on tick 18, lands tick 36. Jump on tick 27 (9 ticks before "
                         "landing) is buffered and fires on tick 36. Jump on tick 62 (10 ticks before the second "
                         "landing on tick 72) expires on tick 72 (I4) and HERO stays on the ground.",
             seed=1003, start_lane=1, gaps=[], script={0: JUMP, 27: JUMP, 62: JUMP}, ticks=80, pauses=[]),
        dict(name="04_coyote_jump",
             description="Ground ends at z = 5.0 (full-width gap 5.0..9.0 m, 10 m/s). Footprint leaves the ground on "
                         "the leave tick e = 31 (state Coyote). Jump on e + 5 = 36 is a full ground jump "
                         "(coyote = true) and lands on the far side on tick 72.",
             seed=1004, start_lane=1, gaps=[(5.0, 9.0, -50.0, 50.0)], script={36: JUMP}, ticks=80, pauses=[]),
        dict(name="05_fastfall_into_slide",
             description="Jump on tick 0; Slide on tick 12 starts a fast-fall from Y(11) (v = 15 m/s) that lands "
                         "within 6 ticks and starts a 39-tick slide on the landing tick. MoveLeft during the slide "
                         "keeps the slide running; Slide on tick 40 restarts the timer to 39.",
             seed=1005, start_lane=1, gaps=[], script={0: JUMP, 12: SLIDE, 20: MOVE_LEFT, 40: SLIDE},
             ticks=86, pauses=[]),
        dict(name="06_pause_resume",
             description="From lane 0: Jump on tick 0; MoveRight on 25 starts a move to lane 1, MoveRight on 26 is "
                         "queued (would start on tick 29); Jump on 28 is buffered. The game pauses after tick 28. The "
                         "first tick after resume (29) carries PauseResumed: the buffered jump (Invalidated) and the "
                         "queued move are cleared; the jump arc and the active lane move continue unchanged. HERO "
                         "lands on tick 36 without jumping, in lane 1.",
             seed=1006, start_lane=0, gaps=[], script={0: JUMP, 25: MOVE_RIGHT, 26: MOVE_RIGHT, 28: JUMP,
                                                       29: PAUSE_RESUMED},
             ticks=44, pauses=[dict(before_tick=29, note="pause after tick 28; countdown; resume")]),
    ]


def build(sc):
    cfg = RunnerConfig(fixed_speed_mps=10.0, use_start_ramp=False, start_lane=sc["start_lane"])
    track = TestTrackQuery(sc["gaps"]) if sc["gaps"] else FlatTrackQuery()
    r = Runner(cfg, track=track, record_events=True)
    pause_ticks = set(p["before_tick"] for p in sc["pauses"])
    ticks = []
    ev_i = len(r.events)
    for _ in range(sc["ticks"]):
        t = r.tick
        if t in pause_ticks:
            r.pause()
            r.resume_ready()
        cmd = sc["script"].get(t, NONE)
        r.step(cmd)
        evs = []
        for (et, etype, payload) in r.events[ev_i:]:
            evs.append(dict(type=etype, **dict(("from" if k == "frm" else k, v) for k, v in payload.items())))
        ev_i = len(r.events)
        ticks.append(dict(
            t=t, cmd=names_from_commands(cmd), cmd_bits=cmd,
            lane=r.target_lane, occupied_lane=r.occupied_lane(),
            x=round(r.x, 6), y=round(r.y, 6), z=round(r.z, 6), state=r.state,
            hitbox_h=r.hitbox_height(), events=evs))
    r.finalize()
    doc = dict(
        schema=SCHEMA, name=sc["name"], spec="docs/specs/001-player-movement.md",
        description=sc["description"], seed=sc["seed"],
        generator="tools/sim/golden.py (runner_model.py)",
        config=dict((_camel(k), getattr(cfg, k)) for k in AUTHORING_FIELDS),
        derived=dict((_camel(k), round(getattr(cfg, k), 6)) for k in DERIVED_FIELDS),
        speed=dict(fixedSpeedMps=10.0, useStartRamp=False, speedMultiplier=1.0),
        track=dict(kind="gaps" if sc["gaps"] else "flat",
                   gaps=[dict(z0=g[0], z1=g[1], x0=g[2], x1=g[3]) for g in sc["gaps"]]),
        obstacles=[],
        pauses=sc["pauses"],
        tolerance=dict(x=1e-4, y=1e-4, z=1e-4),
        outcomes=dict((k, v) for k, v in r.outcomes.items() if v),
        ticks=ticks,
    )
    return doc


def main(check=False):
    os.makedirs(GOLDEN_DIR, exist_ok=True)
    bad = 0
    for sc in scenarios():
        doc = build(sc)
        path = os.path.join(GOLDEN_DIR, sc["name"] + ".json")
        text = json.dumps(doc, indent=1, sort_keys=False) + "\n"
        if check:
            with open(path) as f:
                if f.read() != text:
                    print("MISMATCH", path)
                    bad += 1
        else:
            with open(path, "w") as f:
                f.write(text)
            print("wrote", path)
    return bad


if __name__ == "__main__":
    sys.exit(1 if main(check="--check" in sys.argv) else 0)
