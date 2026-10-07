"""Deterministic tick-based reference model of spec 001 (player movement).

Python 3 standard library only. This is the executable reference the C# simulation
(`JungleBooze.Gameplay` runner) is checked against through golden traces
(see README.md). It is NOT game code: it exists so balance runs and golden traces can be
produced without the Unity editor.

Every rule cites the spec section it implements. Places where the spec is silent or
ambiguous carry a comment `[MODEL-CHOICE]`; they are listed in the sim report.

Per-tick processing order follows spec 001 rule I8:
 (1) read commands, (2) PauseResumed clears buffers, (3) resolve conflicts (I7),
 (4) age the buffer, (5) lateral command, (6) vertical command, (7) speed and z,
 (8) lane tween and queued-move start, (9) vertical motion, ground check, landing and
 buffered fire, (10) slide timer, (11) collisions, (12) events.
"""

import math

# ---------------------------------------------------------------------------
# Commands: bit values match JungleBooze.Core.Input.InputCommand (append-only enum).
# ---------------------------------------------------------------------------
NONE = 0
MOVE_LEFT = 1 << 0
MOVE_RIGHT = 1 << 1
JUMP = 1 << 2
SLIDE = 1 << 3
COMPANION_ASSIST = 1 << 4
PAUSE_RESUMED = 1 << 5  # spec 001 section 8.5 (new flag)

COMMAND_NAMES = {
    MOVE_LEFT: "MoveLeft",
    MOVE_RIGHT: "MoveRight",
    JUMP: "Jump",
    SLIDE: "Slide",
    COMPANION_ASSIST: "CompanionAssist",
    PAUSE_RESUMED: "PauseResumed",
}
NAME_TO_COMMAND = {v: k for k, v in COMMAND_NAMES.items()}

# Movement command flags that must receive exactly one outcome (I9).
MOVEMENT_FLAGS = (MOVE_LEFT, MOVE_RIGHT, JUMP, SLIDE)

# Outcomes (I9)
EXECUTED = "Executed"
QUEUED = "Queued"
BUFFERED = "Buffered"
BUMPED = "Bumped"
CANCELLED = "Cancelled"
SUPERSEDED = "Superseded"
EXPIRED = "Expired"
INVALIDATED = "Invalidated"
ALREADY_ACTIVE = "AlreadyActive"
IGNORED = "Ignored"
OUTCOMES = (EXECUTED, QUEUED, BUFFERED, BUMPED, CANCELLED, SUPERSEDED, EXPIRED,
            INVALIDATED, ALREADY_ACTIVE, IGNORED)

# Locomotion states (6.1)
RUNNING = "Running"
SLIDING = "Sliding"
AIRBORNE = "Airborne"
FAST_FALLING = "FastFalling"
COYOTE = "Coyote"
FALLING = "Falling"
DEAD = "Dead"

# Obstacle archetypes (9.1)
LOW_BARRIER = "LowBarrier"
HIGH_BARRIER = "HighBarrier"
FULL_BLOCK = "FullBlock"
MOVER = "Mover"
GAP = "Gap"

# Contact categories (9.3)
ENTRY_FRONT = "front"
ENTRY_BELOW = "below"
ENTRY_SIDE = "side"
ENTRY_TOP = "top"
ENTRY_TIE = "tie"
ENTRY_INSIDE = "inside"  # [MODEL-CHOICE] boxes already overlapping at tick start (hitbox grew)

EPS = 1e-9


def ms_to_ticks(ms, hz=60):
    """Spec 001 section 2: ticks = max(1, floor(ms * 60 / 1000 + 0.5))."""
    return max(1, int(math.floor(ms * hz / 1000.0 + 0.5)))


