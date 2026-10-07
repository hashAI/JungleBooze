"""Seeded movement gauntlet (spec 001 section 15).

Week 1 has no track generator, so this builds a TestTrackQuery-style course from the reference
obstacles (spec 001 9.2) and full-width gaps (2-4 m), placed single or in pairs, spaced by the
GDD 11.2 tier "min time between required actions".

[ASSUMED] definitions (the spec does not pin them; listed in the sim report):
- A *group* is one row of the course: `single` (one lane blocked by LowBarrier, HighBarrier or
  FullBlock), `pair` (two lanes blocked, each by a random archetype; one lane stays free) or `gap`
  (full-width hole 2-4 m long, so a jump is the only answer).
- *Spacing* is measured front face to front face of consecutive groups, in seconds at the
  segment speed. If the previous group is a gap that is longer than the spacing distance, the
  next group is pushed to 0.5 m after the gap's far edge (counted as `stretched`).
- A gauntlet *segment* (S1) is 3 groups at exactly the tier's minimum spacing, starting with
  HERO running in a seeded random lane, at constant speed, start ramp off.
- A *run course* (S4-S6) uses the tier density (obstacles per 100 m) for the mean spacing and
  the tier minimum spacing as the floor.

Randomness: Python's random.Random(seed) (Mersenne Twister). This is tooling, not game code;
golden traces store explicit commands, so C# never needs to reproduce this generator.
"""

import random

from runner_model import (
    RunnerConfig, TestTrackQuery, make_obstacle, REFERENCE_BOXES,
    LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK, GAP,
)

ARCHETYPES = (LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK)

# GDD 11.2
TIERS = {
    1: dict(min_spacing_s=0.90, per_100m=4),
    2: dict(min_spacing_s=0.75, per_100m=6),
    3: dict(min_spacing_s=0.65, per_100m=7),
    4: dict(min_spacing_s=0.55, per_100m=8),
    5: dict(min_spacing_s=0.50, per_100m=9),
    6: dict(min_spacing_s=0.45, per_100m=10),
}

S1_SPEEDS = (8.0, 10.0, 13.5, 17.5, 21.0, 33.6)

GROUP_WEIGHTS = (("single", 0.45), ("pair", 0.35), ("gap", 0.20))  # [ASSUMED]


class Group(object):
    __slots__ = ("kind", "z0", "z1", "lanes", "gap_len")

    def __init__(self, kind, z0, z1, lanes, gap_len=0.0):
        self.kind = kind
        self.z0 = z0
        self.z1 = z1
        self.lanes = lanes      # dict lane -> archetype (GAP for all lanes of a gap)
        self.gap_len = gap_len

    def describe(self):
        if self.kind == "gap":
            return "gap %.2fm @%.2f" % (self.gap_len, self.z0)
        return "%s %s @%.2f" % (self.kind, ",".join("L%d=%s" % (l, a) for l, a in sorted(self.lanes.items())),
                                self.z0)


class Course(object):
    def __init__(self, cfg, groups, start_lane, seed, speed, spacing_s, stretched=0):
        self.cfg = cfg
        self.groups = groups
        self.start_lane = start_lane
        self.seed = seed
        self.speed = speed
        self.spacing_s = spacing_s
        self.stretched = stretched
        self.obstacles = []
        gaps = []
        oid = 1
        for g in groups:
            if g.kind == "gap":
                gaps.append((g.z0, g.z1, -50.0, 50.0))
            else:
                for lane, arch in sorted(g.lanes.items()):
                    self.obstacles.append(make_obstacle(cfg, oid, arch, lane, g.z0))
                    oid += 1
        self.track = TestTrackQuery(gaps)
        self.end_z = max(g.z1 for g in groups) if groups else 0.0
        self.obstacle_lane = dict((o.id, o.lane) for o in self.obstacles)

    def describe(self):
        return "; ".join(g.describe() for g in self.groups)


def _pick(rng, weights):
    v = rng.random() * sum(w for _, w in weights)
    for name, w in weights:
        if v < w:
            return name
        v -= w
    return weights[-1][0]


def random_group(rng, cfg, z0, kinds=GROUP_WEIGHTS, gap_max=4.0):
    kind = _pick(rng, kinds)
    if kind == "gap":
        length = rng.uniform(2.0, gap_max)
        return Group("gap", z0, z0 + length, dict((l, GAP) for l in range(cfg.lane_count)), length)
    if kind == "single":
        lanes = {rng.randrange(cfg.lane_count): rng.choice(ARCHETYPES)}
    else:
        free = rng.randrange(cfg.lane_count)
        lanes = dict((l, rng.choice(ARCHETYPES)) for l in range(cfg.lane_count) if l != free)
    depth = max(REFERENCE_BOXES[a][1] for a in lanes.values())
    return Group(kind, z0, z0 + depth, lanes)


def _place(groups, z_next):
    """Push z_next past a previous gap if needed. Returns (z, stretched?)."""
    if groups and groups[-1].kind == "gap" and z_next < groups[-1].z1 + 0.5:
        return groups[-1].z1 + 0.5, True
    return z_next, False


def gauntlet_segment(seed, speed, tier, n_groups=3, lead_s=1.2, cfg=None):
    """S1 segment: n_groups at exactly the tier's minimum spacing."""
    rng = random.Random(seed)
    cfg = cfg or RunnerConfig(fixed_speed_mps=speed, use_start_ramp=False)
    spacing = TIERS[tier]["min_spacing_s"]
    start_lane = rng.randrange(cfg.lane_count)
    z = max(6.0, speed * lead_s)
    groups = []
    stretched = 0
    for i in range(n_groups):
        z, s = _place(groups, z)
        stretched += s
        g = random_group(rng, cfg, z)
        groups.append(g)
        z = g.z0 + spacing * speed
    return Course(cfg, groups, start_lane, seed, speed, spacing, stretched)


def run_course(seed, speed, tier, duration_s, cfg=None, lead_s=2.0, gap_max=4.0):
    """S4-S6 course: tier density for mean spacing, tier minimum as the floor."""
    rng = random.Random(seed)
    cfg = cfg or RunnerConfig(fixed_speed_mps=speed, use_start_ramp=False)
    t = TIERS[tier]
    min_d = t["min_spacing_s"] * speed
    mean_d = 100.0 / t["per_100m"]
    hi_d = max(min_d, 2.0 * mean_d - min_d)
    length = speed * duration_s
    z = max(10.0, speed * lead_s)
    groups = []
    stretched = 0
    while z < length:
        z, s = _place(groups, z)
        stretched += s
        g = random_group(rng, cfg, z, gap_max=gap_max)
        groups.append(g)
        z = g.z0 + rng.uniform(min_d, hi_d)
    return Course(cfg, groups, cfg.start_lane, seed, speed, t["min_spacing_s"], stretched)


def fixed_course(speed, groups_spec, start_lane=1, cfg=None):
    """groups_spec: list of (kind, z0, lanes-dict or gap length)."""
    cfg = cfg or RunnerConfig(fixed_speed_mps=speed, use_start_ramp=False)
    groups = []
    for kind, z0, extra in groups_spec:
        if kind == "gap":
            groups.append(Group("gap", z0, z0 + extra, dict((l, GAP) for l in range(cfg.lane_count)), extra))
        else:
            depth = max(REFERENCE_BOXES[a][1] for a in extra.values())
            groups.append(Group(kind, z0, z0 + depth, dict(extra)))
    return Course(cfg, groups, start_lane, 0, speed, 0.0)
