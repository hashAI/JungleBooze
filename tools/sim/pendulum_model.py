"""Reference model of spec 004 (fixed-pivot swing). Task T401.

Implements docs/specs/004-fixed-pivot-swing.md sections 3, 4.2 to 4.8 exactly as written:

  - fixed pivot (sP, lane x, H = 3 + 14 = 17 m), rope L = 14 m, swing gravity 22 m/s^2
  - 60 Hz symplectic Euler (omega first, then theta with the new omega)
  - DeterministicMath.Sin / Cos as polynomials: odd Taylor to x^11 (sin), even Taylor to x^12 (cos) [MODEL-CHOICE:
    the spec only names Sin; the cos polynomial is the matching even Taylor series], Horner form in x^2
  - catch: vC = clamp(vIn, 13, 16), omega0 = vC / L, theta0 = asin(clamp((z - sP) / L, +-sin 10 deg))
  - windows in ticks (Good 27, Perfect 42..52, Good again 53..apex, Poor = auto release at the first omega <= 0)
  - release: vx = vt cos(theta) + I cos(30 deg), vy = vt sin(theta) + I sin(30 deg), floors 6 and 2, flight gravity 16
  - chain: guided release with fixed gravity 16, T clamped to [0.85, 1.5] s, arrival feet 1.5 m
  - ground takeoff window: driven through runner_model.Runner (spec 001 movement), not re-derived

Every function takes a numeric type `F` (float = float64, numpy.float32 = what a C# `float` sim would do), so the
determinism risk of float32 vs float64 can be measured with the same code.

Tick conventions [MODEL-CHOICE, the spec is silent; pinned here and in the tests]:
  - swing tick k = ticks since the grab tick. The grab tick is k = 0: it sets (theta0, omega0) and integrates nothing.
  - a swipe processed at swing tick k >= 27 releases with the state after k-1 integrations (step 6 runs before the
    swing update), hero position at the end of tick k = state position + vx * dt.
  - the auto release (Poor) happens at tick k_a = the first k whose integration gives omega <= 0 (or SwingMaxTicks 96),
    with the state after k_a integrations. A swipe on tick k_a wins (spec section 7, "swipe on the apex tick").
  - flight time t is counted from the instant of the release state: anchor tick a = k - 1 for a swipe, a = k_a for auto.
"""

import math

try:
    import numpy as np
except ImportError:  # float32 comparison needs numpy; everything else runs without it
    np = None

from runner_model import ms_to_ticks

DT_HZ = 60


# ---------------------------------------------------------------------------------------------- parameters
class Params(object):
    """Spec 004 section 5 numbers. Every number is a field so a what-if is `Params(rope_length_m=...)`."""

    def __init__(self, **overrides):
        self.rope_length_m = 14.0
        self.grab_point_height_m = 3.0
        self.swing_gravity_mps2 = 22.0
        self.catch_min_speed_mps = 13.0
        self.catch_max_speed_mps = 16.0
        self.max_swing_angle_deg = 62.0
        self.grab_max_angle_deg = 10.0
        self.hand_to_feet_m = 1.75
        self.grab_blend_ms = 100.0
        self.swing_max_ms = 1600.0
        self.good_start_ms = 450.0
        self.perfect_start_ms = 700.0
        self.perfect_width_ms = 183.0
        self.release_buffer_ms = 150.0
        self.launch_gravity_mps2 = 16.0
        self.perfect_impulse_mps = 3.0
        self.good_impulse_mps = 1.5
        self.poor_impulse_mps = 0.0
        self.impulse_angle_deg = 30.0
        self.release_min_forward_mps = 6.0
        self.release_min_up_mps = 2.0
        self.chain_arrive_feet_m = 1.5
        self.chain_flight_min_s = 0.85
        self.chain_flight_max_s = 1.5
        self.chain_spacing_m = 18.0
        self.grab_earliness_ms = 550.0
        # chasm geometry (section 4.6)
        self.chasm_rim_before_pivot_m = 4.0
        self.chasm_length_m = 16.0
        # grab zone (4.1): +-1.0 m about Z plus the hitbox half depth 0.25 (hitbox depth 0.5)
        self.zone_half_m = 1.0 + 0.25
        self.grab_max_feet_y_m = 3.6
        for k, v in overrides.items():
            if not hasattr(self, k):
                raise AttributeError("unknown parameter " + k)
            setattr(self, k, v)

    # derived
    @property
    def pivot_height_m(self):
        return self.grab_point_height_m + self.rope_length_m

    @property
    def far_edge_m(self):
        """Far edge of the chasm relative to the pivot."""
        return self.chasm_length_m - self.chasm_rim_before_pivot_m

    @property
    def good_start_tick(self):
        return ms_to_ticks(self.good_start_ms)

    @property
    def perfect_start_tick(self):
        return ms_to_ticks(self.perfect_start_ms)

    @property
    def perfect_end_tick(self):
        """Exclusive."""
        return self.perfect_start_tick + ms_to_ticks(self.perfect_width_ms)

    @property
    def release_buffer_ticks(self):
        return ms_to_ticks(self.release_buffer_ms)

    @property
    def swing_max_ticks(self):
        return ms_to_ticks(self.swing_max_ms)

    @property
    def grab_blend_ticks(self):
        return ms_to_ticks(self.grab_blend_ms)

    @property
    def earliness_ticks(self):
        return ms_to_ticks(self.grab_earliness_ms)

    def impulse(self, grade):
        return {"Perfect": self.perfect_impulse_mps, "Good": self.good_impulse_mps,
                "Poor": self.poor_impulse_mps}[grade]


