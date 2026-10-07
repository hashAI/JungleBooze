"""Bots for the spec 001 movement gauntlet.

- OracleSolver: "perfect timing, no reaction delay" (S1). Depth-first search over per-tick commands
  {None, Jump, Slide, MoveLeft, MoveRight} on cloned runner states, with duplicate-state pruning.
  A segment is *solved* if some command sequence passes every group with no death and no stumble.
  The search has a node budget; a segment that exhausts it is reported as UNRESOLVED (not proven
  impossible) together with its seed.
- SkillBot: expert / average / new. Reads the course ahead, picks the obvious answer per group and
  times it with a reaction delay, Gaussian timing noise and a per-group error rate.

Skill profiles are [ASSUMED]: `docs/specs/bot-player.md` does not exist yet. They were fixed before
any S4-S6 result was seen and were NOT tuned to hit the targets.
"""

import random

from runner_model import (
    Runner, NONE, MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE,
    RUNNING, SLIDING, AIRBORNE, FAST_FALLING, COYOTE, FALLING, DEAD,
    LOW_BARRIER, HIGH_BARRIER, FULL_BLOCK, GAP,
)


def make_runner(course, record_events=False):
    r = Runner(course.cfg, track=course.track, obstacles=course.obstacles, record_events=record_events)
    r.target_lane = course.start_lane
    r.x = course.cfg.lane_center(course.start_lane)
    return r


def signature(r):
    m = r.move
    mv = (round(m.start_x, 6), m.end_lane, m.elapsed, m.dir, m.is_bounce) if m is not None else None
    t = r.tick
    return (t, round(r.x, 6), r.target_lane, mv, r.queued_lateral, r.state,
            t - r.jump_start if r.state == AIRBORNE else 0,
            (t - r.ff_t0, round(r.ff_y0, 6)) if r.state == FAST_FALLING else None,
            r.slide_left, r.slide_on_land, r.coyote_left,
            (t - r.buffered_jump_tick) if r.buffered_jump_tick is not None else -1,
            round(r.y, 6) if r.state == FALLING else 0)


class OracleSolver(object):
    ALPHABET = (NONE, JUMP, SLIDE, MOVE_LEFT, MOVE_RIGHT)

    def __init__(self, node_budget=400000):
        self.node_budget = node_budget

    def goal(self, r, course):
        hd = course.cfg.player_hitbox_depth_m / 2.0
        return (r.z - hd > course.end_z + 0.05) and r.state in (RUNNING, SLIDING)

    def solve(self, course):
        """Returns (status, commands, nodes). status in {'solved', 'impossible', 'unresolved'}.

        'impossible' means the search space (one command per tick, alphabet above, exact duplicate
        pruning) was exhausted; 'unresolved' means the node budget ran out first.
        """
        root = make_runner(course)
        visited = set()
        stack = [[root, 0, NONE]]
        nodes = 0
        while stack:
            frame = stack[-1]
            r, i = frame[0], frame[1]
            if i >= len(self.ALPHABET):
                stack.pop()
                continue
            frame[1] = i + 1
            cmd = self.ALPHABET[i]
            r2 = r.clone()
            r2.step(cmd)
            nodes += 1
            if nodes > self.node_budget:
                return "unresolved", None, nodes
            if r2.dead or r2.stumbles:
                continue
            sig = signature(r2)
            if sig in visited:
                continue
            visited.add(sig)
            if self.goal(r2, course):
                cmds = [f[2] for f in stack[1:]] + [cmd]
                return "solved", cmds, nodes
            stack.append([r2, 0, cmd])
        return "impossible", None, nodes


# ---------------------------------------------------------------------------
# Skill bots
# ---------------------------------------------------------------------------
class SkillProfile(object):
    def __init__(self, name, reaction_ms, timing_sigma_ms, error_rate, view_distance_m=34.0):
        self.name = name
        self.reaction_ticks = int(round(reaction_ms * 60 / 1000.0))
        self.sigma_ticks = timing_sigma_ms * 60 / 1000.0
        self.error_rate = error_rate
        self.view_distance_m = view_distance_m


# [ASSUMED] until bot-player.md exists. Not tuned to targets.
PROFILES = {
    "expert": SkillProfile("expert", reaction_ms=250, timing_sigma_ms=33, error_rate=0.002),
    "average": SkillProfile("average", reaction_ms=400, timing_sigma_ms=67, error_rate=0.005),
    "new": SkillProfile("new", reaction_ms=600, timing_sigma_ms=100, error_rate=0.02),
}


