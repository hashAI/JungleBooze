"""Key invariants of the spec 004 pendulum model (tools/sim/pendulum_model.py) and its v2 golden traces.

Run:  python3 -I -m pytest tools/sim/tests -q      or      cd tools/sim && python3 -I -m unittest discover -s tests -t .
The pinned numbers are the T401 results (docs/sim-reports/2026-10-07-fixed-pivot-swing.md), not the spec's hand numbers.
"""

import json
import math
import os
import random
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SIM = os.path.dirname(HERE)
sys.path.insert(0, SIM)

import pendulum_model as pm  # noqa: E402
import pendulum_golden as pg  # noqa: E402
from pendulum_model import DEFAULT as P, SwingTrace, release_at, resolve_swipe, grade_for_tick  # noqa: E402

try:
    import numpy as np
except ImportError:  # pragma: no cover
    np = None

ZG = -P.zone_half_m
FAR = P.far_edge_m


def canon(vc):
    return SwingTrace(vc, ZG)


class TestConfig(unittest.TestCase):
    def test_derived_values(self):
        self.assertEqual(P.pivot_height_m, 17.0)
        self.assertEqual(P.far_edge_m, 12.0)
        self.assertEqual((P.good_start_tick, P.perfect_start_tick, P.perfect_end_tick), (27, 42, 53))
        self.assertEqual(P.release_buffer_ticks, 9)
        self.assertEqual(P.swing_max_ticks, 96)
        self.assertEqual(P.grab_blend_ticks, 6)
        self.assertEqual(P.earliness_ticks, 33)


class TestDeterministicMath(unittest.TestCase):
    def test_float64_error(self):
        es, ec = pm.poly_error_report(float)
        self.assertLess(es, 1e-7)
        self.assertLess(ec, 1e-7)
        self.assertLess(es, 6e-9)

    @unittest.skipIf(np is None, "numpy missing")
    def test_float32_error(self):
        es, ec = pm.poly_error_report(np.float32)
        self.assertLess(es, 2e-7)   # 1.04e-7 measured: just above the spec's 1e-7 (rounding of float32 itself)
        self.assertLess(ec, 2e-7)

    def test_asin_series(self):
        lim = math.sin(math.radians(10.0))
        for i in range(2001):
            a = -lim + 2 * lim * i / 2000
            self.assertLess(abs(pm.det_asin(a) - math.asin(a)), 1e-8)


