"""Golden traces on disk must match runner_model.py, and pin the key spec numbers."""

import json
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import golden  # noqa: E402


def load(name):
    with open(os.path.join(HERE, "golden", name + ".json")) as f:
        return json.load(f)


def by_tick(doc):
    return dict((t["t"], t) for t in doc["ticks"])


class GoldenTests(unittest.TestCase):
    def test_files_match_model(self):
        self.assertEqual(golden.main(check=True), 0)

    def test_schema_fields(self):
        for sc in golden.scenarios():
            d = load(sc["name"])
            self.assertEqual(d["schema"], golden.SCHEMA)
            for t in d["ticks"]:
                for k in ("t", "cmd", "cmd_bits", "lane", "occupied_lane", "x", "y", "z", "state"):
                    self.assertIn(k, t)

    def test_lane_switch_numbers(self):
        t = by_tick(load("01_lane_switch_and_reverse"))
        self.assertEqual(t[6]["x"], 2.4)
        self.assertLess(t[5]["x"], 2.4)

    def test_double_queue_numbers(self):
        t = by_tick(load("02_double_lane_queue_bump_cancel"))
        self.assertEqual(t[10]["x"], 2.4)
        self.assertLess(t[9]["x"], 2.4)
        self.assertEqual(t[33]["lane"], 1)

    def test_jump_numbers(self):
        t = by_tick(load("03_jump_buffered_and_expired"))
        self.assertEqual(t[18]["y"], 1.5)
        self.assertEqual(t[36]["y"], 0.0)
        self.assertEqual([e["type"] for e in t[36]["events"]], ["Landed", "JumpStarted"])
        self.assertEqual(t[79]["state"], "Running")

    def test_coyote_numbers(self):
        t = by_tick(load("04_coyote_jump"))
        self.assertEqual(t[31]["state"], "Coyote")
        self.assertTrue(t[36]["events"][0]["coyote"])
        self.assertEqual(t[72]["state"], "Running")

    def test_fastfall_numbers(self):
        t = by_tick(load("05_fastfall_into_slide"))
        self.assertEqual(t[17]["state"], "Sliding")
        self.assertEqual(sum(1 for k in t if t[k]["state"] == "Sliding"), 40 - 17 + 39)

    def test_pause_numbers(self):
        d = load("06_pause_resume")
        t = by_tick(d)
        self.assertEqual(d["outcomes"].get("Invalidated"), 1)
        self.assertEqual(t[43]["lane"], 1)
        self.assertEqual(t[36]["state"], "Running")


if __name__ == "__main__":
    unittest.main()