DEFAULT = Params()


# ---------------------------------------------------------------------------------------------- deterministic math
def det_sin(x, F=float):
    """Odd Taylor polynomial to x^11 (Horner in x^2). Valid for |x| <= 1.3."""
    x = F(x)
    x2 = x * x
    return x * (F(1.0) + x2 * (F(-1.0 / 6.0) + x2 * (F(1.0 / 120.0) + x2 * (F(-1.0 / 5040.0) + x2 * (
        F(1.0 / 362880.0) + x2 * F(-1.0 / 39916800.0))))))


def det_cos(x, F=float):
    """Even Taylor polynomial to x^12 (Horner in x^2). Valid for |x| <= 1.3."""
    x = F(x)
    x2 = x * x
    return F(1.0) + x2 * (F(-0.5) + x2 * (F(1.0 / 24.0) + x2 * (F(-1.0 / 720.0) + x2 * (
        F(1.0 / 40320.0) + x2 * (F(-1.0 / 3628800.0) + x2 * F(1.0 / 479001600.0))))))


def poly_error_report(F=float, n=26001, lim=1.3):
    """Max abs error of det_sin / det_cos vs math.sin / math.cos on [-lim, lim] (float64 reference)."""
    es = ec = 0.0
    for i in range(n):
        x = -lim + 2.0 * lim * i / (n - 1)
        xf = float(F(x))  # evaluate at the representable argument
        es = max(es, abs(float(det_sin(F(x), F)) - math.sin(xf)))
        ec = max(ec, abs(float(det_cos(F(x), F)) - math.cos(xf)))
    return es, ec


def _sqrt(x, F):
    return F(math.sqrt(x)) if F is float else np.sqrt(x)


# ---------------------------------------------------------------------------------------------- swing core
def catch_speed(v_in, p=DEFAULT):
    return min(max(v_in, p.catch_min_speed_mps), p.catch_max_speed_mps)


def start_angle(z_rel, p=DEFAULT):
    """theta0 = asin(clamp(z_rel / L, +-sin 10 deg)). math.asin: DeterministicMath has no asin, see report."""
    lim = math.sin(math.radians(p.grab_max_angle_deg))
    a = min(max(z_rel / p.rope_length_m, -lim), lim)
    return math.asin(a)