def _tick_when_z(r0_z, r0_tick, speed, z_target):
    """First tick whose post-move z is >= z_target, for constant speed."""
    dz = speed / 60.0
    n = int((z_target - r0_z) / dz - 1e-9)
    if n < 0:
        n = 0
    while r0_z + (n + 1) * dz < z_target - 1e-12:
        n += 1
    return r0_tick + n  # tick index whose step reaches z_target


class SkillBot(object):
    """Plans the whole course up front from the bot's point of view (it sees the same rows a
    player sees), then emits commands per tick. Decisions follow the GDD colour language:
    low = jump, high = slide, tall = change lane, gap = jump."""

    def __init__(self, profile, seed):
        self.p = profile
        self.rng = random.Random(seed ^ 0x5EED)

    def _noise(self):
        return int(round(self.rng.gauss(0.0, self.p.sigma_ticks)))

    def plan(self, course):
        cfg = course.cfg
        speed = course.speed
        hd = cfg.player_hitbox_depth_m / 2.0
        ticks = {}
        lane = course.start_lane
        prev_clear_tick = 0
        prev_vertical_tick = -100
        for gi, g in enumerate(course.groups):
            # when the hero reaches the group's front face, and its center
            t_front = _tick_when_z(0.0, 0, speed, g.z0 - hd)
            t_center = _tick_when_z(0.0, 0, speed, (g.z0 + g.z1) / 2.0)
            t_seen = _tick_when_z(0.0, 0, speed, g.z0 - self.p.view_distance_m)
            t_react = max(0, t_seen) + self.p.reaction_ticks
            here = g.lanes.get(lane)
            target_lane = lane
            vertical = None
            if g.kind == "gap":
                vertical = JUMP
            elif here is None:
                pass
            elif here == LOW_BARRIER:
                vertical = JUMP
            elif here == HIGH_BARRIER:
                vertical = SLIDE
            else:  # full block: nearest lane that is free, else nearest jumpable/slidable one
                cands = sorted(range(cfg.lane_count), key=lambda l: (g.lanes.get(l) is not None,
                                                                    abs(l - lane), self.rng.random()))
                cands = [l for l in cands if g.lanes.get(l) != FULL_BLOCK]
                target_lane = cands[0]
                a = g.lanes.get(target_lane)
                vertical = JUMP if a == LOW_BARRIER else SLIDE if a == HIGH_BARRIER else None
            # error: wrong or missing answer for this group
            if self.rng.random() < self.p.error_rate:
                kind = self.rng.randrange(3)
                if kind == 0:
                    vertical, target_lane = None, lane          # froze
                elif kind == 1:
                    vertical = SLIDE if vertical == JUMP else JUMP  # wrong vertical answer
                else:
                    target_lane = max(0, min(cfg.lane_count - 1, lane + self.rng.choice((-1, 1))))
            # lateral timing: after reacting and after the previous group is cleared
            if target_lane != lane:
                t_lat = max(t_react, prev_clear_tick) + abs(self._noise())
                d = 1 if target_lane > lane else -1
                for k in range(abs(target_lane - lane)):
                    ticks[t_lat + k] = ticks.get(t_lat + k, 0) | (MOVE_RIGHT if d > 0 else MOVE_LEFT)
                lane = target_lane
            if vertical == JUMP:
                ideal = t_center - cfg.jump_airtime_ticks // 2
                tv = max(t_react, ideal + self._noise())
                ticks[tv] = ticks.get(tv, 0) | JUMP
                prev_vertical_tick = tv
            elif vertical == SLIDE:
                ideal = t_front - 8
                tv = max(t_react, ideal + self._noise())
                ticks[tv] = ticks.get(tv, 0) | SLIDE
                prev_vertical_tick = tv
            prev_clear_tick = _tick_when_z(0.0, 0, speed, g.z1 + hd) + 1
        # MoveLeft+MoveRight in one tick would cancel; keep the later intent
        for t, c in ticks.items():
            if c & MOVE_LEFT and c & MOVE_RIGHT:
                ticks[t] = c & ~MOVE_LEFT
        return ticks


def run_commands(course, cmd_by_tick, max_ticks, record_events=False, hashes=False):
    r = make_runner(course, record_events=record_events)
    hs = [] if hashes else None
    stream = []
    for _ in range(max_ticks):
        c = cmd_by_tick.get(r.tick, NONE)
        stream.append(c)
        r.step(c)
        if hashes:
            hs.append(r.state_hash())
        if r.dead:
            break
    r.finalize()
    return r, stream, hs