class TestPendulum(unittest.TestCase):
    def test_catch_speed_table(self):
        exp = {8: 13, 10: 13, 12: 13, 13: 13, 14: 14, 15: 15, 16: 16, 18: 16, 21: 16, 24: 16, 28: 16}
        for v, vc in exp.items():
            tr = SwingTrace(v, ZG)
            self.assertAlmostEqual(P.rope_length_m * float(tr.omega[0]), vc, places=9)

    def test_no_z_jump_at_grab(self):
        for z in (-1.25, -1.0, -0.2, 0.0, 0.9, 1.25):
            tr = SwingTrace(15.0, z)
            self.assertAlmostEqual(tr.hand_z(0), z, delta=1e-6)

    def test_rope_length_constant(self):
        for v in (8, 15, 28):
            tr = SwingTrace(v, ZG)
            for n in range(tr.k_apex + 1):
                d = math.hypot(tr.hand_z(n), P.pivot_height_m - tr.hand_y(n))
                self.assertAlmostEqual(d, 14.0, delta=2e-6)

    def test_apex_tick_and_angle(self):
        for vc, deg in ((13, 43.87), (14, 47.39), (15, 50.96), (16, 54.58)):
            tr = canon(vc)
            self.assertIn(tr.k_apex, (84, 85))     # the spec's hand number was 78..80: wrong, see report
            self.assertLessEqual(tr.k_apex, P.swing_max_ticks)
            peak = math.degrees(max(tr.theta))
            self.assertAlmostEqual(peak, deg, delta=0.02)
            self.assertAlmostEqual(peak, pm.apex_angle_deg(vc), delta=0.5)   # closed form from the bottom
            self.assertLess(peak, P.max_swing_angle_deg)

    def test_peak_angle_all_entries(self):
        for i in range(0, 161):
            v = i * 0.25
            for z in (-1.25, 0.0, 1.25):
                tr = SwingTrace(v, z)
                self.assertLessEqual(math.degrees(max(tr.theta)), 62.0)

    def test_energy_bounded(self):
        # symplectic Euler: oscillating, bounded energy error of about 1.27 % (spec 004 claimed <= 0.1 %)
        th, om = [], []
        L, g, dt = 14.0, 22.0, 1 / 60.0
        t, w = pm.start_angle(ZG), 14.0 / L
        e0 = 0.5 * L * L * w * w + g * L * (1 - math.cos(t))
        worst = 0.0
        for _ in range(3000):
            w -= (g / L) * pm.det_sin(t) * dt
            t += w * dt
            e = 0.5 * L * L * w * w + g * L * (1 - math.cos(t))
            worst = max(worst, abs(e - e0) / e0)
        self.assertGreater(worst, 0.001)
        self.assertLess(worst, 0.015)

    def test_tension_positive_in_catch_range(self):
        for vc in (13, 14, 15, 16):
            tr = canon(vc)
            self.assertGreater(min(tr.tension(n) for n in range(tr.k_apex + 1)), 12.0)

    def test_slack_without_clamp_at_28(self):
        c = 1.0 - 28.0 ** 2 / (2 * 22.0 * 14.0)
        self.assertLess(c, 0.0)                                   # apex above the horizontal
        self.assertIsNotNone(pm.slack_angle_deg(28.0))
        self.assertAlmostEqual(pm.slack_angle_deg(28.0), 100.48, delta=0.05)
        self.assertIsNone(pm.slack_angle_deg(16.0))
        self.assertIsNone(pm.slack_angle_deg(24.0))               # taut up to 86 deg
        # integrate the unclamped swing: tension goes negative
        L, g, dt = 14.0, 22.0, 1 / 60.0
        th, om = pm.start_angle(ZG), 28.0 / L
        neg = None
        for n in range(140):
            om -= (g / L) * math.sin(th) * dt
            th += om * dt
            if L * om * om + g * math.cos(th) < 0:
                neg = n
                break
        self.assertIsNotNone(neg)
        self.assertEqual(neg + 1, 78)

    def test_deterministic_repeat(self):
        a = SwingTrace(17.3, -0.9)
        b = SwingTrace(17.3, -0.9)
        self.assertEqual(a.theta, b.theta)
        self.assertEqual(a.omega, b.omega)

    @unittest.skipIf(np is None, "numpy missing")
    def test_float32_close_to_float64(self):
        rng = random.Random(7)
        worst = 0.0
        for _ in range(100):
            v, z = rng.uniform(8, 28), rng.uniform(-1.25, 1.25)
            a, b = SwingTrace(v, z), SwingTrace(v, z, F=np.float32)
            self.assertEqual(a.k_apex, b.k_apex)
            for k in range(a.k_apex + 1):
                worst = max(worst, abs(float(b.theta[k]) - a.theta[k]))
            self.assertLess(abs(release_at(a, 47, "Perfect").z_land - release_at(b, 47, "Perfect").z_land), 1e-4)
        self.assertLess(worst, 5e-6)


class TestWindows(unittest.TestCase):
    def test_grades(self):
        ka = 85
        self.assertEqual(grade_for_tick(26, ka), "None")
        self.assertEqual(grade_for_tick(27, ka), "Good")
        self.assertEqual(grade_for_tick(41, ka), "Good")
        self.assertEqual(grade_for_tick(42, ka), "Perfect")
        self.assertEqual(grade_for_tick(52, ka), "Perfect")
        self.assertEqual(grade_for_tick(53, ka), "Good")

    def test_buffer(self):
        ka = 85
        self.assertEqual(resolve_swipe(17, ka), (None, "Poor"))      # expires
        self.assertEqual(resolve_swipe(18, ka), (27, "Good"))        # fires at 27
        self.assertEqual(resolve_swipe(26, ka), (27, "Good"))
        self.assertEqual(resolve_swipe(27, ka), (27, "Good"))
        self.assertEqual(resolve_swipe(47, ka), (47, "Perfect"))
        self.assertEqual(resolve_swipe(ka, ka), (ka, "Good"))        # swipe on the apex tick wins
        self.assertEqual(resolve_swipe(ka + 1, ka), (None, "Poor"))
        self.assertEqual(resolve_swipe(None, ka), (None, "Poor"))

    def test_auto_release_is_first_omega_le_zero(self):
        tr = canon(14)
        self.assertGreater(tr.omega[tr.k_apex - 1], 0)
        self.assertLessEqual(tr.omega[tr.k_apex], 0)