class SwingTrace(object):
    """States of an unreleased swing. theta[n], omega[n] = state after n integrations (n = 0 is the grab tick).
    Runs until the first omega <= 0 (k_apex) or swing_max_ticks."""

    def __init__(self, v_in, z_rel, p=DEFAULT, F=float, max_ticks=None):
        self.p = p
        self.F = F
        L = F(p.rope_length_m)
        g_over_l = F(p.swing_gravity_mps2) / L
        dt = F(1.0) / F(DT_HZ)
        theta = F(start_angle(z_rel, p))
        omega = F(catch_speed(v_in, p)) / L
        self.v_c = catch_speed(v_in, p)
        self.theta = [theta]
        self.omega = [omega]
        limit = p.swing_max_ticks if max_ticks is None else max_ticks
        self.k_apex = None
        for n in range(1, limit + 1):
            omega = omega - g_over_l * det_sin(theta, F) * dt
            theta = theta + omega * dt
            self.theta.append(theta)
            self.omega.append(omega)
            if omega <= 0:
                self.k_apex = n
                break
        if self.k_apex is None:
            self.k_apex = limit  # failsafe auto release at SwingMaxMs

    def hand_z(self, n):
        return self.p.rope_length_m * float(det_sin(self.theta[n], self.F))  # relative to sP

    def hand_y(self, n):
        return self.p.pivot_height_m - self.p.rope_length_m * float(det_cos(self.theta[n], self.F))

    def feet_y(self, n):
        return self.hand_y(n) - self.p.hand_to_feet_m

    def tension(self, n):
        """Rope tension per unit mass: L omega^2 + g cos(theta). Slack when < 0."""
        p, F = self.p, self.F
        return float(F(p.rope_length_m) * self.omega[n] * self.omega[n]
                     + F(p.swing_gravity_mps2) * det_cos(self.theta[n], F))

    def energy(self, n):
        """Per-unit-mass energy 0.5 L^2 omega^2 + g L (1 - cos theta), float64 evaluation of the stored state."""
        p = self.p
        w = float(self.omega[n])
        return 0.5 * p.rope_length_m ** 2 * w * w + p.swing_gravity_mps2 * p.rope_length_m * (
            1.0 - math.cos(float(self.theta[n])))


def grade_for_tick(k, k_apex, p=DEFAULT):
    """Spec 4.4. Returns 'None' (too early), 'Good', 'Perfect' for a swipe processed at swing tick k <= k_apex."""
    if k < p.good_start_tick:
        return "None"
    if p.perfect_start_tick <= k < p.perfect_end_tick:
        return "Perfect"
    return "Good"


def resolve_swipe(swipe_tick, k_apex, p=DEFAULT):
    """Where does a swipe made at `swipe_tick` end up? Returns (release_tick, grade).
    release_tick None = no valid release from the swipe (auto/Poor at the apex)."""
    if swipe_tick is None or swipe_tick < 0:
        return None, "Poor"
    if swipe_tick >= p.good_start_tick:
        if swipe_tick <= k_apex:
            return swipe_tick, grade_for_tick(swipe_tick, k_apex, p)
        return None, "Poor"
    # too early: buffered 9 ticks (release_buffer_ticks), fires at good_start_tick if still inside the buffer
    if p.good_start_tick - swipe_tick <= p.release_buffer_ticks:
        return p.good_start_tick, "Good"
    return None, "Poor"


class Release(object):
    """Everything about one release. Positions are relative to the pivot s (sP = 0)."""
    __slots__ = ("tick", "grade", "anchor", "theta", "omega", "z0", "y0", "vx", "vy", "vx_raw", "vy_raw",
                 "t_land", "z_land", "land_tick_after_anchor", "rope_deg", "vt")


