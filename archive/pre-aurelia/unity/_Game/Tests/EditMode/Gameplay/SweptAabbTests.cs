using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 001 9.1 and 9.3, spec 002 5.2: swept AABB with relative motion, entry axis and category.
    /// All numbers are exact binary fractions so entry times are exact.
    /// </summary>
    public sealed class SweptAabbTests
    {
        private const double HalfWidth = 0.25;
        private const double HalfDepth = 0.25;

        private static HeroSweep Hero(double x0, double y0, double z0, double h0, double x1, double y1, double z1, double h1)
        {
            return HeroSweep.FromCenters(x0, y0, z0, h0, x1, y1, z1, h1, HalfWidth, HalfDepth);
        }

        private static ObstacleBox StaticBox(float xMin, float xMax, float yMin, float yMax, double zMin, double zMax)
        {
            return ObstacleBox.Static(1, ObstacleArchetype.FullBlock, 1, xMin, xMax, yMin, yMax, zMin, zMax);
        }

        [Test]
        public void FrontEntry_ZLast_IsLethal()
        {
            HeroSweep hero = Hero(0, 0, 0, 1.75, 0, 0, 1, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 0f, 3f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual(0.75, t, 1e-12);
            Assert.AreEqual(ContactEntry.Front, entry);
            Assert.IsFalse(lateral);
            Assert.IsTrue(CollisionRules.IsLethal(entry));
        }

        [Test]
        public void SideEntry_XLast_IsStumble()
        {
            HeroSweep hero = Hero(0, 0, 1.5, 1.75, 1, 0, 1.5, 1.75);
            ObstacleBox box = StaticBox(1f, 3f, 0f, 3f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual(0.75, t, 1e-12);
            Assert.AreEqual(ContactEntry.Side, entry);
            Assert.IsTrue(lateral);
            Assert.IsFalse(CollisionRules.IsLethal(entry));
            Assert.AreEqual(RunnerEventFlags.Side, CollisionRules.StumbleFlag(lateral));
        }

        [Test]
        public void FromAbove_FeetComeDownOntoTop_IsStumble()
        {
            HeroSweep hero = Hero(0, 1.0, 1.5, 1.75, 0, 0.5, 1.5, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 0f, 0.75f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual(0.5, t, 1e-12);
            Assert.AreEqual(ContactEntry.FromAbove, entry);
            Assert.IsFalse(lateral);
            Assert.IsFalse(CollisionRules.IsLethal(entry));
            Assert.AreEqual(RunnerEventFlags.Top, CollisionRules.StumbleFlag(lateral));
        }

        [Test]
        public void FromBelow_HeadRisesIntoUnderside_IsLethal()
        {
            // Sliding box (0.75 m) growing to standing (1.75 m) under a barrier from 1.25 m.
            HeroSweep hero = Hero(0, 0, 1.5, 0.75, 0, 0, 1.5, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 1.25f, 3f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool _));
            Assert.AreEqual(0.5, t, 1e-12);
            Assert.AreEqual(ContactEntry.FromBelow, entry);
            Assert.IsTrue(CollisionRules.IsLethal(entry));
        }

        [Test]
        public void FromBelow_JumpArcRisingIntoUnderside_IsLethal()
        {
            HeroSweep hero = Hero(0, 0.0, 1.5, 1.75, 0, 0.5, 1.5, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 2.0f, 3f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool _));
            Assert.AreEqual(0.5, t, 1e-12);
            Assert.AreEqual(ContactEntry.FromBelow, entry);
        }

        [Test]
        public void ExactTie_XAndZ_IsStumble()
        {
            // z: front 0.25 -> 1.25 enters 0.75 at t = 0.5. x: right side 0.75 -> 1.25 enters 1.0 at t = 0.5.
            HeroSweep hero = Hero(0.5, 0, 0, 1.75, 1.0, 0, 1, 1.75);
            ObstacleBox box = StaticBox(1f, 3f, 0f, 3f, 0.75, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual(0.5, t, 1e-12);
            Assert.AreEqual(ContactEntry.Tie, entry);
            Assert.IsTrue(lateral);
            Assert.IsFalse(CollisionRules.IsLethal(entry));
        }

        [Test]
        public void TouchingOnly_IsNoContact()
        {
            // HERO's right side ends exactly on the box's left side.
            HeroSweep hero = Hero(0, 0, 1.5, 1.75, 0.75, 0, 1.5, 1.75);
            ObstacleBox box = StaticBox(1f, 3f, 0f, 3f, 1.0, 2.0);

            Assert.IsFalse(SweptAabb.TrySweep(in hero, in box, out double _, out ContactEntry entry, out bool _));
            Assert.AreEqual(ContactEntry.None, entry);
        }

        [Test]
        public void PassingBesideAndOver_IsNoContact()
        {
            HeroSweep beside = Hero(0, 0, 0, 1.75, 0, 0, 4, 1.75);
            ObstacleBox other = StaticBox(1f, 3f, 0f, 3f, 1.0, 2.0);
            Assert.IsFalse(SweptAabb.TrySweep(in beside, in other, out double _, out ContactEntry _, out bool _));

            HeroSweep over = Hero(0, 1.0, 0, 1.75, 0, 1.0, 4, 1.75);
            ObstacleBox low = StaticBox(-1f, 1f, 0f, 0.75f, 1.0, 2.0);
            Assert.IsFalse(SweptAabb.TrySweep(in over, in low, out double _, out ContactEntry _, out bool _));
        }

        [Test]
        public void ThinBoxFasterThanItsDepth_IsStillHit()
        {
            // HERO moves 4 m in one tick through a 0.125 m deep box: no overlap at either end of the tick.
            HeroSweep hero = Hero(0, 0, 0, 1.75, 0, 0, 4, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 0f, 3f, 2.0, 2.125);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool _));
            Assert.AreEqual(ContactEntry.Front, entry);
            Assert.AreEqual(1.75 / 4.0, t, 1e-12);
        }

        [Test]
        public void AlreadyOverlapping_IsInside_TreatedAsSideStumble()
        {
            HeroSweep hero = Hero(0, 0, 1.5, 1.75, 0, 0, 1.75, 1.75);
            ObstacleBox box = StaticBox(-1f, 1f, 0f, 3f, 1.0, 2.0);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual(0.0, t);
            Assert.AreEqual(ContactEntry.Inside, entry);
            Assert.IsTrue(lateral);
            Assert.IsFalse(CollisionRules.IsLethal(entry));
        }

        [Test]
        public void MoverSlidingIntoStandingHero_EntersOnX()
        {
            // HERO stands still alongside (z overlaps all tick); the box moves left from 0.5 to 0.125.
            HeroSweep hero = Hero(0, 0, 1.5, 1.75, 0, 0, 1.5, 1.75);
            var box = new ObstacleBox
            {
                Id = 3,
                Archetype = ObstacleArchetype.Mover,
                XMinPrev = 0.5f,
                XMaxPrev = 2.5f,
                XMin = 0.125f,
                XMax = 2.125f,
                YMin = 0f,
                YMax = 1.875f,
                ZMin = 1.0,
                ZMax = 2.5,
            };
            Assert.IsTrue(box.IsMoving);

            Assert.IsTrue(SweptAabb.TrySweep(in hero, in box, out double t, out ContactEntry entry, out bool lateral));
            Assert.AreEqual((0.5 - 0.25) / 0.375, t, 1e-12);
            Assert.AreEqual(ContactEntry.Side, entry);
            Assert.IsTrue(lateral);
        }

        [Test]
        public void MoverAndHeroMovingApartSideways_NoContact()
        {
            HeroSweep hero = Hero(0, 0, 1.5, 1.75, -0.5, 0, 1.5, 1.75);
            var box = new ObstacleBox
            {
                Id = 3,
                Archetype = ObstacleArchetype.Mover,
                XMinPrev = 0.5f,
                XMaxPrev = 2.5f,
                XMin = 0.375f,
                XMax = 2.375f,
                YMin = 0f,
                YMax = 1.875f,
                ZMin = 1.0,
                ZMax = 2.5,
            };

            Assert.IsFalse(SweptAabb.TrySweep(in hero, in box, out double _, out ContactEntry _, out bool _));
        }

        [Test]
        public void GapXY_SmallestDistanceInXYPlane()
        {
            HeroSweep hero = Hero(0, 1.0, 0, 1.75, 0, 1.0, 0, 1.75);
            Assert.AreEqual(0.25, SweptAabb.GapXY(in hero, StaticBox(-1f, 1f, 0f, 0.75f, 0, 1)), 1e-12, "vertical");
            Assert.AreEqual(0.5, SweptAabb.GapXY(in hero, StaticBox(0.75f, 2f, 0f, 3f, 0, 1)), 1e-12, "lateral");
            Assert.AreEqual(System.Math.Sqrt(0.5 * 0.5 + 0.25 * 0.25), SweptAabb.GapXY(in hero, StaticBox(0.75f, 2f, 0f, 0.75f, 0, 1)), 1e-12, "corner");
        }

        [Test]
        public void Categories_MatchSpecTable()
        {
            Assert.IsTrue(CollisionRules.IsLethal(ContactEntry.Front));
            Assert.IsTrue(CollisionRules.IsLethal(ContactEntry.FromBelow));
            Assert.IsFalse(CollisionRules.IsLethal(ContactEntry.FromAbove));
            Assert.IsFalse(CollisionRules.IsLethal(ContactEntry.Side));
            Assert.IsFalse(CollisionRules.IsLethal(ContactEntry.Tie));
            Assert.IsFalse(CollisionRules.IsLethal(ContactEntry.Inside));
        }
    }
}