class RunnerConfig(object):
    """Immutable-by-convention runtime config built from designer units (3.1)."""

    def __init__(self, **overrides):
        # Authoring values (section 3.1 start values)
        self.lane_count = 3
        self.lane_width_m = 2.4
        self.start_lane = 1
        self.lane_switch_ms = 120.0
        self.lane_switch_ease_exponent = 2.0
        self.lane_queue_start_fraction = 0.5
        self.edge_forgiveness_fraction = 0.6
        self.jump_apex_height_m = 1.5
        self.jump_airtime_ms = 600.0
        self.fast_fall_min_speed_mps = 15.0
        self.fast_fall_max_ms = 100.0
        self.slide_ms = 650.0
        self.input_buffer_ms = 150.0
        self.coyote_ms = 80.0
        self.player_hitbox_width_m = 0.7
        self.player_hitbox_depth_m = 0.5
        self.standing_height_m = 1.8
        self.sliding_height_m = 0.8
        self.fall_death_depth_m = 1.0
        self.run_start_ramp_ms = 500.0
        self.run_start_speed_fraction = 0.5
        self.stumble_bounce_ms = 150.0
        self.stumble_daze_ms = 3000.0
        self.near_miss_distance_m = 0.35
        # SpeedCurve (3.2)
        self.speed_rows = ((0.0, 10.0), (500.0, 12.0), (1100.0, 13.5), (2300.0, 15.5),
                           (3600.0, 17.5), (5000.0, 19.0), (7000.0, 21.0))
        self.tutorial_speed_mps = 8.0
        # Model-only switches (not spec fields)
        self.fixed_speed_mps = None   # if set: ignore curve (gauntlet runs at constant speed)
        self.use_start_ramp = True
        self.speed_multiplier = 1.0
        self.tutorial = False
        for k, v in overrides.items():
            if not hasattr(self, k):
                raise AttributeError("unknown config field " + k)
            setattr(self, k, v)
        self._derive()

    def _derive(self):
        self.lane_switch_ticks = ms_to_ticks(self.lane_switch_ms)
        self.jump_airtime_ticks = ms_to_ticks(self.jump_airtime_ms)
        self.fast_fall_max_ticks = ms_to_ticks(self.fast_fall_max_ms)
        self.slide_ticks = ms_to_ticks(self.slide_ms)
        self.input_buffer_ticks = ms_to_ticks(self.input_buffer_ms)
        self.coyote_ticks = ms_to_ticks(self.coyote_ms)
        self.run_start_ramp_ticks = ms_to_ticks(self.run_start_ramp_ms) if self.run_start_ramp_ms > 0 else 0
        self.stumble_bounce_ticks = ms_to_ticks(self.stumble_bounce_ms)
        self.stumble_daze_ticks = ms_to_ticks(self.stumble_daze_ms)
        self.airtime_s = self.jump_airtime_ticks / 60.0
        self.gravity_mps2 = 8.0 * self.jump_apex_height_m / (self.airtime_s ** 2)
        self.jump_velocity_mps = 4.0 * self.jump_apex_height_m / self.airtime_s
        self.lane_queue_start_tick = int(math.ceil(self.lane_switch_ticks * self.lane_queue_start_fraction - EPS))
        self.edge_forgiveness_m = self.edge_forgiveness_fraction * self.lane_width_m

    def lane_center(self, lane):
        return (lane - (self.lane_count - 1) / 2.0) * self.lane_width_m

    def config_hash(self):
        keys = sorted(k for k in self.__dict__)
        s = ";".join("%s=%r" % (k, self.__dict__[k]) for k in keys)
        h = 2166136261
        for ch in s.encode("utf-8"):
            h ^= ch
            h = (h * 16777619) & 0xFFFFFFFF
        return "%08x" % h


# ---------------------------------------------------------------------------
# Track (6.5): ITrackQuery.HasGround(x, zMin, zMax) under the HERO footprint.
# ---------------------------------------------------------------------------
class FlatTrackQuery(object):
    """Week-1 default: ground everywhere."""

    def has_ground(self, x_min, x_max, z_min, z_max):
        return True


class TestTrackQuery(object):
    """Ground with rectangular holes. gaps: list of (z0, z1, x0, x1).

    Ground exists under the footprint unless a single gap rectangle covers the whole
    footprint [MODEL-CHOICE: adjacent gaps are not merged; gauntlet gaps are full width].
    """

    def __init__(self, gaps=()):
        self.gaps = sorted(gaps)

    def has_ground(self, x_min, x_max, z_min, z_max):
        for (g0, g1, gx0, gx1) in self.gaps:
            if g0 > z_min:
                break
            if g0 <= z_min and g1 >= z_max and gx0 <= x_min and gx1 >= x_max:
                return False
        return True