def release_at(trace, k, grade, p=DEFAULT, auto=False):
    """Release at swing tick k with `grade` ('Perfect','Good','Poor'). A swipe (auto False) uses the state after k-1
    integrations; the auto release uses the state after k integrations."""
    F = trace.F
    n = k if auto else k - 1
    theta, omega = trace.theta[n], trace.omega[n]
    vt = F(p.rope_length_m) * omega
    imp = F(p.impulse(grade))
    alpha = F(math.radians(p.impulse_angle_deg))
    vx_raw = vt * det_cos(theta, F) + imp * det_cos(alpha, F)
    vy_raw = vt * det_sin(theta, F) + imp * det_sin(alpha, F)
    vx = vx_raw if vx_raw > F(p.release_min_forward_mps) else F(p.release_min_forward_mps)
    vy = vy_raw if vy_raw > F(p.release_min_up_mps) else F(p.release_min_up_mps)
    r = Release()
    r.tick, r.grade, r.anchor = k, grade, n
    r.theta, r.omega, r.vt = float(theta), float(omega), float(vt)
    r.rope_deg = math.degrees(float(theta))
    r.z0 = trace.hand_z(n)
    r.y0 = trace.feet_y(n)
    r.vx, r.vy, r.vx_raw, r.vy_raw = float(vx), float(vy), float(vx_raw), float(vy_raw)
    g = p.launch_gravity_mps2
    r.t_land = (r.vy + math.sqrt(r.vy * r.vy + 2.0 * g * r.y0)) / g
    r.z_land = r.z0 + r.vx * r.t_land
    r.land_tick_after_anchor = int(math.ceil(r.t_land * DT_HZ - 1e-9))
    return r


def run_swing(v_in, z_rel, swipe_tick=None, p=DEFAULT, F=float):
    """Full tick logic of section 4.4 for one swing. Returns (trace, Release)."""
    tr = SwingTrace(v_in, z_rel, p, F)
    k, grade = resolve_swipe(swipe_tick, tr.k_apex, p)
    if k is None:
        return tr, release_at(tr, tr.k_apex, "Poor", p, auto=True)
    return tr, release_at(tr, k, grade, p)


# ---------------------------------------------------------------------------------------------- flight / chains
def flight_state(rel, i, p=DEFAULT):
    """Hero (z, y) relative to the pivot, i ticks after the anchor tick (free flight)."""
    t = i / float(DT_HZ)
    return rel.z0 + rel.vx * t, rel.y0 + rel.vy * t - 0.5 * p.launch_gravity_mps2 * t * t


def guide_chain(rel, dz_to_next, p=DEFAULT):
    """Section 4.8 guided release to a vine `dz_to_next` metres ahead of the pivot of the swing we release from.
    Returns (T, vx, vy)."""
    g = p.launch_gravity_mps2
    zb = dz_to_next
    T = (zb - rel.z0) / rel.vx
    T = min(max(T, p.chain_flight_min_s), p.chain_flight_max_s)
    vx = (zb - rel.z0) / T
    vy = (p.chain_arrive_feet_m - rel.y0 + 0.5 * g * T * T) / T
    return T, vx, vy


def chain_catch(rel, T, vx, vy, p=DEFAULT):
    """Run the guided flight tick by tick and find the grab tick at the next vine (pivot at p.chain_spacing_m).
    Returns dict(ok, tick, z_rel_B, y, vx) or ok False. Zone test: |z - Zb| <= zone_half, 0 < y < 3.6 (airborne)."""
    zb = p.chain_spacing_m
    g = p.launch_gravity_mps2
    for i in range(0, int(T * DT_HZ) + 12):
        t = i / float(DT_HZ)
        z = rel.z0 + vx * t
        y = rel.y0 + vy * t - 0.5 * g * t * t
        if y <= 0.0:
            return dict(ok=False, tick=i, z_rel_B=z - zb, y=y, vx=vx)
        if abs(z - zb) <= p.zone_half_m and y < p.grab_max_feet_y_m:
            return dict(ok=True, tick=i, z_rel_B=z - zb, y=y, vx=vx)
        if z - zb > p.zone_half_m:
            return dict(ok=False, tick=i, z_rel_B=z - zb, y=y, vx=vx)
    return dict(ok=False, tick=-1, z_rel_B=0.0, y=0.0, vx=vx)


# ---------------------------------------------------------------------------------------------- closed forms (spec 4.2 / 4.3)
def apex_angle_deg(v_c, p=DEFAULT):
    """Energy conservation, from the bottom (theta = 0): cos(theta_max) = 1 - vC^2 / (2 g L)."""
    c = 1.0 - v_c * v_c / (2.0 * p.swing_gravity_mps2 * p.rope_length_m)
    if c < -1.0 or c > 1.0:
        return None
    return math.degrees(math.acos(c))


