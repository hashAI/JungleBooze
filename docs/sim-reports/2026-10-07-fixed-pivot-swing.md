# Sim report: spec 004 fixed-pivot swing (T401, exact simulation)

**Date:** 2026-10-07 | **Author:** balance-simulator | **Spec:** `docs/specs/004-fixed-pivot-swing.md` | **Model:** `tools/sim/pendulum_model.py` (Python, float64 reference, float32 cross-check)

**What was run:** the spec 004 swing implemented literally (fixed pivot 17 m, rope 14 m, swing gravity 22, 60 Hz symplectic Euler with the Taylor polynomial sine/cosine, catch clamp 13..16, 6 tick blend, windows 27 / 42..52 in ticks, 30 deg impulse, floors 6 and 2, flight gravity 16, guided chains, 16 m chasm with rim 4 m before the pivot) plus the spec 001 movement model (`runner_model.py`) for the ground take-off window. Not the C# code: the C# runner does not have the swing yet. Everything below is a property of the spec as written.

**Reproduce:** `cd tools/sim && python3 -I pendulum_study.py && python3 -I pendulum_report.py` (about 30 s, pure Python plus numpy for the float32 check). Tests: `python3 -I -m pytest tools/sim/tests -q` (33 pass). Seeds: fuzz 40101 (wide 40102), energy 40102, chain 40103, bots 40104, float32 40105. Every number is deterministic from these seeds. Full per-tick table for entry speeds 8..28 step 2 and every release tick: `docs/sim-reports/data/2026-10-07-fixed-pivot-swing-tables.csv` (660 rows).

No parameter was changed. Recommendations are at the end and go to game-designer.

## Headline

The spec's hand numbers were wrong in ways that matter. The design itself works: **0 of 3,118,393 releases land in the chasm** and the worst landing is 2.86 m past the far edge. But:

1. **Apex tick is 85 (84..85), not 78..80.** The spec used the quarter period from the lowest point, but the swing starts at -5.1 deg, 5 to 6 ticks before the lowest point. Time to the apex from the grab is 1.417 s, not 1.30..1.33 s. AC-407 as written fails.
2. **The spec's Perfect landing numbers are 1.2 to 1.9 m too short and its Good-early ones about 1.2 m too long** (hand calc placed the rope at 35.6 deg at tick 47.5; the integrator has 32.0 deg). Perfect lands **6.6 to 13.6 m** past the edge (spec: 6 to 11), so the claim fails at vC 15 and 16.
3. **The 3.2 m guarantee is false: the minimum is 2.86 m** (Good, release at the first Good tick 27, vC 13). Poor is 3.32 m (spec 3.2: correct). 2.86 m is still safe; AC-413's 14.5 m floor holds with 0.36 m to spare.
4. **Energy drift of the specified integrator is 1.27 %, not 0.1 %.** It is bounded (no secular growth, checked over 6,000 ticks), so it is a tolerance fix, not a bug.
5. **Chain second swings do not always have vC = 13**: after a Good-early release the arrival speed is up to 14.78 m/s, so vC of the next swing is 13 to 14.78 (AC-419 as written fails).
6. Ground take-off windows are **1 to 3 ticks longer** than the spec's hand formula (the formula forgot the 0.25 m hitbox half depth at the rim). The ACs pass with margin.
7. The bot Perfect rates match the spec's analytic table (expert 80.9, average 38.6, new 26.9 %). Average sits only 1.4 points under the 40 % cap.

## Targets