class TestRelease(unittest.TestCase):
    def test_impulse_at_fixed_state(self):
        tr = canon(14)
        k = 47
        base = release_at(tr, k, "Poor")
        good = release_at(tr, k, "Good")
        perf = release_at(tr, k, "Perfect")
        c, s = math.cos(math.radians(30)), math.sin(math.radians(30))
        self.assertAlmostEqual(good.vx_raw - base.vx_raw, 1.5 * c, delta=1e-7)
        self.assertAlmostEqual(good.vy_raw - base.vy_raw, 1.5 * s, delta=1e-7)
        self.assertAlmostEqual(perf.vx_raw - base.vx_raw, 3.0 * c, delta=1e-7)
        self.assertAlmostEqual(perf.vy_raw - base.vy_raw, 3.0 * s, delta=1e-7)

    def test_floors(self):
        for vc in (13, 14, 15, 16):
            tr = canon(vc)
            for k in range(27, tr.k_apex + 1):
                r = release_at(tr, k, grade_for_tick(k, tr.k_apex))
                self.assertGreaterEqual(r.vx, 6.0)
                self.assertGreaterEqual(r.vy, 2.0)
            r = release_at(tr, tr.k_apex, "Poor", auto=True)
            self.assertEqual((r.vx, r.vy), (6.0, 2.0))

    def test_landing_closed_form(self):
        r = release_at(canon(15), 47, "Perfect")
        y = r.y0 + r.vy * r.t_land - 8.0 * r.t_land ** 2
        self.assertAlmostEqual(y, 0.0, delta=1e-9)
        self.assertAlmostEqual(r.z_land, r.z0 + r.vx * r.t_land, delta=1e-9)

    def test_pinned_t401_numbers(self):
        # (vC, release, past far edge m): measured, tolerance 0.01 m
        rows = [(13, "poor", 3.324), (14, "poor", 4.201), (15, "poor", 5.053), (16, "poor", 5.875),
                (13, 27, 2.858), (16, 27, 9.2), (13, 47, 7.11), (14, 47, 8.98), (15, 47, 10.87), (16, 47, 12.76),
                (13, 66, 3.42), (16, 66, 6.54)]
        for vc, k, exp in rows:
            tr = canon(vc)
            if k == "poor":
                r = release_at(tr, tr.k_apex, "Poor", auto=True)
            else:
                r = release_at(tr, k, grade_for_tick(k, tr.k_apex))
            self.assertAlmostEqual(r.z_land - FAR, exp, delta=0.012, msg=str((vc, k)))

    def test_every_release_clears_chasm(self):
        rng = random.Random(40199)
        lo, hi = 1e9, -1e9
        for i in range(41):
            v = 8.0 + 0.5 * i
            for _ in range(10):
                tr = SwingTrace(v, rng.uniform(-1.25, -1.25 + v / 60.0))
                for k in range(27, tr.k_apex + 1):
                    r = release_at(tr, k, grade_for_tick(k, tr.k_apex))
                    lo, hi = min(lo, r.z_land), max(hi, r.z_land)
                r = release_at(tr, tr.k_apex, "Poor", auto=True)
                lo, hi = min(lo, r.z_land), max(hi, r.z_land)
        self.assertGreaterEqual(lo, 14.5)       # AC-413 floor
        self.assertLessEqual(hi, 30.0)          # AC-413 ceiling
        self.assertGreaterEqual(lo - FAR, 2.8)  # the real guarantee (spec said 3.2: false, 2.86 at vC 13 / tick 27)

    def test_perfect_beats_poor_and_good(self):
        for vc in (13, 14, 15, 16):
            tr = canon(vc)
            poor = release_at(tr, tr.k_apex, "Poor", auto=True).z_land
            per = [release_at(tr, k, "Perfect").z_land for k in range(42, 53)]
            good = [release_at(tr, k, "Good").z_land for k in list(range(27, 42)) + list(range(53, tr.k_apex + 1))]
            self.assertGreaterEqual(min(per) - poor, 2.5)         # AC-414
            self.assertGreater(min(per), max(good))               # Perfect is the longest throw (spec R404 said no)


class TestChain(unittest.TestCase):
    def test_guided_chain_catches(self):
        rng = random.Random(40198)
        for grade in ("Perfect", "Good", "Poor"):
            for vc in (13, 14, 15, 16):
                for _ in range(200):
                    tr = SwingTrace(vc, rng.uniform(-1.25, -1.25 + vc / 60.0))
                    if grade == "Poor":
                        r = release_at(tr, tr.k_apex, "Poor", auto=True)
                    elif grade == "Perfect":
                        r = release_at(tr, rng.randrange(42, 53), "Perfect")
                    else:
                        ks = [k for k in range(27, tr.k_apex + 1) if not 42 <= k < 53]
                        r = release_at(tr, rng.choice(ks), "Good")
                    T, vx, vy = pm.guide_chain(r, 18.0)
                    self.assertGreaterEqual(T, 0.85 - 1e-12)
                    self.assertLessEqual(T, 1.5 + 1e-12)
                    self.assertAlmostEqual(r.y0 + vy * T - 8 * T * T, 1.5, delta=1e-9)
                    c = pm.chain_catch(r, T, vx, vy)
                    self.assertTrue(c["ok"], msg=str((grade, vc)))
                    self.assertLessEqual(vx, 14.8)       # AC-419 as written (< 13) is false: up to 14.78 after a Good-early


