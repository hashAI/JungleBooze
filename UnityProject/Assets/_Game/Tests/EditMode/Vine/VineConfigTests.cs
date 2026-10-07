using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 sections 5 and 11.1: the vine tuning, its derived numbers and the validator.</summary>
    public sealed class VineConfigTests
    {
        [Test]
        public void AC401_PivotHeightIsGrabPointPlusRopeLength()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(3.0f, vines.GrabPointHeightM, 1e-6f);
            Assert.AreEqual(14.0f, vines.RopeLengthM, 1e-6f);
            Assert.AreEqual(17.0f, vines.PivotHeightM, 0.001f);

            VineDesignValues values = VineDesignValues.CreateDefault();
            values.RopeLengthM = 12f;
            values.GrabPointHeightM = 2.5f;
            Assert.AreEqual(14.5f, VineConfig.FromDesignValues(values).PivotHeightM, 0.001f);
        }

        [Test]
        public void AC409_WindowsAreTheT401TicksFromMilliseconds()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(27, vines.GoodStartTick);
            Assert.AreEqual(42, vines.PerfectStartTick);
            Assert.AreEqual(53, vines.PerfectEndTick);
            Assert.AreEqual(9, vines.ReleaseBufferTicks);
            Assert.AreEqual(96, vines.SwingMaxTicks);
            Assert.AreEqual(6, vines.GrabBlendTicks);
            Assert.AreEqual(30, vines.LandingBlendTicks);
            Assert.AreEqual(85, vines.ApexTicksEstimate);
            Assert.AreEqual(0.55, vines.GrabEarlinessS, 1e-9);
        }

        [Test]
        public void AC409_GradeForSwingTick()
        {
            VineConfig vines = VineConfig.CreateDefault();
            for (int t = 0; t <= 26; t++)
            {
                Assert.AreEqual(VineReleaseGrade.None, vines.GradeForSwingTick(t), "tick " + t);
            }

            for (int t = 27; t <= 41; t++)
            {
                Assert.AreEqual(VineReleaseGrade.Good, vines.GradeForSwingTick(t), "tick " + t);
            }

            for (int t = 42; t <= 52; t++)
            {
                Assert.AreEqual(VineReleaseGrade.Perfect, vines.GradeForSwingTick(t), "tick " + t);
            }

            for (int t = 53; t <= 96; t++)
            {
                Assert.AreEqual(VineReleaseGrade.Good, vines.GradeForSwingTick(t), "tick " + t);
            }
        }

        [Test]
        public void AC404_CatchSpeedIsTheClampOfTheEntrySpeed()
        {
            VineConfig vines = VineConfig.CreateDefault();
            double[] entry = { 8, 10, 12, 13, 14, 15, 16, 18, 21, 24, 28 };
            double[] expected = { 13, 13, 13, 13, 14, 15, 16, 16, 16, 16, 16 };
            for (int i = 0; i < entry.Length; i++)
            {
                Assert.AreEqual(expected[i], vines.CatchSpeedFor(entry[i]), 1e-9, "entry " + entry[i]);
            }
        }

        [Test]
        public void StartAngle_IsAsinOfTheGrabOffsetAndClampedAtTenDegrees()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(Math.Asin(-1.25 / 14.0), vines.StartAngleFor(-1.25), 1e-8);
            Assert.AreEqual(-5.12, vines.StartAngleFor(-1.25) * 180.0 / Math.PI, 0.01);
            Assert.AreEqual(10.0 * Math.PI / 180.0, vines.StartAngleFor(50.0), 1e-8);
            Assert.AreEqual(-10.0 * Math.PI / 180.0, vines.StartAngleFor(-50.0), 1e-8);
        }

        [Test]
        public void AC415_LaunchVelocityAddsTheGradeImpulseAt30DegreesAndAppliesTheFloors()
        {
            VineConfig vines = VineConfig.CreateDefault();
            double theta = 0.3;
            double omega = 0.8;
            double vt = 14.0 * omega;
            double c30 = Math.Cos(30.0 * Math.PI / 180.0);
            double s30 = Math.Sin(30.0 * Math.PI / 180.0);

            vines.LaunchVelocity(theta, omega, VineReleaseGrade.Perfect, out double vx, out double vy);
            Assert.AreEqual((vt * Math.Cos(theta)) + (3.0 * c30), vx, 1e-6);
            Assert.AreEqual((vt * Math.Sin(theta)) + (3.0 * s30), vy, 1e-6);

            vines.LaunchVelocity(theta, omega, VineReleaseGrade.Good, out vx, out vy);
            Assert.AreEqual((vt * Math.Cos(theta)) + (1.5 * c30), vx, 1e-6);
            Assert.AreEqual((vt * Math.Sin(theta)) + (1.5 * s30), vy, 1e-6);

            vines.LaunchVelocity(theta, omega, VineReleaseGrade.Auto, out vx, out vy);
            Assert.AreEqual(vt * Math.Cos(theta), vx, 1e-6);
            Assert.AreEqual(vt * Math.Sin(theta), vy, 1e-6);

            // A release at the apex (no tangential speed) is pushed off by the floors alone.
            vines.LaunchVelocity(0.95, 0.0, VineReleaseGrade.Auto, out vx, out vy);
            Assert.AreEqual(6.0, vx, 0.0);
            Assert.AreEqual(2.0, vy, 0.0);

            // Floors hold for every grade at any state.
            VineReleaseGrade[] grades = { VineReleaseGrade.Auto, VineReleaseGrade.Good, VineReleaseGrade.Perfect };
            for (int g = 0; g < grades.Length; g++)
            {
                for (double th = -0.1; th <= 0.95; th += 0.05)
                {
                    for (double om = -0.1; om <= 1.2; om += 0.1)
                    {
                        vines.LaunchVelocity(th, om, grades[g], out vx, out vy);
                        Assert.GreaterOrEqual(vx, 6.0);
                        Assert.GreaterOrEqual(vy, 2.0);
                    }
                }
            }
        }

        [Test]
        public void Validator_AcceptsTheDefaultsAndTheT401CatchLimitAndRejectsAWiderSwing()
        {
            var errors = new List<string>();
            Assert.IsTrue(VineDesignValues.CreateDefault().Validate(errors), string.Join(" ", errors));

            VineDesignValues edge = VineDesignValues.CreateDefault();
            edge.CatchMaxSpeedMps = 17.8f;
            errors.Clear();
            Assert.IsTrue(edge.Validate(errors), string.Join(" ", errors));

            VineDesignValues wide = VineDesignValues.CreateDefault();
            wide.CatchMaxSpeedMps = 18.2f;
            errors.Clear();
            Assert.IsFalse(wide.Validate(errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("catchMaxSpeedMps")), string.Join(" ", errors));
            Assert.Throws<ArgumentException>(() => VineConfig.FromDesignValues(wide));
        }

        [Test]
        public void Validator_RejectsOutOfRangeRopeAndGravity()
        {
            VineDesignValues values = VineDesignValues.CreateDefault();
            values.RopeLengthM = 25f;
            values.SwingGravityMps2 = 3f;
            var errors = new List<string>();
            Assert.IsFalse(values.Validate(errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("ropeLengthM")));
            Assert.IsTrue(errors.Exists(e => e.Contains("swingGravityMps2")));
        }

        [Test]
        public void ComputeHash_IsStableAndChangesWithTheSwing()
        {
            Assert.AreEqual(VineConfig.CreateDefault().ComputeHash(), VineConfig.CreateDefault().ComputeHash());
            VineDesignValues values = VineDesignValues.CreateDefault();
            values.SwingGravityMps2 = 20f;
            Assert.AreNotEqual(VineConfig.CreateDefault().ComputeHash(), VineConfig.FromDesignValues(values).ComputeHash());
        }

        [Test]
        public void AssetWithoutSavedValuesLoadsTheCodeDefaults()
        {
            VineConfigAsset asset = ScriptableObject.CreateInstance<VineConfigAsset>();
            try
            {
                VineConfig fromAsset = asset.ToConfig();
                Assert.AreEqual(VineConfig.CreateDefault().ComputeHash(), fromAsset.ComputeHash());
                Assert.AreEqual(17.0f, fromAsset.PivotHeightM, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CompatibilityMembersForTheOldViewMatchThePendulum()
        {
            VineConfig vines = VineConfig.CreateDefault();
            Assert.AreEqual(vines.RopeLengthM, vines.SwingRadiusM);
            Assert.AreEqual(0.0, vines.SwingStartAngleRad);
            Assert.AreEqual(vines.ApexTicksEstimate, vines.SwingTicks);
            Assert.AreEqual(27.0 / 85.0, vines.GoodStartPhase, 1e-6);
            Assert.AreEqual(42.0 / 85.0, vines.PerfectStartPhase, 1e-6);
            Assert.AreEqual(53.0 / 85.0, vines.PerfectEndPhase, 1e-6);
        }
    }
}