| # | Target | Measured | Result |
|---|---|---|---|
| S-401 (AC-413) | vIn 8..28 step 0.5 x 1,000 random grab offsets, every release tick 27..apex + Poor: landing >= sP+14.5 and <= sP+30, 0 chasm landings | 2,440,839 releases (+677,554 with grabs anywhere in the zone): min 14.86 (2.86 past edge), max 25.61; 0 violations | **PASS** |
| Spec 4.6 / 6 | every valid release incl. Poor lands >= 3.2 m past the far edge | min 2.86 m (vC 13, Good, tick 27); Poor min 3.32 m | **FAIL** (restate as >= 2.8 m) |
| Spec 4.6 / 6 | Perfect lands 6 to 11 m past the edge | 6.58 to 13.61 m (per vC: 13 6.58..7.30, 14 8.24..9.34, 15 9.89..11.45, 16 11.52..13.61) | **FAIL** at vC 15, 16 |
| S-402 | tables within +-0.3 m landing, +-2 ticks apex, +-0.5 deg angle | Poor: all within 0.13 m. Good-late within 0.34 m. Angles within 0.47 deg. Perfect: 1.2 to 1.9 m off. Good-early: 1.2 to 1.3 m off. Apex tick 85 vs 78..80: 5 to 7 ticks off | **FAIL** (Perfect, Good-early, apex tick) |
| S-403 / GDD 7.6 | Perfect: expert >= 70 %, average 25..40 %; Poor <= 6 % all | expert 80.9 %, average 38.6 %, new 26.9 % (20k trials per speed; exact analytic 81.0 / 38.9 / 27.6). Poor 0.0 / 0.3 / 3.5 % | **PASS** (average margin 1.4 pt) |
| S-404 / AC-403 | energy drift <= 0.1 % in 100 ticks, 1,000 start states | max 1.27 % (bounded, no secular growth); velocity Verlet would give 0.010 % | **FAIL** (restate as <= 1.5 %) |
| S-404 / AC-406 | theta never above 62 deg for vIn 0..40; peak angle within 0.5 deg of 43.4 / 47.0 / 50.6 / 54.2 | max 54.6 deg over 1,000 states; peaks 43.87 / 47.39 / 50.96 / 54.58 deg (+0.47 .. +0.36 off) | **PASS** (marginal on 13: use the measured values) |
| AC-407 | first omega <= 0 at tick 78 / 78 / 79 / 80 (+-2) | 85 / 85 / 85 / 85 (84..85 over all normal grab offsets) | **FAIL** |
| Spec 4.3 | rope never slack for vC 13..16 | min tension 15.9 / 14.9 / 13.9 / 12.8 m/s^2 (positive; slack means < 0) | **PASS** |
| Spec 4.2 | 28 m/s without clamp: rope goes slack | yes: slack at 100.5 deg from tick 78; 24 m/s is still taut (apex 86.3 deg, min tension 1.3) | **PASS** |
| S-405 / AC-418 | chain, 10,000 trials per grade and vC: T in [0.85, 1.5], arrival feet 1.5 +-0.1, catch 100 % | 120,000 trials: 100 % catches, T 0.850..1.500 s, feet 1.500 exactly | **PASS** |
| AC-419 | second and third swing have vC = 13.000 (arrival vx < 13) | vC of the next swing is 13.00 .. 14.78 (Good-early at vC 16 arrives at 14.78) | **FAIL** |
| S-406 / AC-427 | take-off window 280 / 350 / 410 ms at 8 / 10 / 13 m/s, within +-1 tick of 286 / 355 / 418 / 499 (21 m/s) | 333 / 400 / 467 / 533 ms at 8 / 10 / 13 / 21 m/s | **PASS** (AC floors) / **FAIL** (+-1 tick of hand numbers) |
| GDD 7.6 grab | grab success: new >= 75 %, average >= 90 % (jump-timing bot) | new 96.5 %, average 99.5 %, expert 99.8 % (profile sigmas 100 / 67 / 33 ms); at 2x sigma: new 73..80 %, average 90..95 % | **PASS** (thin at 2x sigma, slow speeds) |
| AC-408 | DeterministicMath error <= 1e-7 on [-1.3, 1.3] | float64: sin 4.8e-09, cos 4.5e-10. float32: sin 1.04e-07, cos 7.74e-08 | **PASS** (float64) / **FAIL by 4 %** (float32 sin) |
| AC-435 | float vs double: x,y,z within 1e-4, theta within 1e-6 | 1,000 random swings: max d(theta) 6.0e-07, d(omega) 6.3e-07, d(z) 5.6e-06 m, d(y) 7.3e-06 m, landing 1.8e-05 m; apex tick identical in 1000/1000 | **PASS** (theta only 1.7x under 1e-6: use 5e-6) |
| AC-414 | Perfect(center) - Poor >= 2.5 m | 3.79 / 4.78 / 5.82 / 6.89 m at vC 13..16 | **PASS** |
| AC-411 | auto release no later than tick 96 | tick 85; margin 11 ticks | **PASS** |

## 1. Tables of 4.2 to 4.6, exact

Convention (pinned in the model docstring and the tests): swing tick k = ticks since the grab tick; a swipe at tick k >= 27 uses the state after k-1 integrations; the auto release uses the state after k_a integrations. Canonical grab: z = pivot - 1.25 (first tick of the zone), theta0 = -5.12 deg. A different grab offset inside the zone changes landings by at most the offset (see fuzz). Rope angle is theta at the release state.

### 1.1 Swing (spec 4.3 table)

| vC (m/s) | peak theta, simulated (spec) | peak theta, closed form from the bottom | apex tick (spec) | apex time from grab | T/4 closed form, ticks | hand height at apex (spec 'rope end') | feet height at apex |
|---|---|---|---|---|---|---|---|
| 13 | 43.87 deg (43.4) | 43.48 deg | 85 (78) | 1.417 s | 78.0 | 6.90 m (7.4) | 5.15 m |
| 14 | 47.39 deg (47.0) | 47.01 deg | 85 (78.5) | 1.417 s | 78.5 | 7.52 m (8.2) | 5.77 m |
| 15 | 50.96 deg (50.6) | 50.60 deg | 85 (79) | 1.417 s | 79.0 | 8.18 m (8.9) | 6.43 m |
| 16 | 54.58 deg (54.2) | 54.24 deg | 85 (80) | 1.417 s | 79.6 | 8.89 m (9.6) | 7.14 m |

The spec's 'apex height of the rope end above the path' (7.4 / 8.2 / 8.9 / 9.6) is 0.5 to 0.7 m higher than the geometry gives (17 - 14 cos theta). The simulated peak is 0.35 to 0.47 deg above the closed form because the swing starts 5.1 deg before the lowest point with speed vC at that point, not at the bottom.

### 1.2 Perfect (center tick 47), spec 4.6 first table

| vC | rope angle deg | grab-to-release m | release (vx, vy) m/s | feet y m | flight s | landing past pivot m | past far edge m | Perfect window range past edge |
|---|---|---|---|---|---|---|---|---|
| 13 | 32.0 | 8.7 | 10.2, 6.2 | 3.4 | 1.15 | 19.1 | **7.1** (spec 5.9) | 6.6 .. 7.3 |
| 14 | 34.7 | 9.2 | 10.5, 6.9 | 3.7 | 1.24 | 21.0 | **9.0** (spec 7.6) | 8.2 .. 9.3 |
| 15 | 37.4 | 9.7 | 10.7, 7.7 | 4.1 | 1.34 | 22.9 | **10.9** (spec 9.2) | 9.9 .. 11.5 |
| 16 | 40.1 | 10.3 | 10.9, 8.5 | 4.5 | 1.45 | 24.8 | **12.8** (spec 10.9) | 11.5 .. 13.6 |