class TestTakeoff(unittest.TestCase):
    def test_windows_550(self):
        # spec 004 4.1 hand numbers were 286 / 355 / 418 / 499 ms; the spec 001 model gives these
        for v, ms in ((8, 333.3), (10, 400.0), (13, 466.7), (21, 533.3)):
            w = pm.takeoff_window(v)
            self.assertTrue(w["contiguous"])
            self.assertAlmostEqual(w["ms"], ms, delta=17.0, msg=str(v))   # +-1 tick
            self.assertGreaterEqual(w["ms"], {8: 280, 10: 350, 13: 410, 21: 0}[v])   # AC-427 floors

    def test_450_is_smaller(self):
        self.assertLess(pm.takeoff_window(10, earliness_ms=450)["ms"], pm.takeoff_window(10)["ms"])


class TestGoldens(unittest.TestCase):
    def test_files_match_model(self):
        self.assertEqual(pg.main(check=True), 0)

    def _load(self, name):
        with open(os.path.join(SIM, "golden", name + ".json")) as f:
            return json.load(f)

    def test_schema_and_structure(self):
        for name, _ in pg.build_all():
            d = self._load(name)
            self.assertEqual(d["schema"], "junglebooze.golden-trace.v2")
            for k in ("vines", "ticks", "tolerance", "config", "derived", "expect"):
                self.assertIn(k, d)
            ts = [t["t"] for t in d["ticks"]]
            self.assertEqual(ts, list(range(ts[0], ts[0] + len(ts))))

    def test_swing_ticks_on_circle_and_pivot_fixed(self):
        for name in ("07_vine_perfect_release", "08_vine_poor_auto_release", "09a_vine_catch_clamp_entry_8",
                     "09b_vine_catch_clamp_entry_21", "12_vine_boost_entry"):
            d = self._load(name)
            pz = d["vines"][0]["z"]
            for t in d["ticks"]:
                if t["state"] == "Carried" and t["t"] >= 6:
                    dz = t["z"] - pz
                    dy = 17.0 - t["hand_y"]
                    self.assertAlmostEqual(math.hypot(dz, dy), 14.0, delta=2e-6)

    def test_landing_tick_matches_closed_form(self):
        for name in ("07_vine_perfect_release", "08_vine_poor_auto_release", "09a_vine_catch_clamp_entry_8"):
            d = self._load(name)
            e = d["expect"]
            rel = [t for t in d["ticks"] if any(ev["type"] == "VineReleased" for ev in t["events"])][0]
            ln = rel["launch"]
            t_land = (ln["vy"] + math.sqrt(ln["vy"] ** 2 + 32 * ln["y0"])) / 16.0
            anchor = e["releaseTick"] - (0 if e["grade"] == "Poor" else 1)
            self.assertLessEqual(abs(e["landingTickAfterGrab"] - (anchor + t_land * 60)), 1.0)

    def test_catch_clamp_goldens(self):
        self.assertEqual(self._load("09a_vine_catch_clamp_entry_8")["expect"]["catchSpeedMps"], 13.0)
        self.assertEqual(self._load("09b_vine_catch_clamp_entry_21")["expect"]["catchSpeedMps"], 16.0)
        a = self._load("09b_vine_catch_clamp_entry_21")["ticks"][0]["swing"]["omega"]
        b = self._load("12_vine_boost_entry")["ticks"][0]["swing"]["omega"]
        self.assertAlmostEqual(a, 16.0 / 14.0, places=6)
        self.assertAlmostEqual(b, 16.0 / 14.0, places=6)

    def test_chain_golden(self):
        e = self._load("10_vine_chain_two")["expect"]
        self.assertTrue(0.85 <= e["guidedFlight"]["T"] <= 1.5)
        self.assertEqual(e["catchSpeedB"], 13.0)

    def test_v1_goldens_untouched(self):
        for n in os.listdir(os.path.join(SIM, "golden")):
            if n[:2] in ("01", "02", "03", "04", "05", "06"):
                self.assertEqual(self._load(n[:-5])["schema"], "junglebooze.golden-trace.v1")


if __name__ == "__main__":
    unittest.main()
