"""Tests for the bot harness used by S4-S7 and the what-if stage (determinism and config overrides)."""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from runner_model import RunnerConfig  # noqa: E402
from test_course import run_course  # noqa: E402
from bots import SkillBot, PROFILES, run_commands  # noqa: E402
import run_targets  # noqa: E402


class BotHarnessTests(unittest.TestCase):
    def test_bot_run_is_deterministic(self):
        setup = dict(target="S5", bot="average", speed=10.0, tier=1, duration_s=20, runs=1)
        a = run_targets._bot_run(setup, 7100002)
        b = run_targets._bot_run(setup, 7100002)
        self.assertEqual(a, b)

    def test_plan_replays_to_same_state(self):
        course = run_course(7200000, 8.0, 1, 20)
        plan = SkillBot(PROFILES["new"], 7200000).plan(course)
        r1, _, h1 = run_commands(course, plan, 20 * 60 + 90, hashes=True)
        r2, _, h2 = run_commands(course, dict(plan), 20 * 60 + 90, hashes=True)
        self.assertEqual(h1, h2)
        self.assertEqual(r1.state_hash(), r2.state_hash())

    def test_outcome_accounting_holds_for_bot_runs(self):
        setup = dict(target="S6", bot="new", speed=8.0, tier=1, duration_s=30, runs=1)
        for seed in range(7200000, 7200010):
            row = run_targets._bot_run(setup, seed)
            self.assertEqual(sum(row["outcomes"].values()), row["flags"], "seed %d" % seed)

    def test_whatif_override_reaches_the_runner(self):
        cfg = RunnerConfig(fixed_speed_mps=10.0, use_start_ramp=False, jump_airtime_ms=650.0)
        self.assertEqual(cfg.jump_airtime_ticks, 39)
        course = run_course(7100002, 10.0, 1, 10, cfg=cfg)
        self.assertEqual(course.cfg.jump_airtime_ticks, 39)

    def test_whatif_baseline_jump_window_matches_s2a(self):
        # S2a measured 16-17 ticks at 8 m/s with the start values
        self.assertEqual(run_targets._jump_window_cfg(8.0, {}), (16, 17))


if __name__ == "__main__":
    unittest.main()