Spec hand values at 13..16: rope 35.6 / 38.4 / 41.2 / 43.9 deg, distance 9.4 / 9.9 / 10.5 / 11.0 m, release speed (8.6, 5.8) .. (9.2, 7.9), feet 3.9 / 4.3 / 4.7 / 5.2 m, flight 1.14 / 1.24 / 1.33 / 1.43 s. Measured flight times (1.15 .. 1.45 s) agree; angle, distance, feet height and vx do not (the hand calc put the rope about 3.6 deg too far along).

### 1.3 Other grades, spec 4.6 second table (past the far edge, m)

| vC | Good-early tick 27 (spec) | Good-late tick 66 (spec) | Poor, apex (spec) | best Good (tick 41) | Perfect min..max |
|---|---|---|---|---|---|
| 13 | 2.86 (4.1) | 3.42 (3.3) | 3.32 (3.2) | 5.12 | 6.58 .. 7.30 |
| 14 | 4.81 (6.0) | 4.47 (4.2) | 4.20 (4.1) | 7.04 | 8.24 .. 9.34 |
| 15 | 6.93 (8.2) | 5.52 (5.2) | 5.05 (5.0) | 9.04 | 9.89 .. 11.45 |
| 16 | 9.20 (10.4) | 6.54 (6.2) | 5.87 (5.8) | 11.18 | 11.52 .. 13.61 |

Perfect is the **longest throw** at every vC (min Perfect > max Good by 1.46 / 1.20 / 0.85 / 0.34 m at vC 13 / 14 / 15 / 16). The spec text 'the longest throw is an earlier Good' (4.4, R404) is not true. The maximum of the whole swing is at tick 42, the first Perfect tick; landing then falls monotonically through the Perfect window (about 0.07 m per tick at vC 13, 0.21 at vC 16).

### 1.4 Landing past the far edge by release tick (canonical grab, m)

| tick | ms | grade | vC 13 | vC 14 | vC 15 | vC 16 |
|---|---|---|---|---|---|---|
| 27 | 450 | Good | 2.86 | 4.81 | 6.93 | 9.20 |
| 30 | 500 | Good | 3.67 | 5.69 | 7.86 | 10.17 |
| 33 | 550 | Good | 4.31 | 6.35 | 8.51 | 10.80 |
| 36 | 600 | Good | 4.76 | 6.78 | 8.90 | 11.12 |
| 39 | 650 | Good | 5.03 | 7.00 | 9.04 | 11.16 |
| 41 | 683 | Good | 5.12 | 7.04 | 9.02 | 11.05 |
| 42 | 700 | Perfect | 7.30 | 9.34 | 11.45 | 13.61 |
| 45 | 750 | Perfect | 7.23 | 9.18 | 11.16 | 13.16 |
| 47 | 783 | Perfect | 7.11 | 8.98 | 10.87 | 12.76 |
| 50 | 833 | Perfect | 6.83 | 8.57 | 10.32 | 12.05 |
| 52 | 867 | Perfect | 6.58 | 8.24 | 9.89 | 11.52 |
| 53 | 883 | Good | 4.42 | 5.89 | 7.36 | 8.79 |
| 56 | 933 | Good | 4.00 | 5.36 | 6.70 | 7.99 |
| 60 | 1000 | Good | 3.37 | 4.58 | 5.76 | 6.88 |
| 66 | 1100 | Good | 3.42 | 4.47 | 5.52 | 6.54 |
| 72 | 1200 | Good | 3.42 | 4.42 | 5.41 | 6.38 |
| 78 | 1300 | Good | 3.23 | 4.18 | 5.12 | 6.03 |
| 84 | 1400 | Good | 3.33 | 4.20 | 5.05 | 5.87 |
| 85 | 1417 | Good | 3.33 (Good by swipe; auto 3.32) | 4.20 (Good by swipe; auto 4.20) | 5.06 (Good by swipe; auto 5.05) | 5.88 (Good by swipe; auto 5.87) |

### 1.5 Totals the spec quotes

| Quantity | Spec | Measured |
|---|---|---|
| Grab-to-release distance, any grade, any vC | 6.7 to 12.6 m | 5.5 to 12.7 m |
| Grab-to-release distance, Perfect centre | 9.4 to 11.0 m | 8.7 to 10.3 m (whole Perfect window 8.0 to 10.9) |
| Release speed, Perfect (total) | 10.3 to 12.1 m/s | 12.0 to 13.8 m/s (centre) |
| Release speed, Good-early (total) | 12.7 to 14.4 m/s | 13.5 to 16.1 m/s |
| Highest feet height in a Perfect flight | 4.9 (vC 13) to 7.1 m (16) | 4.6 to 6.8 m |
| Whole airborne time grab to landing, Perfect | 1.93 to 2.22 s | 1.91 to 2.22 s |
| Perfect ring time after release (vy / 16) | 0.36 to 0.49 s | 0.39 to 0.53 s |
| Perfect minus Poor | 3 to 5 m | 3.8 to 6.9 m |

### 1.6 Entry speeds 8 to 28 (canonical grab)

| vIn m/s | vC | catch change | peak deg | apex tick | Good tick 27 past edge | Perfect tick 47 past edge | Poor past edge | Perfect grab-to-landing m |
|---|---|---|---|---|---|---|---|---|
| 8 | 13 | +5 | 43.9 | 85 | 2.86 | 7.11 | 3.32 | 20.4 |
| 10 | 13 | +3 | 43.9 | 85 | 2.86 | 7.11 | 3.32 | 20.4 |
| 12 | 13 | +1 | 43.9 | 85 | 2.86 | 7.11 | 3.32 | 20.4 |
| 14 | 14 | +0 | 47.4 | 85 | 4.81 | 8.98 | 4.20 | 22.2 |
| 16 | 16 | +0 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 18 | 16 | -2 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 20 | 16 | -4 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 22 | 16 | -6 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 24 | 16 | -8 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 26 | 16 | -10 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |
| 28 | 16 | -12 | 54.6 | 85 | 9.20 | 12.76 | 5.87 | 26.0 |

