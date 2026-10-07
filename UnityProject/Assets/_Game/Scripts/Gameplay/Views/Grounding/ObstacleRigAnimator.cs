using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Per-frame motion of the grounded rigs (spec 005 6.1, 8.4, 9.1): the hanging mat's sway, the thorn canes'
    /// tremble, and the barrel boulder's idle rocking, chock pop, no-slip roll, dust and settle. Visual position is the
    /// simulated position (the barrel's x is the interpolated sim x; its spin is a function of that x), so nothing can
    /// lead or trail the hitbox. Cosmetic time comes from the accumulated real frame time, never from the simulation.
    /// No allocation.
    /// </summary>
    public sealed class ObstacleRigAnimator
    {
        private readonly ObstacleGroundingTuning _t;

        public ObstacleRigAnimator(ObstacleGroundingTuning tuning)
        {
            _t = tuning;
        }

        /// <summary>Reduce Motion: fewer dust puffs, no sway.</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>Sets the sway of hanging parts. <paramref name="distanceAheadM"/> is the distance from HERO to the rig.</summary>
        public void AnimateSway(ObstacleRig rig, float clockS, float distanceAheadM, bool thorn)
        {
            float amplitude;
            float hz;
            if (thorn)
            {
                amplitude = _t.CaneSwayDeg;
                hz = _t.CaneSwayHz;
            }
            else
            {
                amplitude = GroundingMath.SwayAmplitudeDeg(distanceAheadM, _t.MatSwayDeg, _t.MatSwayNearDeg, _t.SwayFadeDistM);
                hz = _t.MatSwayHz;
            }

            if (ReduceMotion)
            {
                amplitude *= 0.25f;
            }

            for (int lane = 0; lane < rig.SwayPivot.Length; lane++)
            {
                Transform pivot = rig.SwayPivot[lane];
                if (pivot == null)
                {
                    continue;
                }

                pivot.localRotation = Quaternion.Euler(0f, 0f, GroundingMath.SwayDeg(clockS, rig.SwayPhase[lane], hz, amplitude));
            }
        }

        /// <summary>
        /// Moves the boulder rig for this frame. <paramref name="x"/> is the interpolated simulation x of the box center,
        /// <paramref name="distanceAheadM"/> the distance from HERO to the box front, <paramref name="triggerDistanceM"/>
        /// the distance at which the simulation starts the mover (speed times the trigger lead).
        /// </summary>
        public void AnimateMover(
            ObstacleRig rig, in ObstacleInstance o, float x, float clockS, float dtS, float distanceAheadM, float triggerDistanceM, float speedMps)
        {
            if (rig.BarrelRoot == null || rig.BarrelSpin == null)
            {
                return;
            }

            // Phase changes seen by the view: the chock pops and dust bursts at the trigger, the barrel sinks at the stop.
            if (o.Phase != rig.PrevPhase)
            {
                if (o.Phase == MoverPhase.Moving)
                {
                    rig.PopClockS = 0f;
                    Burst(rig, new Vector3(rig.StartX + (rig.TravelSign * 0.4f), 0.2f, 0f), _t.DustBurstCount);
                }
                else if (o.Phase == MoverPhase.Settled)
                {
                    rig.SettleClockS = 0f;
                    Burst(rig, new Vector3(rig.EndX, 0.2f, 0f), _t.DustBurstCount);
                }

                rig.PrevPhase = o.Phase;
            }

            float r = _t.RollRadiusM;
            float sinkM = 0f;
            float squash = 0f;
            float rockDeg = 0f;
            float rollDeg;
            if (o.Phase == MoverPhase.Idle)
            {
                // Rocking about the chock; it grows and leans toward the end lane just before the trigger.
                float windowM = Mathf.Max(0.01f, speedMps * _t.AnticipationS);
                float anticipation = Mathf.Clamp01(1f - ((distanceAheadM - triggerDistanceM) / windowM));
                rockDeg = GroundingMath.IdleRockDeg(
                    clockS, rig.PhaseSeed, _t.RockHz, _t.RockIdleDeg, _t.RockAnticipationDeg, anticipation, -rig.TravelSign);
                rig.SpinOffsetDeg = rockDeg;
                rollDeg = 0f;
            }
            else
            {
                // The last rocking angle fades out over 0.3 s after the trigger, so the launch has no jump.
                float fade = Mathf.Clamp01(1f - (rig.PopClockS / 0.3f));
                rollDeg = GroundingMath.RollAngleDeg(x - rig.StartX, r) + (rig.SpinOffsetDeg * fade);
            }

            if (rig.SettleClockS >= 0f)
            {
                rig.SettleClockS += dtS;
                GroundingMath.SettlePose(rig.SettleClockS, _t.SinkOnSettleM, _t.SinkTicks, out sinkM, out squash, out rockDeg);
                if (rig.SettleClockS > 0.5f)
                {
                    rig.SettleClockS = 0.5f;
                }
            }

            rig.BarrelRoot.localPosition = new Vector3(x, rig.BarrelRestY - sinkM, 0f);
            rig.BarrelRoot.localScale = new Vector3(1f, 1f - squash, 1f);
            rig.BarrelSpin.localRotation = Quaternion.Euler(0f, 0f, rollDeg + rockDeg);

            if (rig.Chock != null)
            {
                if (rig.PopClockS >= 0f)
                {
                    rig.PopClockS += dtS;
                    if (GroundingMath.ChockPopPose(rig.PopClockS, _t.ChockPopS, rig.TravelSign, out float dx, out float dy, out float spin))
                    {
                        rig.Chock.localPosition = rig.ChockRest + new Vector3(dx, dy, 0f);
                        rig.Chock.localRotation = Quaternion.Euler(0f, 0f, -spin);
                    }
                    else if (rig.Chock.gameObject.activeSelf)
                    {
                        rig.Chock.gameObject.SetActive(false);
                    }
                }
            }

            // Dust trail while it rolls.
            if (o.Phase == MoverPhase.Moving)
            {
                float rate = ReduceMotion ? _t.DustPuffsPerSReduced : _t.DustPuffsPerS;
                int due = GroundingMath.PuffsDue(ref rig.PuffAccumulator, rate, dtS);
                if (due > 0)
                {
                    Burst(rig, new Vector3(x - (rig.TravelSign * 0.3f), 0.15f, 0f), due);
                }
            }

            UpdatePuffs(rig, dtS);
        }

        private void Burst(ObstacleRig rig, Vector3 at, int count)
        {
            if (rig.Puffs.Length == 0)
            {
                return;
            }

            int n = ReduceMotion ? Mathf.Max(1, count / 2) : count;
            for (int k = 0; k < n; k++)
            {
                int slot = rig.PuffCursor % rig.Puffs.Length;
                rig.PuffCursor++;
                float u0 = GroundingMath.Unit(GroundingMath.Mix(rig.RunSeed, GroundingMath.VariantStream, rig.ObstacleId, (uint)((rig.PuffCursor * 2) + 200)));
                float u1 = GroundingMath.Unit(GroundingMath.Mix(rig.RunSeed, GroundingMath.VariantStream, rig.ObstacleId, (uint)((rig.PuffCursor * 2) + 201)));
                rig.PuffPos[slot] = at + new Vector3((u0 - 0.5f) * 0.8f, 0f, (u1 - 0.5f) * 1.2f);
                rig.PuffAge[slot] = 0f;
                rig.PuffSize[slot] = 0.4f + (0.3f * u1);
                if (!rig.Puffs[slot].gameObject.activeSelf)
                {
                    rig.Puffs[slot].gameObject.SetActive(true);
                }
            }
        }

        private void UpdatePuffs(ObstacleRig rig, float dtS)
        {
            float life = Mathf.Max(0.05f, _t.DustLifeS);
            for (int i = 0; i < rig.Puffs.Length; i++)
            {
                if (rig.PuffAge[i] < 0f)
                {
                    continue;
                }

                rig.PuffAge[i] += dtS / life;
                Transform puff = rig.Puffs[i];
                if (rig.PuffAge[i] >= 1f)
                {
                    rig.PuffAge[i] = -1f;
                    puff.gameObject.SetActive(false);
                    continue;
                }

                float age = rig.PuffAge[i];
                float s = GroundingMath.PuffScale(age, rig.PuffSize[i]);
                puff.localPosition = rig.PuffPos[i] + new Vector3(0f, 0.45f * age, 0f);
                puff.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