class Obstacle(object):
    __slots__ = ("id", "archetype", "lane", "x0", "x1", "y0", "y1", "z0", "z1")

    def __init__(self, oid, archetype, lane, x0, x1, y0, y1, z0, z1):
        self.id = oid
        self.archetype = archetype
        self.lane = lane
        self.x0, self.x1, self.y0, self.y1, self.z0, self.z1 = x0, x1, y0, y1, z0, z1

    def __repr__(self):
        return "Obstacle(%d,%s,lane=%d,z=%.3f..%.3f,y=%.2f..%.2f)" % (
            self.id, self.archetype, self.lane, self.z0, self.z1, self.y0, self.y1)


# Reference boxes, spec 001 section 9.2: (width, depth, bottom, top)
REFERENCE_BOXES = {
    LOW_BARRIER: (2.04, 0.6, 0.0, 0.8),
    HIGH_BARRIER: (2.04, 0.5, 1.1, 3.0),
    FULL_BLOCK: (2.04, 1.0, 0.0, 3.0),
}


def make_obstacle(cfg, oid, archetype, lane, z_front, width=None, depth=None, y0=None, y1=None):
    w, d, b, t = REFERENCE_BOXES[archetype]
    if width is not None:
        w = width
    if depth is not None:
        d = depth
    if y0 is not None:
        b = y0
    if y1 is not None:
        t = y1
    cx = cfg.lane_center(lane)
    return Obstacle(oid, archetype, lane, cx - w / 2.0, cx + w / 2.0, b, t, z_front, z_front + d)


def _axis_interval(a0, a1, b0, b1, c, d):
    """Times t in [0,1] where moving interval [a(t), b(t)] strictly overlaps [c, d].

    a(t) = a0 + (a1-a0) t, b(t) = b0 + (b1-b0) t. Returns (lo, hi, overlapping_at_0) or None.
    """
    lo, hi = 0.0, 1.0
    # a(t) < d
    da = a1 - a0
    if abs(da) < 1e-15:
        if not a0 < d:
            return None
    else:
        r = (d - a0) / da
        if da > 0:
            hi = min(hi, r)
        else:
            lo = max(lo, r)
    # b(t) > c
    db = b1 - b0
    if abs(db) < 1e-15:
        if not b0 > c:
            return None
    else:
        r = (c - b0) / db
        if db > 0:
            lo = max(lo, r)
        else:
            hi = min(hi, r)
    if lo >= hi - 1e-12:
        return None
    at0 = (a0 < d) and (b0 > c)
    return (lo, hi, at0)


class LaneMove(object):
    __slots__ = ("start_x", "end_lane", "end_x", "elapsed", "duration", "dir", "is_bounce")

    def __init__(self, start_x, end_lane, end_x, duration, direction, is_bounce=False):
        self.start_x = start_x
        self.end_lane = end_lane
        self.end_x = end_x
        self.elapsed = 0
        self.duration = duration
        self.dir = direction
        self.is_bounce = is_bounce

    def copy(self):
        m = LaneMove(self.start_x, self.end_lane, self.end_x, self.duration, self.dir, self.is_bounce)
        m.elapsed = self.elapsed
        return m