Only vC matters (the clamp makes every entry below 13 identical and every entry above 16 identical); the grab offset inside the zone is the only other input.

## 2. Fairness fuzz (S-401)

| Run | Releases checked | Min landing (past edge) | Max landing (past edge) | Violations | Apex ticks seen | Min rope tension |
|---|---|---|---|---|---|---|
| vIn 8..28 step 0.5, 1,000 grab offsets each in the first-tick range (seed 40101) | 2,440,839 | 14.858 (2.86) at vIn 11.0, tick 27 | 25.612 (13.61) at vIn 17.0, tick 42 | 0 | [84, 85] | 12.8 |
| same, 300 offsets each anywhere in the zone -1.25..+1.25 (seed 40102) | 677,554 | 14.860 (2.86) | 25.611 (13.61) | 0 | 73..85 | 12.8 |

Violation = landing < sP+14.5, > sP+30, or inside the chasm (footprint within 0.25 m of the far edge). The wide run shows the apex tick falls to 73 when the grab is late in the zone (a chain arrival or a late coyote catch near the pivot): the HUD estimate should use the actual start angle, or the ring should be driven by the real omega.

**Pad clearance** (chunk data of spec section 9, landing max sP+25.61): V-03/V-04 pivot 34, pad coins from 62: max landing 59.6, 2.4 m to spare. V-05 last pivot 52: max landing 77.6, coins from 78: 0.4 m to spare. V-06 last pivot 70: max landing 95.6, coins from 96: 0.4 m. 20 m clear: V-05 97.6 <= 110, V-06 115.6 <= 120. All fit; the coin starts of V-05/V-06 should move back 4 m.

## 3. Integrator, determinism