def quarter_period_s(v_c, p=DEFAULT):
    """Spec 4.3 closed form for the HUD ring (series in k2 = vC^2 / (4 g L))."""
    k2 = v_c * v_c / (4.0 * p.swing_gravity_mps2 * p.rope_length_m)
    return math.sqrt(p.rope_length_m / p.swing_gravity_mps2) * (math.pi / 2.0) * (
        1.0 + k2 / 4.0 + 9.0 * k2 * k2 / 64.0 + 25.0 * k2 ** 3 / 256.0)


def slack_angle_deg(v_c, p=DEFAULT):
    """Angle (deg from the downward vertical) at which an unclamped pendulum with bottom speed v_c goes slack,
    or None if it never does (the rope stays taut up to the apex)."""
    g, L = p.swing_gravity_mps2, p.rope_length_m
    c = (2.0 * g - v_c * v_c / L) / (3.0 * g)  # tension = 0  ->  cos(theta)
    if c >= 1.0:
        return None
    if apex_angle_deg(v_c, p) is not None and math.cos(math.radians(apex_angle_deg(v_c, p))) >= 0.0:
        return None  # apex at or below the horizontal: always taut
    return math.degrees(math.acos(max(-1.0, c)))


# ---------------------------------------------------------------------------------------------- ground take-off window (4.1)
def takeoff_window(speed_mps, p=DEFAULT, earliness_ms=None, chasm=True, sP=200.0):
    """Which jump ticks give a grab? Runs spec 001 movement (runner_model.Runner) at a fixed speed over a chasm
    whose near rim is `rim_before_pivot` m before the pivot. Jump command at runner tick j (j counted from a start
    far enough before the rim). Grab = on some tick the hero is AIRBORNE with y > 0, <= earliness ticks after the jump
    tick, |z - sP| <= zone_half, y < 3.6 (lane equal by construction).
    Returns dict(ticks=sorted list of jump ticks that grab, lo, hi, n, ms, fell=jump ticks that fell without grab)."""
    from runner_model import Runner, RunnerConfig, TestTrackQuery, FlatTrackQuery, JUMP, NONE, AIRBORNE, DEAD
    ear = p.earliness_ticks if earliness_ms is None else ms_to_ticks(earliness_ms)
    cfg = RunnerConfig(fixed_speed_mps=float(speed_mps), use_start_ramp=False)
    rim = sP - p.chasm_rim_before_pivot_m
    far = sP + p.far_edge_m
    track = TestTrackQuery([(rim, far, -50.0, 50.0)]) if chasm else FlatTrackQuery()
    start_back = 4.0 + speed_mps * 2.0 + 6.0
    ok_ticks, fell = [], []
    # jump ticks to try: from far too early to far too late
    n_try = int((start_back + 8.0) / speed_mps * DT_HZ) + 60
    for j in range(0, n_try):
        r = Runner(cfg, track=track, record_events=False)
        r.z = rim - start_back
        grabbed = False
        for t in range(0, j + int(DT_HZ * 1.5)):
            r.step(JUMP if t == j else NONE)
            if r.dead:
                break
            if (r.state == AIRBORNE and r.y > 0.0 and t - j <= ear
                    and abs(r.z - sP) <= p.zone_half_m and r.y < p.grab_max_feet_y_m):
                grabbed = True
                break
            if r.z > sP + p.zone_half_m + 1.0:
                break
        if grabbed:
            ok_ticks.append(j)
        elif r.dead:
            fell.append(j)
    return dict(ticks=ok_ticks, lo=min(ok_ticks) if ok_ticks else None, hi=max(ok_ticks) if ok_ticks else None,
                n=len(ok_ticks), ms=len(ok_ticks) * 1000.0 / DT_HZ, fell=fell,
                contiguous=(not ok_ticks) or (max(ok_ticks) - min(ok_ticks) + 1 == len(ok_ticks)))