class Runner(object):
    """One run of spec 001 movement. Call step(commands) once per 60 Hz tick."""

    def __init__(self, cfg=None, track=None, obstacles=(), record_events=True):
        self.cfg = cfg or RunnerConfig()
        self.track = track or FlatTrackQuery()
        self.obstacles = sorted(obstacles, key=lambda o: o.z0)
        self.record_events = record_events
        c = self.cfg
        self.tick = 0              # tick that the next step() executes
        self.z = 0.0
        self.speed = 0.0
        self.x = c.lane_center(c.start_lane)
        self.target_lane = c.start_lane
        self.move = None
        self.queued_lateral = 0
        self.state = RUNNING
        self.y = 0.0
        self.jump_start = 0
        self.fall_t0 = 0
        self.fall_y0 = 0.0
        self.fall_v0 = 0.0
        self.ff_t0 = 0
        self.ff_y0 = 0.0
        self.ff_v = 0.0
        self.air_t0 = 0            # tick HERO left the ground (for Landed.airTicks)
        self.slide_left = 0
        self.slide_on_land = False
        self.coyote_left = 0
        self.buffered_jump_tick = None
        self.daze_left = 0
        self.invulnerable_ticks = 0
        self.dead = False
        self.death = None          # dict(cause, archetype, obstacle_id, after_stumble, entry, tick)
        self.ignored_obstacles = set()
        self.stumbles = []         # list of dicts
        self.outcomes = dict((o, 0) for o in OUTCOMES)
        self.flags_received = 0
        self.buffered_entered = 0
        self.events = []           # (tick, type, payload) if record_events
        self.paused = False
        self._obs_lo = 0           # first obstacle index that may still matter
        self._speed_row = 0
        self.max_lethal_old_lane = 0  # S7 probe counter
        if self.record_events:
            self._emit("RunStarted", lane=self.target_lane)

    # ------------------------------------------------------------------ utils
    def clone(self):
        r = object.__new__(Runner)
        r.__dict__.update(self.__dict__)
        r.move = self.move.copy() if self.move is not None else None
        r.ignored_obstacles = set(self.ignored_obstacles)
        r.stumbles = list(self.stumbles)
        r.outcomes = dict(self.outcomes)
        r.events = list(self.events) if self.record_events else []
        r.death = dict(self.death) if self.death else None
        return r

    def _emit(self, etype, **payload):
        if self.record_events:
            self.events.append((self.tick, etype, payload))

    def _outcome(self, outcome):
        self.outcomes[outcome] += 1

    def hitbox_height(self):
        return self.cfg.sliding_height_m if self.state == SLIDING else self.cfg.standing_height_m

    def occupied_lane(self):
        """Lane whose center is nearest to X; a tie goes to TargetLane (5.1)."""
        c = self.cfg
        best, best_d = None, None
        for lane in range(c.lane_count):
            d = abs(self.x - c.lane_center(lane))
            if best is None or d < best_d - 1e-12:
                best, best_d = lane, d
            elif abs(d - best_d) <= 1e-12 and lane == self.target_lane:
                best = lane
        return best

    def lane_move_progress(self):
        if self.move is None:
            return 1.0
        return self.move.elapsed / float(self.move.duration)

    def _has_ground(self):
        c = self.cfg
        hw = c.player_hitbox_width_m / 2.0
        hd = c.player_hitbox_depth_m / 2.0
        return self.track.has_ground(self.x - hw, self.x + hw, self.z - hd, self.z + hd)

    def _valid_lane(self, lane):
        return 0 <= lane < self.cfg.lane_count

    def pause(self):
        """8.2: while paused the simulation does not step."""
        self.paused = True

    def resume_ready(self):
        """8.4: countdown finished. The caller must put PAUSE_RESUMED on the next tick's commands."""
        self.paused = False

    def state_tuple(self):
        return (self.tick, round(self.x, 9), round(self.y, 9), round(self.z, 9), self.state,
                self.target_lane, self.move.elapsed if self.move else -1, self.queued_lateral,
                self.slide_left, self.coyote_left, self.buffered_jump_tick, self.daze_left, self.dead)

    def state_hash(self):
        h = 2166136261
        for ch in repr(self.state_tuple()).encode("ascii"):
            h ^= ch
            h = (h * 16777619) & 0xFFFFFFFF
        return h

    def finalize(self):
        """End of run: a still-pending buffered Jump keeps outcome Buffered (I9)."""
        if self.buffered_jump_tick is not None:
            self._outcome(BUFFERED)
            self.buffered_jump_tick = None

    # ------------------------------------------------------------------ speed
    def _base_speed(self):
        c = self.cfg
        if c.fixed_speed_mps is not None:
            return c.fixed_speed_mps
        if c.tutorial:
            return c.tutorial_speed_mps
        return speed_curve(c.speed_rows, self.z)

    # ------------------------------------------------------------------ step
    def step(self, commands=NONE):
        if self.paused:
            raise RuntimeError("step() called while paused (spec 001 8.2)")
        c = self.cfg
        t = self.tick
        x_prev, y_prev, z_prev = self.x, self.y, self.z

        # (1) read commands; count movement flags (I9)
        for f in MOVEMENT_FLAGS:
            if commands & f:
                self.flags_received += 1

        if self.dead:
            for f in MOVEMENT_FLAGS:
                if commands & f:
                    self._outcome(IGNORED)
            self.tick += 1
            return

        # (2) PauseResumed clears the buffer and the lateral queue (I6, 8.5)
        if commands & PAUSE_RESUMED:
            if self.buffered_jump_tick is not None:
                self._outcome(INVALIDATED)  # [MODEL-CHOICE] I9 has no "Cleared" outcome
                self.buffered_jump_tick = None
            self.queued_lateral = 0

        # (3) same-tick conflicts (I7)
        lateral = 0
        if (commands & MOVE_LEFT) and (commands & MOVE_RIGHT):
            self._outcome(CANCELLED)
            self._outcome(CANCELLED)
        elif commands & MOVE_LEFT:
            lateral = -1
        elif commands & MOVE_RIGHT:
            lateral = 1
        jump = bool(commands & JUMP)
        slide = bool(commands & SLIDE)
        if jump and slide:
            self._outcome(SUPERSEDED)
            slide = False

        # (4) age the buffer (I2, I4): valid while age <= inputBufferTicks
        if self.buffered_jump_tick is not None and t - self.buffered_jump_tick > c.input_buffer_ticks:
            self._outcome(EXPIRED)
            self.buffered_jump_tick = None

        # (5) lateral command (5.3)
        if lateral:
            self._lateral(lateral)

        # (6) vertical command
        if jump:
            self._jump_command()
        elif slide:
            self._slide_command()

        # (7) speed and z (4.1)
        base = self._base_speed()
        if c.use_start_ramp and c.run_start_ramp_ticks > 0 and t < c.run_start_ramp_ticks:
            frac = c.run_start_speed_fraction + (1.0 - c.run_start_speed_fraction) * (t / float(c.run_start_ramp_ticks))
            speed = base * frac
        else:
            speed = base
        speed *= c.speed_multiplier
        self.speed = speed
        self.z += speed / 60.0
        if self.record_events and c.fixed_speed_mps is None and not c.tutorial:
            rows = c.speed_rows
            while self._speed_row + 1 < len(rows) and self.z >= rows[self._speed_row + 1][0]:
                self._speed_row += 1
                self._emit("SpeedStepReached", row=self._speed_row)

        # (8) lane tween and queued-move start (5.2)
        self._lane_tween()

        # (9) vertical motion, ground check, landing, buffered fire
        self._vertical_motion()

        # (10) slide timer (6.4)
        if not self.dead and self.state == SLIDING:
            if self.slide_left <= 0:
                if not self._standing_box_blocked(z_prev):
                    self.state = RUNNING
                    self._emit("SlideEnded", reason="Timeout")
                # else: extend tick by tick, no cap (6.4.5)
            else:
                self.slide_left -= 1

        # (11) collisions (9)
        if not self.dead:
            if self.daze_left > 0:
                self.daze_left -= 1
                if self.daze_left == 0:
                    self._emit("DazeEnded")
            if self.invulnerable_ticks > 0:
                self.invulnerable_ticks -= 1
            else:
                self._collisions(x_prev, y_prev, z_prev)

        # (12) events are appended as they happen; nothing else to do here.
        self.tick += 1

    # ------------------------------------------------------------------ lanes
    def _start_move(self, start_x, end_lane, direction, reversal=False, queued=False):
        c = self.cfg
        from_lane = self.target_lane
        self.move = LaneMove(start_x, end_lane, c.lane_center(end_lane), c.lane_switch_ticks, direction)
        self.target_lane = end_lane
        self._emit("LaneChangeStarted", frm=from_lane, to=end_lane, dir=direction,
                   reversal=reversal, queued=queued)

    def _lateral(self, d):
        c = self.cfg
        # L1
        if self.state == DEAD or self.y < 0:
            self._outcome(IGNORED)
            return
        m = self.move
        # L2
        if m is not None and m.is_bounce:
            self.queued_lateral = d
            self._outcome(QUEUED)
            return
        # L3
        if m is None:
            if self._valid_lane(self.target_lane + d):
                self._start_move(self.x, self.target_lane + d, d)
                self._outcome(EXECUTED)
            else:
                self._emit("LaneBlocked", dir=d)
                self._outcome(BUMPED)
            return
        if d == -m.dir:
            if self.queued_lateral != 0:
                # L4: cancel the queued move only
                self.queued_lateral = 0
                self._emit("LaneChangeCancelled", dir=d)
                self._outcome(EXECUTED)  # [MODEL-CHOICE] the cancelling command itself
                return
            # L5: reverse from current X back to the origin lane
            origin = m.end_lane - m.dir
            self._start_move(self.x, origin, d, reversal=True)
            self._outcome(EXECUTED)
            return
        # same direction
        if self.queued_lateral != 0:
            # L7
            self._emit("LaneBlocked", dir=d)
            self._outcome(BUMPED)
            return
        # L6
        if not self._valid_lane(self.target_lane + d):
            self._emit("LaneBlocked", dir=d)
            self._outcome(BUMPED)
            return
        if m.elapsed >= c.lane_queue_start_tick:
            # active move already completed >= laneQueueStartTick ticks: start at once
            self._start_move(self.x, self.target_lane + d, d, queued=True)
            self._outcome(EXECUTED)
        else:
            self.queued_lateral = d
            self._outcome(QUEUED)

    def _lane_tween(self):
        c = self.cfg
        m = self.move
        if m is None:
            if self.queued_lateral != 0:
                # only possible after a stumble bounce ended (L2): start it now as an L3 command
                d = self.queued_lateral
                self.queued_lateral = 0
                if self._valid_lane(self.target_lane + d) and not self.dead and self.y >= 0:
                    self._start_move(self.x, self.target_lane + d, d, queued=True)
                    m = self.move
                else:
                    self._emit("LaneBlocked", dir=d)
                    return
            else:
                return
        if (not m.is_bounce) and self.queued_lateral != 0 and m.elapsed >= c.lane_queue_start_tick:
            d = self.queued_lateral
            self.queued_lateral = 0
            self._start_move(self.x, self.target_lane + d, d, queued=True)
            m = self.move
        m.elapsed += 1
        if m.elapsed >= m.duration:
            self.x = m.end_x
            self.move = None
        else:
            u = m.elapsed / float(m.duration)
            p = 1.0 - (1.0 - u) ** c.lane_switch_ease_exponent
            self.x = m.start_x + (m.end_x - m.start_x) * p

    # ------------------------------------------------------------------ vertical
    def _start_jump(self, buffered=False, coyote=False):
        if self.state == SLIDING:
            self._emit("SlideEnded", reason="Jump")
        self.state = AIRBORNE
        self.jump_start = self.tick
        self.air_t0 = self.tick
        self.slide_on_land = False
        self.slide_left = 0
        self._emit("JumpStarted", fromSlide=False, coyote=coyote, buffered=buffered)

    def _jump_command(self):
        s = self.state
        if s == DEAD or self.y < 0:
            self._outcome(IGNORED)
            return
        if s in (RUNNING, SLIDING, COYOTE):
            from_slide = (s == SLIDING)
            if from_slide:
                self._emit("SlideEnded", reason="Jump")
                self.state = RUNNING
            self._start_jump(coyote=(s == COYOTE))
            if from_slide and self.record_events:
                tk, et, pl = self.events[-1]
                pl["fromSlide"] = True
            self._outcome(EXECUTED)
            return
        # Airborne, FastFalling, Falling with Y >= 0: buffer (6.2.5, I1, I2)
        if self.buffered_jump_tick is not None:
            self._outcome(SUPERSEDED)  # [MODEL-CHOICE] older buffered jump replaced by newer
        self.buffered_jump_tick = self.tick
        self.buffered_entered += 1

    def _slide_command(self):
        c = self.cfg
        s = self.state
        if s == DEAD or self.y < 0:
            self._outcome(IGNORED)
            return
        if s == RUNNING:
            self.state = SLIDING
            self.slide_left = c.slide_ticks
            self._emit("SlideStarted", restart=False)
            self._outcome(EXECUTED)
        elif s == SLIDING:
            self.slide_left = c.slide_ticks
            self._emit("SlideStarted", restart=True)
            self._outcome(EXECUTED)
        elif s in (AIRBORNE, COYOTE):
            y0 = self.y
            self.ff_y0 = y0
            self.ff_v = max(c.fast_fall_min_speed_mps, y0 / (c.fast_fall_max_ms / 1000.0))
            self.ff_t0 = self.tick
            if s == COYOTE:
                self.air_t0 = self.tick
            self.state = FAST_FALLING
            self.slide_on_land = True
            self._emit("FastFallStarted")
            self._outcome(EXECUTED)
        elif s == FAST_FALLING:
            self._outcome(ALREADY_ACTIVE)
        else:
            # Falling with Y >= 0 (only the tick coyote runs out): spec silent.
            self._outcome(IGNORED)  # [MODEL-CHOICE]

    def _land(self, was_fast_fall):
        c = self.cfg
        self.y = 0.0
        self._emit("Landed", airTicks=self.tick - self.air_t0, wasFastFall=was_fast_fall)
        slide_pending = self.slide_on_land
        self.slide_on_land = False
        if self.buffered_jump_tick is not None:
            self.buffered_jump_tick = None
            self._outcome(EXECUTED)
            self.state = RUNNING
            self._start_jump(buffered=True)
        elif slide_pending:
            self.state = SLIDING
            self.slide_left = c.slide_ticks
            self._emit("SlideStarted", restart=False)
        else:
            self.state = RUNNING

    def _start_fall(self, t0, y0, v0):
        self.state = FALLING
        self.fall_t0 = t0
        self.fall_y0 = y0
        self.fall_v0 = v0

    def _die(self, cause, archetype=None, obstacle_id=None, after_stumble=False, entry=None):
        self.dead = True
        self.state = DEAD
        self.move = None
        self.queued_lateral = 0
        if self.buffered_jump_tick is not None:
            self.buffered_jump_tick = None
            self._outcome(INVALIDATED)
        self.death = dict(cause=cause, archetype=archetype, obstacle_id=obstacle_id,
                          after_stumble=after_stumble, entry=entry, tick=self.tick,
                          x=self.x, y=self.y, z=self.z, target_lane=self.target_lane)
        self._emit("Died", cause=cause, archetype=archetype, obstacleId=obstacle_id,
                   afterStumble=after_stumble)

    def _vertical_motion(self):
        c = self.cfg
        t = self.tick
        s = self.state
        g = c.gravity_mps2
        if s == AIRBORNE:
            n = t - self.jump_start
            if n == c.jump_airtime_ticks // 2:
                self._emit("JumpApex")
            if n >= c.jump_airtime_ticks:
                self.y = 0.0
                if self._has_ground():
                    self._land(False)
                else:
                    # 6.2.4 / 6.5.3: continue the same parabola below 0
                    self._start_fall(self.jump_start, 0.0, c.jump_velocity_mps)
            else:
                tt = n / 60.0
                self.y = c.jump_velocity_mps * tt - 0.5 * g * tt * tt
        elif s == FAST_FALLING:
            k = t - self.ff_t0 + 1  # the start tick already drops (6.3.2 "on that tick")
            y = self.ff_y0 - self.ff_v * k / 60.0
            if y <= 1e-9:
                if self._has_ground():
                    self._land(True)
                else:
                    # 6.3.6: falls into the gap [MODEL-CHOICE: gravity with initial -v]
                    self.y = y
                    self._start_fall(t, y, -self.ff_v)
            else:
                self.y = y
        elif s in (RUNNING, SLIDING):
            if not self._has_ground():
                if s == SLIDING:
                    self._emit("SlideEnded", reason="Ledge")
                    self.slide_left = 0
                self.state = COYOTE
                self.coyote_left = c.coyote_ticks
                self.air_t0 = t
                self._emit("LeftGround", coyote=True)
        elif s == COYOTE:
            if self._has_ground():
                self.state = RUNNING
            else:
                self.coyote_left -= 1
                if self.coyote_left <= 0:
                    self._start_fall(t, 0.0, 0.0)
        elif s == FALLING:
            tt = (t - self.fall_t0) / 60.0
            self.y = self.fall_y0 + self.fall_v0 * tt - 0.5 * g * tt * tt
            if self.y <= -c.fall_death_depth_m + 1e-12:
                self._die("Fell")

    # ------------------------------------------------------------------ collisions
    def _hero_box(self, x, y, z, h):
        c = self.cfg
        hw = c.player_hitbox_width_m / 2.0
        hd = c.player_hitbox_depth_m / 2.0
        return (x - hw, x + hw, y, y + h, z - hd, z + hd)

    def _standing_box_blocked(self, z_prev):
        """6.4.5. [MODEL-CHOICE] checks the standing box over this tick's whole z sweep, so the
        swept collision test of step 11 cannot see the stand-up as a contact."""
        c = self.cfg
        b = list(self._hero_box(self.x, self.y, self.z, c.standing_height_m))
        b[4] = min(b[4], z_prev - c.player_hitbox_depth_m / 2.0)
        for o in self._nearby():
            if o.id in self.ignored_obstacles:
                continue
            if b[0] < o.x1 and b[1] > o.x0 and b[2] < o.y1 and b[3] > o.y0 and b[4] < o.z1 and b[5] > o.z0:
                return True
        return False

    def _nearby(self):
        obs = self.obstacles
        hd = self.cfg.player_hitbox_depth_m / 2.0
        zmin = self.z - hd - 2.0
        i = self._obs_lo
        while i < len(obs) and obs[i].z1 + 5.0 < zmin:
            i += 1
        self._obs_lo = i
        zmax = self.z + hd + 1.0
        res = []
        while i < len(obs) and obs[i].z0 <= zmax:
            res.append(obs[i])
            i += 1
        return res

    def _moving_away_from(self, lane):
        m = self.move
        if m is None or m.is_bounce:
            return False
        cl = self.cfg.lane_center(lane)
        return (self.x - cl) * m.dir > 0

    def _collisions(self, x_prev, y_prev, z_prev):
        c = self.cfg
        h = self.hitbox_height()
        hw = c.player_hitbox_width_m / 2.0
        hd = c.player_hitbox_depth_m / 2.0
        for o in self._nearby():
            if o.id in self.ignored_obstacles:
                continue
            # 9.5 edge forgiveness
            if self._moving_away_from(o.lane) and abs(self.x - c.lane_center(o.lane)) >= c.edge_forgiveness_m - 1e-12:
                continue
            ix = _axis_interval(x_prev - hw, self.x - hw, x_prev + hw, self.x + hw, o.x0, o.x1)
            if ix is None:
                continue
            iy = _axis_interval(y_prev, self.y, y_prev + h, self.y + h, o.y0, o.y1)
            if iy is None:
                continue
            iz = _axis_interval(z_prev - hd, self.z - hd, z_prev + hd, self.z + hd, o.z0, o.z1)
            if iz is None:
                continue
            lo = max(ix[0], iy[0], iz[0])
            hi = min(ix[1], iy[1], iz[1])
            if lo >= hi - 1e-12:
                continue
            entering = []
            for name, iv in (("x", ix), ("y", iy), ("z", iz)):
                if not iv[2] and abs(iv[0] - lo) <= 1e-9:
                    entering.append(name)
            if not entering:
                entry = ENTRY_INSIDE
            elif len(entering) > 1:
                entry = ENTRY_TIE
            elif entering[0] == "z":
                entry = ENTRY_FRONT
            elif entering[0] == "x":
                entry = ENTRY_SIDE
            else:
                entry = ENTRY_BELOW if self.y > y_prev else ENTRY_TOP
            self._contact(o, entry, entering)
            if self.dead:
                return

    def _contact(self, o, entry, entering):
        c = self.cfg
        lethal = entry in (ENTRY_FRONT, ENTRY_BELOW, ENTRY_INSIDE)
        if lethal:
            if self._moving_away_from(o.lane) and abs(self.x - c.lane_center(o.lane)) >= c.edge_forgiveness_m:
                self.max_lethal_old_lane += 1
            self._die("Hit", o.archetype, o.id, after_stumble=False, entry=entry)
            return
        # stumble (9.4)
        if self.daze_left > 0:
            self._die("Hit", o.archetype, o.id, after_stumble=True, entry=entry)
            return
        kind = "side" if (entry == ENTRY_SIDE or (entry == ENTRY_TIE and "x" in entering)) else "top"
        self.ignored_obstacles.add(o.id)
        self.stumbles.append(dict(tick=self.tick, kind=kind, entry=entry, obstacle_id=o.id, archetype=o.archetype))
        self._emit("Stumbled", kind=kind, obstacleId=o.id, archetype=o.archetype)
        self.daze_left = c.stumble_daze_ticks
        if kind == "side":
            m = self.move
            if m is not None:
                origin = m.end_lane - m.dir
            else:
                origin = self.occupied_lane()  # [MODEL-CHOICE] no move active (movers, later)
            self.queued_lateral = 0
            self.move = LaneMove(self.x, origin, c.lane_center(origin), c.stumble_bounce_ticks,
                                 -(m.dir if m is not None else 0), is_bounce=True)
            self.target_lane = origin


def speed_curve(rows, distance):
    """3.2: linear between rows, first speed below the first row, cap after the last."""
    if distance <= rows[0][0]:
        return rows[0][1]
    for i in range(1, len(rows)):
        d1, s1 = rows[i]
        if distance <= d1:
            d0, s0 = rows[i - 1]
            return s0 + (s1 - s0) * (distance - d0) / (d1 - d0)
    return rows[-1][1]


def commands_from_names(names):
    v = 0
    for n in names:
        v |= NAME_TO_COMMAND[n]
    return v


def names_from_commands(v):
    return [COMMAND_NAMES[f] for f in sorted(COMMAND_NAMES) if v & f]