| Check | Result |
|---|---|
| Polynomial sin (x^11), cos (x^12) vs math.sin/cos on [-1.3, 1.3], float64 | max error 4.82e-09 / 4.48e-10 |
| same polynomials evaluated in float32 | max error 1.04e-07 / 7.74e-08 (the sin result is 4 percent above the 1e-7 limit; it is float32 rounding, not the polynomial) |
| asin for theta0 (not in the spec's DeterministicMath) | the spec calls asin; math.asin is platform dependent in principle. Series a + a^3/6 + 3a^5/40 + 15a^7/336 for |a| <= sin 10 deg has max error 4.5e-09 rad (tested) |
| Energy drift of symplectic Euler, 100 ticks, 1,000 start states | max 1.27 % (vC 13..16 at the canonical grab: 1.27 %, 1.25 %, 1.22 %, 1.20 %) |
| Same, 6,000 ticks in 600 tick blocks | 1.246 % .. 1.246 % per block: bounded, no secular drift |
| Velocity Verlet for comparison (not the spec) | 0.010 % over 100 ticks |
| float32 vs float64, 1,000 random swings (seed 40105) | max d(theta) 6.01e-07 rad, d(omega) 6.29e-07, d(z) 5.58e-06 m, d(y) 7.34e-06 m, Perfect landing 1.76e-05 m; apex tick identical in all |
| float32 energy drift (vC 13..16) | 1.270 %, 1.246 %, 1.225 %, 1.205 % (same as float64 to 4 digits) |

Reading: float and double agree to 6e-7 rad, far inside gameplay needs, but they are **not bit-identical**, so a replay hash (AC-408 'equal state hashes') only holds between runs of the same precision on the same arithmetic. The spec says the swing is 'in double'; keep that, or compare goldens with tolerance. Risk left open: an ARM64 JIT that fuses multiply-add would change float32 bits (not measurable here).

## 4. Rope tension and the 28 m/s claim

| Unclamped entry (m/s) | v^2/(2gL) | peak deg (closed form) | peak deg (sim) | min tension L w^2 + g cos(theta) (m/s^2) | first slack tick | slack angle |
|---|---|---|---|---|---|---|
| 8 | 0.104 | 26.3 | 26.9 | 19.62 | never | - |
| 10 | 0.162 | 33.1 | 33.6 | 18.33 | never | - |
| 13 | 0.274 | 43.5 | 43.9 | 15.86 | never | - |
| 16 | 0.416 | 54.2 | 54.6 | 12.75 | never | - |
| 18 | 0.526 | 61.7 | 62.0 | 10.32 | never | - |
| 21 | 0.716 | 73.5 | 73.8 | 6.14 | never | - |
| 24 | 0.935 | 86.3 | 86.6 | 1.31 | never | - |
| 28 | 1.273 | 105.8 | 106.2 | -6.12 | 78 | 100.5 deg |

The claim holds: 28 m/s (ratio 1.27) goes slack at 100.5 deg, tick 78. 24 m/s stays taut (86 deg, tension 1.3), 21 m/s is steep (73.5 deg). Inside the clamp range tension never goes below 12.7 m/s^2.

## 5. Chains (S-405), D = 18 m, 10,000 trials per grade and vC

| Grade | vC | caught | T s | arrival vx m/s | T clamped min / max | vC of next swing | feet y at the grab tick |
|---|---|---|---|---|---|---|---|
| Perfect | 13 | 10000 / 10000 | 1.001 .. 1.114 | 8.9 .. 11.25 | 0 / 0 | 13.00 .. 13.00 | 2.37 .. 2.86 |
| Perfect | 14 | 10000 / 10000 | 0.924 .. 1.029 | 9.1 .. 11.62 | 0 / 0 | 13.00 .. 13.00 | 2.28 .. 2.82 |
| Perfect | 15 | 10000 / 10000 | 0.856 .. 0.954 | 9.2 .. 11.95 | 0 / 0 | 13.00 .. 13.00 | 2.27 .. 2.83 |
| Perfect | 16 | 10000 / 10000 | 0.850 .. 0.889 | 9.3 .. 11.44 | 6881 / 0 | 13.00 .. 13.00 | 2.39 .. 2.85 |
| Good | 13 | 10000 / 10000 | 1.077 .. 1.500 | 6.0 .. 12.75 | 0 / 799 | 13.00 .. 13.00 | 2.29 .. 3.60 |
| Good | 14 | 10000 / 10000 | 0.991 .. 1.431 | 6.0 .. 13.46 | 0 / 0 | 13.00 .. 13.46 | 2.11 .. 3.60 |
| Good | 15 | 10000 / 10000 | 0.916 .. 1.335 | 6.0 .. 14.14 | 0 / 0 | 13.00 .. 14.14 | 2.13 .. 3.60 |
| Good | 16 | 10000 / 10000 | 0.850 .. 1.246 | 6.0 .. 14.78 | 335 / 0 | 13.00 .. 14.78 | 2.00 .. 3.60 |
| Poor | 13 | 10000 / 10000 | 1.383 .. 1.387 | 6.0 .. 6.00 | 0 / 0 | 13.00 .. 13.00 | 3.41 .. 3.60 |
| Poor | 14 | 10000 / 10000 | 1.283 .. 1.286 | 6.0 .. 6.00 | 0 / 0 | 13.00 .. 13.00 | 3.54 .. 3.58 |
| Poor | 15 | 10000 / 10000 | 1.188 .. 1.191 | 6.0 .. 6.00 | 0 / 0 | 13.00 .. 13.00 | 3.42 .. 3.45 |
| Poor | 16 | 10000 / 10000 | 1.099 .. 1.101 | 6.0 .. 6.00 | 0 / 0 | 13.00 .. 13.00 | 3.41 .. 3.60 |

Arrival feet height at the vine z is 1.500 m in every trial (guided by construction). The release-grade chains need no change. Notes: (a) Perfect at vC 16 hits the 0.85 s lower clamp in 69 % of trials, so the ChainFlightMinS value is active; (b) Poor chains are high lobs: guided T 1.10..1.38 s, vy 3.7..8.4 m/s, apex 7.3..7.6 m (spec said 8 m); (c) they cross the 3.6 m grab-height limit late: the first grab tick is at z = vine - 0.93 .. -1.00 with y just under 3.6 m, so the height rule, not the 2.5 m zone length, decides the grab tick (the zone still leaves 2.2 m after it); (d) vC of the next swing is 13 only after Perfect and Poor; after Good it is up to 14.78 (AC-419).

## 6. Take-off window (S-406), GrabEarlinessMs 550 vs 450

Model: spec 001 `Runner` at constant speed over the 16 m chasm, a Jump command on each candidate tick; success = AIRBORNE with y > 0 inside the zone (|z - sP| <= 1.25, y < 3.6), at most 33 ticks (550 ms) after the take-off tick. All windows are contiguous.

| Speed m/s | Window with 550 (ticks / ms) | Window with 450 (ms) | Spec 004 4.1 formula (ms) | Model minus formula (ticks) |
|---|---|---|---|---|
| 8 | 20 / 333 | 233 | 286 | +2.8 |
| 10 | 24 / 400 | 300 | 355 | +2.7 |
| 12 | 27 / 450 | 350 | 401 | +2.9 |
| 14 | 28 / 467 | 367 | 434 | +2.0 |
| 16 | 30 / 500 | 400 | 458 | +2.5 |
| 18 | 31 / 517 | 417 | 477 | +2.4 |
| 20 | 31 / 517 | 417 | 492 | +1.4 |
| 22 | 32 / 533 | 433 | 505 | +1.7 |
| 24 | 33 / 550 | 450 | 515 | +2.1 |
| 26 | 33 / 550 | 450 | 524 | +1.5 |
| 28 | 33 / 550 | 450 | 532 | +1.1 |

Why the model is longer: the late bound is the coyote jump. The footprint leaves the ground when z - 0.25 >= rim, then 5 ticks of coyote time, so the last take-off is at rim + 0.25 + 0.0833 v, not rim + 0.08 v. The early bound matches the spec (sP - 1.25 - 0.55 v). From 24 m/s the window is capped by the 33 tick earliness rule (550 ms). At 450 the window is 233 to 300 ms for 8 to 10 m/s, shorter than the old 355 ms, so 550 is needed (R405 confirmed).

## 7. Fairness bots (S-403 and GDD 7.6)

Release timing: swipe tick = round(N(783 ms, sigma) x 0.06), centred on the Perfect window (tick 47); sigma 70 / 180 / 260 ms. No retry after an early swipe (<= tick 17 expires). Jump timing: take-off tick = window centre + round(N(0, s)), s = the existing `bots.py` profile sigma (33 / 67 / 100 ms), plus the existing decision error rate (0.2 / 0.5 / 2 %) as a no-grab. Six speeds (12, 13.5, 15.5, 17.5, 19, 21 m/s) x 20,000 trials per profile = 120,000 trials per profile. Miss = no grab on a chasm vine (a Missed vine death; no retry modelled, so pessimistic).

| Profile | release / jump sigma | Perfect | Good | Poor | Miss (no grab) | given a grab: P / G / Poor | exact analytic (release only) P / G / Poor |
|---|---|---|---|---|---|---|---|
| expert | 70 / 33 ms | 80.9 % | 18.9 % | 0.0 % | 0.2 % | 81.0 % / 19.0 % / 0.0 % | 81.0 % / 19.0 % / 0.0 % |
| average | 180 / 67 ms | 38.6 % | 60.6 % | 0.3 % | 0.5 % | 38.8 % / 60.9 % / 0.3 % | 38.9 % / 60.7 % / 0.3 % |
| new | 260 / 100 ms | 26.9 % | 66.1 % | 3.5 % | 3.5 % | 27.9 % / 68.5 % / 3.7 % | 27.6 % / 68.8 % / 3.6 % |

Targets: expert Perfect >= 70 % (**PASS**, 80.9 vs exact 81.0); average 25..40 % (**PASS**, 38.6; given a grab 38.8; exact 38.9); Poor <= 6 % (**PASS**, new 3.5 %). Grab success: expert 99.8 %, average 99.5 %, new 96.5 % vs targets average >= 90 %, new >= 75 % (**PASS**). Per-speed grab success, new: 12.0 m/s 95.7 %, 13.5 m/s 96.0 %, 15.5 m/s 96.4 %, 17.5 m/s 97.0 %, 19.0 m/s 96.9 %, 21.0 m/s 97.1 %.

**Sensitivity (the jump sigma doubled, 67 -> 134 ms for average, 100 -> 200 for new):** average grab 92.9 % (target 90), new grab 76.6 % (target 75), per speed for new 12.0: 72.7 %, 13.5: 74.6 %, 15.5: 75.6 %, 17.5: 78.6 %, 19.0: 78.3 %, 21.0: 79.9 %. Pooled over speeds both still pass, but new fails at the slow speeds (12 and 13.5 m/s: 72.7 and 74.6 %) and average is at 90.1 % at 12 m/s. The grab targets therefore pass only while the profile sigmas are right and are thinnest at 12 m/s (a 450 ms window). This is why GrabEarlinessMs must not go below 550.

**Perfect share (exact) vs release sigma and Perfect width:**

| Perfect width | sigma 70 | sigma 100 | sigma 120 | sigma 150 | sigma 180 | sigma 220 | sigma 260 |
|---|---|---|---|---|---|---|---|
| 9 ticks (150 ms) | 71.6 % | 54.7 % | 46.8 % | 38.3 % | 32.3 % | 26.7 % | 22.7 % |
| 11 ticks (183 ms) | 81.0 % | 64.1 % | 55.5 % | 45.9 % | 38.9 % | 32.3 % | 27.6 % |
| 13 ticks (217 ms) | 87.8 % | 72.1 % | 63.3 % | 53.0 % | 45.3 % | 37.8 % | 32.3 % |

Average (180 ms) is 38.9 % with the spec's 11 ticks. If real average players are 150 ms, Perfect becomes 45.9 % (over the 40 % cap), and the spec's remedy of 9 ticks gives 38.3 % at 150 ms but then expert (70 ms) is 71.6 %, just above the 70 % floor. 11 ticks is correct for sigma 180; 9 is correct for sigma 150. Both pairs fit the GDD; the choice depends on the real average player (T406/playtest), not on this model.

## 8. Cost of a swing in distance (S-407, new model only)

| Run speed | Grade | Grab to landing s | Distance covered m | Distance a runner at that speed would cover m | Loss m | Loss s |
|---|---|---|---|---|---|---|
| 10.0 | Perfect | 1.91 | 20.4 | 19.1 | -1.2 | -0.12 |
| 10.0 | Poor | 2.35 | 16.6 | 23.5 | 7.0 | 0.70 |
| 15.0 | Perfect | 2.11 | 24.1 | 31.7 | 7.5 | 0.50 |
| 15.0 | Poor | 2.45 | 18.3 | 36.7 | 18.4 | 1.23 |
| 21.0 | Perfect | 2.22 | 26.0 | 46.5 | 20.5 | 0.98 |
| 21.0 | Poor | 2.49 | 19.1 | 52.4 | 33.3 | 1.58 |

Spec 4.7 'about 22 m, about 1 s at 21 m/s' holds for Perfect (20.5 m, 0.98 s). Poor at 21 m/s costs 33 m, 1.6 s. At 10 m/s a Perfect swing gains 1.2 m (the clamp pulls the runner up to 13 m/s). The old model kept the run speed (distance v x 2.35 s), so there is no comparable loss in it; whether 1.6 s of lost progress for Poor at top speed is acceptable is a score-design question for game-designer.

## 9. What-if (not applied)

| Candidate change | Min landing past edge | Perfect min .. max | min (Perfect - best Good) at one vC |
|---|---|---|---|
| baseline (spec 004) | 2.86 | 6.58 .. 13.61 | 0.34 |
| CatchMaxSpeedMps 15 | 2.86 | 6.58 .. 11.45 | 0.85 |
| PerfectImpulseMps 2.0 | 2.86 | 5.20 .. 11.82 | -1.33 |
| GoodStartMs 500 | 3.23 | 6.58 .. 13.61 | 0.34 |
| GoodStartMs 500 + PerfectImpulseMps 2.0 | 3.23 | 5.20 .. 11.82 | -1.33 |
| CatchMaxSpeedMps 15 + GoodStartMs 500 | 3.23 | 6.58 .. 11.45 | 0.85 |

`GoodStartMs 500` (tick 30) lifts the minimum to 3.23 m (the worst case moves to tick 78 at vC 13, so a '3.2 m' guarantee would then be true). `CatchMaxSpeedMps 15` caps Perfect at 11.45 m (10.9 at the centre; tick 42 is still just over 11) and widens the Perfect-over-Good ladder to 0.85 m. `PerfectImpulseMps 2.0` is rejected: the best Good (tick 41, impulse 1.5) would then out-throw the Perfect window (ladder -1.33 m). None is needed for safety; they are feel/presentation calls for game-designer.

## 10. Spec numbers to change, with the recommended value

Section numbers refer to `docs/specs/004-fixed-pivot-swing.md`. 'Pure number' = replace a hand number with the exact one, no gameplay change.

| Spec place | Spec says | Exact value | Kind |
|---|---|---|---|
| 4.3 table, 4.4 text, AC-407, feel target 'swing to the apex' | Apex tick 78 / 78.5 / 79 / 80; 'about 1,300 ms'; 1.30 to 1.33 s | **85 (84..85), 1,417 ms** from the grab; 1.30 to 1.33 s is the time from the lowest point. HUD `SwingPhase` = tick / 85. SwingMaxMs 1600 (96 ticks) still leaves 11 ticks | Pure number. The ring would end early with 78. |
| 4.3 table | Peak 43.4 / 47.0 / 50.6 / 54.2 deg; apex height 7.4 / 8.2 / 8.9 / 9.6 m | **43.9 / 47.4 / 51.0 / 54.6 deg; rope end 6.9 / 7.5 / 8.2 / 8.9 m** (feet 5.2 / 5.8 / 6.4 / 7.1 m); AC-406 tolerance +-0.1 deg around these | Pure number. |
| 4.4 table (angles at vC 14), 4.3 'Perfect = 34 to 41 deg' | Good 24..34, Perfect 34..41, late 41..47 deg | Good 19.3..30.6, **Perfect 31.3..37.7**, late 38.3..47.4 deg (vC 14) | Pure number. |
| 4.6 first table | Perfect landing past edge 5.9 / 7.6 / 9.2 / 10.9; distance 9.4..11.0; feet 3.9..5.2 | **7.1 / 9.0 / 10.9 / 12.8**; distance 8.7 / 9.2 / 9.8 / 10.3; feet 3.4 / 3.7 / 4.1 / 4.5; release (vx, vy) (10.2, 6.2) / (10.5, 6.9) / (10.7, 7.7) / (10.9, 8.5); flight 1.15 / 1.24 / 1.34 / 1.45 s | Pure number. |
| 4.6 second table | Good-early 4.1 / 6.0 / 8.2 / 10.4; Good-late 3.3 / 4.2 / 5.2 / 6.2; Poor 3.2 / 4.1 / 5.0 / 5.8 | Good-early **2.9 / 4.8 / 6.9 / 9.2**; Good-late 3.4 / 4.5 / 5.5 / 6.5; Poor 3.3 / 4.2 / 5.1 / 5.9 | Pure number. |
| 4.6 reading, section 6 'hard guarantee', R404 | Every valid release >= 3.2 m past the edge; Perfect 6 to 11; the longest throw is an earlier Good | **>= 2.85 m** (Good at tick 27, vC 13); Poor >= 3.3 m; **Perfect 6.6 to 13.6 m** and Perfect is the longest throw at every vC | Pure number. Optional design change in section 9 above. |
| AC-413 / S-401 | landing >= sP+14.5 and <= sP+30 | Keep both bounds. Measured 14.86 and 25.61 (4.4 m clear above, 0.36 m clear below) | No change. |
| AC-414 | Perfect(centre) - Poor >= 2.5 | Keep; measured 3.8 / 4.9 / 5.8 / 6.9 | No change. |
| 4.3 'Energy drift <= 0.1 %', AC-403 | 0.1 % over 100 ticks | **<= 1.5 %** (measured 1.27 % max, bounded). Keep the symplectic Euler: velocity Verlet (0.010 %) costs a second sin per tick and changes every golden, and gameplay does not need it | Tolerance. |
| 4.3 validator, 'vCmax <= 18.1' | closed form from the lowest point | The start angle adds energy: **vCmax <= 17.8** (start angle at the 10 deg clamp). At vC 18 the true peak is 62.03 deg | Pure number (config validator rule: cos(theta0max) - vC^2/(2gL) >= cos(62 deg)). |
| 4.3 'DeterministicMath ... max error 1e-7', AC-408 | Sin/Cos error <= 1e-7 | float64 5e-9 / 4e-10 passes. If the swing runs in float32: sin 1.04e-7. State the limit as **2e-7 in float32 or keep the swing state in double** (the spec already says double) | Tolerance. |
| 4.2 catch / 11.1 | theta0 = asin(...) | DeterministicMath has no asin. Add the 4-term series (error 4.5e-9 rad) `a(1 + a^2(1/6 + a^2(3/40 + a^2 15/336)))`, or compute theta0 once with `Math.Asin` and keep it out of the hash path | Missing piece. |
| AC-435 | theta within 1e-6 | **5e-6** (float vs double differ by 6e-7 over 1,000 random swings) | Tolerance. |
| 4.8, AC-419 | 'arrival vx 6 to 12.1 m/s ... every chain swing after the first has vC 13' | **Arrival vx 6.0 to 14.78 m/s; vC of the next swing 13 to 14.78** (13 after Perfect and Poor) | Pure number. |
| 4.8 chain check | Poor at vC 13: vy 8.7, apex 8 m; Perfect at vC 16: T 0.90 | Poor vC 13: vy 8.4 m/s, T 1.38 s, apex 7.4 m; Perfect vC 16: T 0.85..0.89 (69 % clamped at 0.85) | Pure number. |
| 4.1 take-off window formula and AC-427 | [sP - 1.25 - 0.55 v, rim + 0.08 v], 286 / 355 / 418 / 499 ms at 8 / 10 / 13 / 21 | **[sP - 1.25 - 0.55 v, rim + 0.25 + 0.0833 v]**; **333 / 400 / 467 / 533 ms**; capped at 550 ms from 24 m/s; AC-427 floors 280 / 350 / 410 stay valid | Pure number. GrabEarlinessMs 550 confirmed (450 gives 233 / 300 ms at 8 / 10 m/s). |
| Section 9 chunk data | V-05 pad coins from 78, V-06 from 96 | Max landing sP+25.6 gives 77.6 and 95.6: move the pad coin starts to **82** (V-05) and **100** (V-06); V-03/V-04 (62) are fine | Chunk data. |
| 4.4 'Poor' wording | automatic release on the first tick with omega <= 0 (tick 78 to 80) | tick 84 or 85 for a normal grab; down to 73 for a grab late in the zone | Pure number. |
| 6 bot table | expert 81.0 / average 38.9 / new 27.6 % Perfect | Confirmed by 120k trials: 80.9 / 38.6 / 26.9 %. New Poor 3.5 %. The spec's Poor for new (5.5 % = 2.6 late + 2.9 early) is wrong in the late part: swipes after the apex tick 85 are 0.7 %, early (<= tick 17) 2.9 %, Poor 3.6 %, Good 68.8 % | Pure number. |

## 11. Recommended parameter table for the engineer

All gameplay parameters stay at the spec 004 section 5 values: the simulation finds the design safe and the numbers below are what the C# tests must reproduce. Nothing here is a tuning change.

| Parameter | Value | Check |
|---|---|---|
| RopeLengthM / PivotHeightM | 14.0 / 17.0 (derived) | hand always on the circle (<= 2e-6 m) |
| SwingGravityMps2 | 22.0 | - |
| CatchMin / CatchMaxSpeedMps | 13.0 / 16.0 | peak 43.9..54.6 deg |
| MaxSwingAngleDeg validator | 62; vCmax <= 17.8 (start-angle aware) | - |
| GrabMaxAngleDeg / start angle | 10 / asin(clamp((z - sP)/14, +-0.17365)) | theta0 = -5.12 deg at z - sP = -1.25 |
| Integrator | symplectic Euler, omega first, 60 Hz, state in double | drift <= 1.5 % (1.27 measured) |
| DeterministicMath | sin Taylor x^11, cos Taylor x^12 (Horner in x^2), asin series 4 terms | 5e-9 / 4e-10 / 4.5e-9 |
| GoodStartMs / PerfectStartMs / PerfectWidthMs | 450 / 700 / 183 -> ticks 27 / 42 / 53 (exclusive) | - |
| ReleaseBufferMs | 150 (9 ticks) | swipe at tick 18..26 fires at 27; <= 17 expires |
| Apex / ApexTicksEstimate | 85 for vC 13..16 (84..85 at normal grabs) | SwingMaxMs 1600 = 96 ticks |
| Impulses Perfect / Good / Poor | 3.0 / 1.5 / 0.0 at 30 deg | - |
| ReleaseMinForward / ReleaseMinUp | 6.0 / 2.0 | Poor release is exactly (6.0, 2.0) |
| LaunchGravityMps2 | 16 | - |
| Chasm | 16 m, rim sP-4, far edge sP+12 | worst landing +2.86, best +13.6 |
| GrabEarlinessMs | 550 | windows 333 / 400 / 467 / 533 ms at 8 / 10 / 13 / 21 |
| Chain: spacing / T range / arrive feet | 18 / 0.85..1.5 / 1.5 | 100 % catch |
| Pad coin start V-05 / V-06 | 82 / 100 (was 78 / 96) | landing <= sP+25.6 |

## 12. Golden traces (schema v2)

`python3 -I tools/sim/pendulum_golden.py` writes (and `--check` verifies) these under `tools/sim/golden/`. Schema v2 is additive over v1 (documented in `tools/sim/README.md`); v1 files 01..06 are untouched and byte-identical.

| File | Scenario | Key expected values |
|---|---|---|
| 07_vine_perfect_release | entry 15, Perfect swipe at tick 47 | vC 15, landing 10.82 m past the far edge |
| 08_vine_poor_auto_release | entry 14, no swipe | auto release tick 85, floors (6, 2), +4.19 m |
| 09a_vine_catch_clamp_entry_8 | entry 8 -> 13 m/s, Good at tick 27 | +2.91 m (the minimum case) |
| 09b_vine_catch_clamp_entry_21 | entry 21 -> 16 m/s, Good at tick 60 | +6.71 m |
| 10_vine_chain_two | entry 21 -> Perfect at 47 -> guided to vine B 18 m on (T 0.85) -> Good at B tick 40 | B grabbed on tick 90 at vC 13, final landing z 135.15 |
| 11_vine_missed_over_chasm | spec 001 movement, jump 12 ticks after the window, no grab | falls: Died Fell on tick 57 (vine layer relabels MissedVine) |
| 12_vine_boost_entry | entry 26 (boost x1.6) -> 16 m/s, Perfect at tick 45 | identical swing to entry 21 (multiplier plays no role), +13.02 m |

Per tick: `t`, `state` (Carried / Falling / Landed), `x`, `y` (feet), `z`, `hand_y`, `swing {theta, omega}` while carried, `launch {vx, vy, y0, vxRaw, vyRaw}` on the release tick, `events`. Tolerance in each file: x, y, z 1e-4, theta and omega 5e-6.

## 13. Not verified / limits

- This is the Python model; the C# runner has no swing yet. S-408 (full-run regression) and S-407's comparison with the old model need the C# code (T406).
- The hero's lateral blend (x) is not modelled (lane center only); the y blend is in the goldens.
- No retry after a missed grab or an expired early swipe is modelled (pessimistic Miss/Poor).
- The grab-height rule `top > 1.6 m` is always true (hero top = y + 1.8 > 1.6 for y > 0); the effective height rule is `y < 3.6`. It matters for Poor chain lobs (section 5).
- 'Missed vine share of deaths <= 15 %' needs the full run bots and the obstacle course (T406).
- Git: `pendulum_model.py`, `pendulum_study.py` and the CSV were already included in a WIP commit (d7e136d) made outside this task while it was running. This task made no commit; the remaining files are uncommitted.

