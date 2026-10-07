"""Unit tests pinning runner_model.py to spec 001 tick numbers.

Run from tools/sim:   python3 -I -m unittest -v
Run from repo root:   python3 -I -m unittest discover -s tools/sim -t tools/sim -v
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from runner_model import (  # noqa: E402
    RunnerConfig, Runner, TestTrackQuery, make_obstacle, speed_curve, ms_to_ticks,
    MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE, PAUSE_RESUMED, NONE,
    RUNNING, SLIDING, AIRBORNE, FAST_FALLING, COYOTE, FALLING, DEAD,
    LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK, OUTCOMES,
    EXECUTED, QUEUED, BUMPED, CANCELLED, SUPERSEDED, EXPIRED, IGNORED, ALREADY_ACTIVE,
    INVALIDATED,
)

LANE = 2.4


def cfg(**kw):
    kw.setdefault("use_start_ramp", False)
    kw.setdefault("fixed_speed_mps", 10.0)
    return RunnerConfig(**kw)


def run(r, script, ticks):
    """script: dict tick -> commands. Returns list of snapshots after each tick."""
    out = []
    for _ in range(ticks):
        t = r.tick
        r.step(script.get(t, NONE))
        out.append(dict(tick=t, x=r.x, y=r.y, z=r.z, state=r.state, lane=r.target_lane))
    return out


def events_of(r, etype):
    return [(t, p) for (t, e, p) in r.events if e == etype]


class ConfigTests(unittest.TestCase):
    def test_ticks_and_derived_values_ac61(self):
        c = RunnerConfig()
        self.assertEqual(
            (c.lane_switch_ticks, c.lane_queue_start_tick, c.jump_airtime_ticks, c.fast_fall_max_ticks,
             c.slide_ticks, c.input_buffer_ticks, c.coyote_ticks, c.run_start_ramp_ticks,
             c.stumble_bounce_ticks, c.stumble_daze_ticks),
            (7, 4, 36, 6, 39, 9, 5, 30, 9, 180))
        self.assertAlmostEqual(c.gravity_mps2, 33.333, places=3)
        self.assertAlmostEqual(c.jump_velocity_mps, 10.0, places=9)
        self.assertAlmostEqual(c.edge_forgiveness_m, 1.44, places=9)

    def test_ms_to_ticks_rounding(self):
        self.assertEqual(ms_to_ticks(120), 7)
        self.assertEqual(ms_to_ticks(80), 5)
        self.assertEqual(ms_to_ticks(0), 1)  # max(1, ...) even though coyoteMs range allows 0


class SpeedTests(unittest.TestCase):
    def test_curve_ac01(self):
        rows = RunnerConfig().speed_rows
        self.assertAlmostEqual(speed_curve(rows, 0), 10.0)
        self.assertAlmostEqual(speed_curve(rows, 250), 11.0)
        self.assertAlmostEqual(speed_curve(rows, 7000), 21.0)
        self.assertAlmostEqual(speed_curve(rows, 20000), 21.0)
        r = Runner(RunnerConfig(tutorial=True, use_start_ramp=False))
        r.step()
        self.assertAlmostEqual(r.speed, 8.0)

    def test_start_ramp_ac02(self):
        r = Runner(RunnerConfig())
        speeds, zs = [], []
        for _ in range(40):
            zs.append(r.z)
            r.step()
            speeds.append(r.speed)
        self.assertAlmostEqual(speeds[0], 5.0)
        # AC-02 says 7.5; the curve has already risen to 10.0044 m/s at z = 1.1 m, so 7.503
        self.assertAlmostEqual(speeds[15], 7.5, delta=0.01)
        for t in range(30, 40):
            self.assertAlmostEqual(speeds[t], speed_curve(r.cfg.speed_rows, zs[t]), places=12)

    def test_distance_ac03(self):
        r = Runner(cfg())
        run(r, {}, 600)
        self.assertAlmostEqual(r.z, 100.0, delta=0.001)

    def test_boost_and_airtime_ac05(self):
        r = Runner(cfg(fixed_speed_mps=21.0, speed_multiplier=1.6))
        z0 = r.z
        r.step(JUMP)
        self.assertAlmostEqual(r.z - z0, 0.56, places=6)
        run(r, {}, 36)
        self.assertEqual(r.state, RUNNING)
        self.assertEqual(events_of(r, "Landed")[0][0], 36)

    def test_speed_step_events_ac04(self):
        r = Runner(RunnerConfig(use_start_ramp=False))
        while r.z < 600:
            r.step()
        self.assertEqual([p["row"] for _, p in events_of(r, "SpeedStepReached")], [1])


class LaneTests(unittest.TestCase):
    def test_single_switch_7_ticks_ac06_ac07(self):
        r = Runner(cfg())
        s = run(r, {0: MOVE_RIGHT}, 10)
        xs = [p["x"] for p in s]
        self.assertGreater(xs[0], 0.0)                     # moves on the command tick
        self.assertAlmostEqual(xs[0] / LANE, 0.265, delta=0.001)
        self.assertAlmostEqual(xs[1] / LANE, 0.490, delta=0.001)
        self.assertAlmostEqual(xs[2] / LANE, 0.673, delta=0.001)
        self.assertLess(xs[5], LANE)
        self.assertEqual(xs[6], LANE)                      # exactly on the 7th tick (tick 6)
        self.assertTrue(all(x <= LANE for x in xs))        # never overshoots
        self.assertEqual(r.target_lane, 2)

    def test_double_swipe_11_ticks_ac08(self):
        c = cfg(start_lane=0)
        r = Runner(c)
        s = run(r, {0: MOVE_RIGHT, 1: MOVE_RIGHT}, 14)
        xs = [p["x"] for p in s]
        self.assertEqual(r.outcomes[QUEUED], 1)
        # the second move's first X change is on tick 4, before lane-1 center
        self.assertLess(xs[3], 0.0)
        self.assertGreater(xs[4], xs[3])
        started = events_of(r, "LaneChangeStarted")
        self.assertEqual([t for t, _ in started], [0, 4])
        self.assertLess(xs[9], LANE)
        self.assertEqual(xs[10], LANE)                     # lane-2 center on tick 10 (11 ticks)

    def test_late_second_swipe_starts_at_once_ac09(self):
        r = Runner(cfg(start_lane=0))
        run(r, {0: MOVE_RIGHT, 5: MOVE_RIGHT}, 15)
        self.assertEqual([t for t, _ in events_of(r, "LaneChangeStarted")], [0, 5])
        self.assertEqual(r.target_lane, 2)
        self.assertEqual(r.x, LANE)

    def test_reverse_ac10(self):
        r = Runner(cfg())
        s = run(r, {0: MOVE_RIGHT, 2: MOVE_LEFT}, 12)
        st = events_of(r, "LaneChangeStarted")
        self.assertTrue(st[1][1]["reversal"])
        self.assertEqual(st[1][0], 2)
        self.assertNotEqual(s[7]["x"], 0.0)
        self.assertEqual(s[8]["x"], 0.0)                   # 7 ticks: ticks 2..8
        self.assertEqual(r.target_lane, 1)

    def test_opposite_cancels_queue_ac11(self):
        r = Runner(cfg(start_lane=0))
        run(r, {0: MOVE_RIGHT, 1: MOVE_RIGHT, 2: MOVE_LEFT}, 15)
        self.assertEqual(len(events_of(r, "LaneChangeCancelled")), 1)
        self.assertEqual(r.target_lane, 1)
        self.assertEqual(r.x, 0.0)

    def test_bump_ac12(self):
        r = Runner(cfg(start_lane=0))
        s = run(r, {0: MOVE_LEFT}, 20)
        self.assertTrue(all(p["x"] == -LANE for p in s))
        self.assertEqual(len(events_of(r, "LaneBlocked")), 1)
        self.assertEqual(r.outcomes[BUMPED], 1)
        r = Runner(cfg(start_lane=2))
        run(r, {0: MOVE_RIGHT}, 20)
        self.assertEqual(r.outcomes[BUMPED], 1)
        self.assertEqual(r.x, LANE)

    def test_third_same_direction_bumped_ac13(self):
        r = Runner(cfg(start_lane=0))
        run(r, {0: MOVE_RIGHT, 1: MOVE_RIGHT, 2: MOVE_RIGHT}, 15)
        self.assertEqual(r.outcomes[BUMPED], 1)
        self.assertEqual(r.target_lane, 2)

    def test_lane_move_does_not_touch_vertical_ac14(self):
        a = Runner(cfg())
        sa = run(a, {0: JUMP}, 40)
        b = Runner(cfg())
        sb = run(b, {0: JUMP, 5: MOVE_RIGHT, 20: MOVE_LEFT}, 40)
        self.assertEqual([p["y"] for p in sa], [p["y"] for p in sb])
        a = Runner(cfg())
        sa = run(a, {0: SLIDE}, 45)
        b = Runner(cfg())
        sb = run(b, {0: SLIDE, 3: MOVE_LEFT}, 45)
        self.assertEqual([p["state"] for p in sa], [p["state"] for p in sb])
        a = Runner(cfg())
        sa = run(a, {0: JUMP, 18: SLIDE}, 30)
        b = Runner(cfg())
        sb = run(b, {0: JUMP, 18: SLIDE, 19: MOVE_RIGHT}, 30)
        self.assertEqual([p["y"] for p in sa], [p["y"] for p in sb])


class JumpTests(unittest.TestCase):
    def test_arc_ac16(self):
        r = Runner(cfg())
        s = run(r, {3: JUMP}, 45)
        ys = [p["y"] for p in s]
        self.assertEqual(ys[3], 0.0)                       # n = 0 on the start tick
        self.assertAlmostEqual(ys[3 + 18], 1.5, delta=0.001)
        self.assertAlmostEqual(max(ys), 1.5, delta=0.001)
        self.assertEqual(ys.index(max(ys)), 21)
        self.assertGreater(ys[3 + 35], 0.0)
        self.assertEqual(ys[3 + 36], 0.0)
        self.assertEqual(events_of(r, "Landed")[0][0], 39)
        self.assertEqual(events_of(r, "Landed")[0][1]["airTicks"], 36)
        self.assertEqual(events_of(r, "JumpApex")[0][0], 21)

    def test_same_at_all_speeds_ac17(self):
        ref = None
        for sp in (8.0, 10.0, 21.0, 33.6):
            r = Runner(cfg(fixed_speed_mps=sp))
            ys = [p["y"] for p in run(r, {0: JUMP}, 40)]
            if ref is None:
                ref = ys
            self.assertEqual(ys, ref)

    def test_no_double_jump_ac18(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 10: JUMP}, 30)
        self.assertEqual(len(events_of(r, "JumpStarted")), 1)
        self.assertEqual(r.buffered_entered, 1)


class FastFallTests(unittest.TestCase):
    def test_from_apex_6_ticks_ac19(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 19: SLIDE}, 40)   # Y at end of tick 18 = 1.5 (apex)
        land = events_of(r, "Landed")[0][0]
        self.assertEqual(land - 19 + 1, 6)
        self.assertTrue(events_of(r, "Landed")[0][1]["wasFastFall"])

    def test_from_forced_heights_ac19(self):
        for h, limit in ((4.0, 6), (0.3, 2), (1.5, 6)):
            r = Runner(cfg())
            r.step(JUMP)
            r.y = h                     # force a height
            r.step(SLIDE)
            n = 1
            while r.state == FAST_FALLING:
                r.step()
                n += 1
            self.assertLessEqual(n, limit, "height %s" % h)

    def test_slide_on_landing_ac20(self):
        r = Runner(cfg())
        s = run(r, {0: JUMP, 10: SLIDE}, 70)
        land = events_of(r, "Landed")[0][0]
        self.assertEqual(events_of(r, "SlideStarted")[0][0], land)
        sliding = [p["tick"] for p in s if p["state"] == SLIDING]
        self.assertEqual(len(sliding), 39)
        self.assertEqual(sliding[0], land)

    def test_jump_during_fast_fall_wins_ac21(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 18: SLIDE, 19: JUMP}, 30)
        js = events_of(r, "JumpStarted")
        self.assertEqual(len(js), 2)
        self.assertTrue(js[1][1]["buffered"])
        self.assertEqual(events_of(r, "SlideStarted"), [])

    def test_slide_while_fast_falling_already_active(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 18: SLIDE, 19: SLIDE}, 30)
        self.assertEqual(r.outcomes[ALREADY_ACTIVE], 1)


class SlideTests(unittest.TestCase):
    def test_39_ticks_ac22(self):
        r = Runner(cfg())
        hs = []
        for t in range(50):
            r.step(SLIDE if t == 2 else NONE)
            hs.append((r.state, r.hitbox_height()))
        sliding = [i for i, (s, _) in enumerate(hs) if s == SLIDING]
        self.assertEqual(sliding, list(range(2, 41)))
        self.assertEqual(hs[2][1], 0.8)
        self.assertEqual(hs[41], (RUNNING, 1.8))

    def test_restart_ac23(self):
        r = Runner(cfg())
        s = run(r, {0: SLIDE, 20: SLIDE}, 70)
        sliding = [p["tick"] for p in s if p["state"] == SLIDING]
        self.assertEqual(sliding[-1], 20 + 38)
        self.assertEqual(s[59]["state"], RUNNING)

    def test_jump_cancels_slide_ac24(self):
        r = Runner(cfg())
        run(r, {0: SLIDE, 10: JUMP}, 11)
        self.assertEqual(r.state, AIRBORNE)
        self.assertEqual(r.hitbox_height(), 1.8)
        self.assertEqual(events_of(r, "SlideEnded")[0][1]["reason"], "Jump")
        self.assertTrue(events_of(r, "JumpStarted")[0][1]["fromSlide"])

    def test_extend_under_high_barrier_ac25(self):
        c = cfg()
        # a long high barrier so the slide timer runs out underneath it
        ob = make_obstacle(c, 1, HIGH_BARRIER, 1, 2.0, depth=10.0)
        r = Runner(c, obstacles=[ob])
        s = run(r, {0: SLIDE}, 120)
        self.assertFalse(r.dead)
        stand = [p["tick"] for p in s if p["state"] == RUNNING][0]
        # z at stand-up: standing box clear of the barrier back face (z1 = 12.0)
        self.assertGreaterEqual(s[stand]["z"] - 0.25, 12.0 - 1e-9)
        self.assertGreater(stand, 39)


class BufferTests(unittest.TestCase):
    def test_9_ticks_fire_10_expire_ac26(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 36 - 9: JUMP}, 40)
        js = events_of(r, "JumpStarted")
        self.assertEqual([t for t, _ in js], [0, 36])
        self.assertTrue(js[1][1]["buffered"])
        r = Runner(cfg())
        run(r, {0: JUMP, 36 - 10: JUMP}, 40)
        self.assertEqual(len(events_of(r, "JumpStarted")), 1)
        self.assertEqual(r.outcomes[EXPIRED], 1)

    def test_newer_jump_replaces_ac27(self):
        r = Runner(cfg())
        run(r, {0: JUMP, 36 - 12: JUMP, 36 - 5: JUMP}, 40)
        self.assertEqual([t for t, _ in events_of(r, "JumpStarted")], [0, 36])
        self.assertEqual(r.outcomes[SUPERSEDED], 1)
        self.assertEqual(r.outcomes[EXPIRED], 0)

    def test_same_tick_conflicts_ac28(self):
        r = Runner(cfg())
        s = run(r, {0: MOVE_LEFT | MOVE_RIGHT}, 5)
        self.assertTrue(all(p["x"] == 0.0 for p in s))
        self.assertEqual(r.outcomes[CANCELLED], 2)
        r = Runner(cfg())
        run(r, {0: JUMP | SLIDE}, 2)
        self.assertEqual(r.state, AIRBORNE)
        self.assertEqual(r.outcomes[SUPERSEDED], 1)

    def test_accounting_random_stream_ac29(self):
        import random as _r  # test-only, seeded; the model never uses randomness
        rng = _r.Random(12345)
        c = cfg()
        gaps = [(30.0 + 40 * i, 33.0 + 40 * i, -10, 10) for i in range(50)]
        obs = [make_obstacle(c, i, (LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK)[i % 3], i % 3, 15.0 + 20 * i)
               for i in range(80)]
        r = Runner(c, track=TestTrackQuery(gaps), obstacles=obs, record_events=False)
        for _ in range(10000):
            cmd = 0
            for f in (MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE, PAUSE_RESUMED):
                if rng.random() < 0.06:
                    cmd |= f
            r.step(cmd)
        r.finalize()
        self.assertEqual(sum(r.outcomes.values()), r.flags_received)
        self.assertGreater(r.flags_received, 1000)


class CoyoteTests(unittest.TestCase):
    def ledge(self, c):
        # ground ends: footprint (z +- 0.25) fully over the gap from z = 5.25
        return TestTrackQuery([(5.0, 1000.0, -10, 10)])

    def leave_tick(self, c):
        r = Runner(c, track=self.ledge(c))
        while r.state == RUNNING:
            r.step()
        return r.tick - 1

    def test_coyote_5_ticks_ac30(self):
        c = cfg()
        e = self.leave_tick(c)
        r = Runner(c, track=TestTrackQuery([(5.0, 1000.0, -10, 10)]))
        s = run(r, {e + 5: JUMP}, e + 50)
        js = events_of(r, "JumpStarted")
        self.assertEqual(js[0][0], e + 5)
        self.assertTrue(js[0][1]["coyote"])
        ys = [p["y"] for p in s]
        self.assertAlmostEqual(ys[e + 5 + 18], 1.5, delta=0.001)
        r = Runner(c, track=TestTrackQuery([(5.0, 1000.0, -10, 10)]))
        run(r, {e + 6: JUMP}, e + 30)
        self.assertEqual(events_of(r, "JumpStarted"), [])
        self.assertEqual(r.buffered_entered, 1)
        self.assertTrue(r.dead)

    def test_fell_and_ignored_ac31(self):
        c = cfg()
        e = self.leave_tick(c)
        r = Runner(c, track=self.ledge(c))
        script = dict((t, MOVE_LEFT | 0) for t in range(e + 7, e + 40))
        s = run(r, script, e + 40)
        self.assertTrue(r.dead)
        self.assertEqual(r.death["cause"], "Fell")
        self.assertLessEqual(r.death["y"], -1.0)
        # falling starts at e + 5 from rest; about 15 ticks to reach -1.0 m
        self.assertEqual(r.death["tick"] - (e + 5), 15)
        self.assertEqual(r.outcomes[EXECUTED], 0)
        self.assertEqual(r.outcomes[IGNORED], r.flags_received)

    def test_no_coyote_after_jump_ac32(self):
        c = cfg()
        r = Runner(c, track=TestTrackQuery([(5.5, 1000.0, -10, 10)]))
        run(r, {0: JUMP, 30: JUMP}, 60)
        self.assertEqual(len(events_of(r, "JumpStarted")), 1)
        self.assertTrue(r.dead)
        self.assertEqual(r.death["cause"], "Fell")

    def test_gap_jump_ac33(self):
        c = cfg()
        for before, alive in ((1.0, True), (5.5, False)):
            edge = 20.0
            r = Runner(c, track=TestTrackQuery([(edge, edge + 4.0, -10, 10)]))
            # step until z (after the jump tick's move) = edge - before
            start = int(round((edge - before) * 6)) - 1
            run(r, {start: JUMP}, start + 70)
            self.assertEqual(not r.dead, alive, "before=%s" % before)

    def test_ground_returns_by_lane_move(self):
        c = cfg()
        track = TestTrackQuery([(5.0, 1000.0, -1.2, 1.2)])  # gap in middle lane only
        r = Runner(c, track=track)
        while r.state == RUNNING:
            r.step()
        self.assertEqual(r.state, COYOTE)
        r.step(MOVE_RIGHT)
        run(r, {}, 3)
        self.assertEqual(r.state, RUNNING)
        self.assertFalse(r.dead)


class CollisionTests(unittest.TestCase):
    def test_front_full_block_ac34(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 7, FULL_BLOCK, 1, 5.0)])
        run(r, {}, 60)
        self.assertTrue(r.dead)
        self.assertEqual((r.death["cause"], r.death["archetype"], r.death["obstacle_id"], r.death["entry"]),
                         ("Hit", FULL_BLOCK, 7, "front"))
        # first overlapping tick: front of hero (z + 0.25) passes 5.0
        self.assertEqual(r.death["tick"], 28)

    def test_jump_low_barrier_ac35(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 1, LOW_BARRIER, 1, 5.0)])
        run(r, {12: JUMP}, 80)
        self.assertFalse(r.dead)
        self.assertEqual(r.stumbles, [])

    def test_high_barrier_ac36(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 1, HIGH_BARRIER, 1, 5.0)])
        run(r, {}, 60)
        self.assertTrue(r.dead)
        r = Runner(c, obstacles=[make_obstacle(c, 1, HIGH_BARRIER, 1, 5.0)])
        run(r, {20: SLIDE}, 80)
        self.assertFalse(r.dead)
        self.assertEqual(r.stumbles, [])

    def test_jump_into_high_barrier_from_below_ac37(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 1, HIGH_BARRIER, 1, 5.0, depth=4.0)])
        run(r, {0: SLIDE, 33: JUMP}, 80)
        self.assertTrue(r.dead)
        self.assertIn(r.death["entry"], ("below", "inside"))

    def test_side_stumble_and_bounce_ac38(self):
        c = cfg()
        ob = make_obstacle(c, 3, FULL_BLOCK, 2, 2.0, depth=6.0)
        r = Runner(c, obstacles=[ob])
        s = run(r, {}, 20)  # front of the block passed beside HERO
        st = r.tick
        s = run(r, {st: MOVE_RIGHT}, 40)
        self.assertFalse(r.dead)
        self.assertEqual(len(r.stumbles), 1)
        self.assertEqual(r.stumbles[0]["kind"], "side")
        stick = r.stumbles[0]["tick"]
        by_tick = dict((p["tick"], p) for p in s)
        self.assertEqual(by_tick[stick + 9]["x"], 0.0)
        self.assertNotEqual(by_tick[stick + 8]["x"], 0.0)
        r2 = Runner(c, obstacles=[make_obstacle(c, 3, FULL_BLOCK, 2, 2.0, depth=6.0)])
        run(r2, {}, 20)
        run(r2, {r2.tick: MOVE_RIGHT}, r2.stumbles and 0 or 1)
        while not r2.stumbles:
            r2.step()
        self.assertEqual(r2.daze_left, 180)

    def test_top_stumble_ac39(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 1, LOW_BARRIER, 1, 6.0, depth=0.6)])
        # jump early so HERO comes down on the barrier top
        run(r, {7: JUMP}, 80)
        self.assertFalse(r.dead)
        self.assertEqual([s["kind"] for s in r.stumbles], ["top"])
        self.assertEqual(r.state, RUNNING)

    def test_second_stumble_window_ac40(self):
        # build the two contact ticks directly: obstacle 2 placed so top contact happens at first + k
        c = cfg()
        for k, lethal in ((179, True), (181, False)):
            r = Runner(c, obstacles=[make_obstacle(c, 1, LOW_BARRIER, 1, 6.0)])
            run(r, {7: JUMP}, 60)
            first = r.stumbles[0]["tick"]
            target = first + k
            # same relative jump: contact happened at 'first' for a jump at tick 2 with front at 6.0
            dz = (target - first) * (10.0 / 60.0)
            r = Runner(c, obstacles=[make_obstacle(c, 1, LOW_BARRIER, 1, 6.0),
                                     make_obstacle(c, 2, LOW_BARRIER, 1, 6.0 + dz)])
            run(r, {7: JUMP, 7 + k: JUMP}, 7 + k + 60)
            self.assertEqual(r.dead, lethal, "k=%d" % k)
            if lethal:
                self.assertTrue(r.death["after_stumble"])
                self.assertEqual(r.death["tick"], target)
            else:
                self.assertEqual(len(r.stumbles), 2)

    def test_edge_forgiveness_ac41(self):
        c = cfg()
        # 2.4 m wide test obstacle in lane 1; HERO moves 1 -> 2 and reaches the face mid-move
        def trial(cmd_tick):
            ob = make_obstacle(c, 1, FULL_BLOCK, 1, 5.0, width=2.4)
            r = Runner(c, obstacles=[ob])
            run(r, {cmd_tick: MOVE_RIGHT}, 60)
            return r
        # contact tick for a straight run is 28. Move 2 ticks before: X after 3 ticks = 1.615 >= 1.44
        r = trial(26)
        self.assertFalse(r.dead)
        # 1 tick before: X = 1.176 at contact (< 1.40): normal overlap rule -> front hit
        r = trial(27)
        self.assertTrue(r.dead)

    def test_edge_forgiveness_threshold_exact(self):
        c = cfg()
        ob = make_obstacle(c, 1, FULL_BLOCK, 1, 5.0, width=2.4)
        for xval, expect_hit in ((1.44, False), (1.40, True)):
            r = Runner(c, obstacles=[ob])
            r.step(MOVE_RIGHT)
            r.x = xval                          # force X; the move is still active (moving away)
            r.move.start_x = r.move.end_x = xval
            r.move.elapsed = 3
            r.z = 4.70
            r.step()
            # HERO is now overlapping in x range for 1.40 (hero half 0.35 + obstacle half 1.2 = 1.55)
            self.assertEqual(r.dead or bool(r.stumbles), expect_hit, "x=%s" % xval)

    def test_no_tunneling_ac42(self):
        c = cfg(fixed_speed_mps=40.0)
        r = Runner(c, obstacles=[make_obstacle(c, 1, FULL_BLOCK, 1, 5.03, depth=0.1)])
        run(r, {}, 30)
        self.assertTrue(r.dead)

    def test_invulnerable_ac43(self):
        c = cfg()
        r = Runner(c, obstacles=[make_obstacle(c, 1, FULL_BLOCK, 1, 5.0)],
                   track=TestTrackQuery([(20.0, 30.0, -10, 10)]))
        r.invulnerable_ticks = 10 ** 6
        run(r, {}, 200)
        self.assertTrue(r.dead)
        self.assertEqual(r.death["cause"], "Fell")

    def test_tie_is_stumble_ac44(self):
        c = cfg()
        # obstacle in lane 2; place it so x and z start overlapping in the same tick, at the same time
        r = Runner(c)
        r.step(MOVE_RIGHT)  # tick 0: x 0 -> 0.6367, z 0 -> 0.1667
        # x enters lane-2 box (x0 = 2.4-1.02 = 1.38) when hero right edge x+0.35 > 1.38 -> x > 1.03
        # tick 1: x 0.6367 -> 1.1755 ; t_x = (1.03-0.6367)/(1.1755-0.6367)
        tx = (1.03 - 0.6367346938775511) / (1.1755102040816325 - 0.6367346938775511)
        zf = 0.16666666666666666 + 0.25 + tx * (10.0 / 60.0)
        r = Runner(c, obstacles=[make_obstacle(c, 1, FULL_BLOCK, 2, zf, depth=3.0)])
        run(r, {0: MOVE_RIGHT}, 20)
        self.assertFalse(r.dead)
        self.assertEqual(r.stumbles[0]["entry"], "tie")
        self.assertEqual(r.stumbles[0]["kind"], "side")


class PauseTests(unittest.TestCase):
    def test_resume_clears_buffer_and_queue_only_ac45(self):
        c = cfg(start_lane=0)
        r = Runner(c)
        run(r, {0: JUMP, 1: MOVE_RIGHT, 2: MOVE_RIGHT, 30: JUMP}, 31)
        # paused before tick 31: buffered jump (tick 30) pending; queue empty by now
        self.assertIsNotNone(r.buffered_jump_tick)
        r.pause()
        with self.assertRaises(RuntimeError):
            r.step()
        r.resume_ready()
        y_before = r.y
        r.step(PAUSE_RESUMED)
        self.assertIsNone(r.buffered_jump_tick)
        self.assertEqual(r.outcomes[INVALIDATED], 1)
        self.assertNotEqual(r.y, y_before)  # arc continues
        run(r, {}, 10)
        self.assertEqual(len(events_of(r, "JumpStarted")), 1)

    def test_resume_mid_move_keeps_move_drops_queue(self):
        c = cfg(start_lane=0)
        a = Runner(c)
        run(a, {0: MOVE_RIGHT, 1: MOVE_RIGHT}, 2)
        a.pause()
        a.resume_ready()
        run(a, {2: PAUSE_RESUMED}, 12)
        self.assertEqual(a.target_lane, 1)
        self.assertEqual(a.x, 0.0)

    def test_replay_hash_ac46(self):
        import random as _r
        rng = _r.Random(7)
        script = {}
        for t in range(600):
            v = rng.random()
            if v < 0.03:
                script[t] = (MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE)[rng.randrange(4)]
        script[300] = script.get(300, 0) | PAUSE_RESUMED
        hashes = []
        for _ in range(2):
            r = Runner(cfg(), record_events=False)
            hs = []
            for t in range(600):
                r.step(script.get(t, 0))
                hs.append(r.state_hash())
            hashes.append(hs)
        self.assertEqual(hashes[0], hashes[1])


if __name__ == "__main__":
    unittest.main()
