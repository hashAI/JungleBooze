using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Controls
{
    /// <summary>
    /// Review B1, S2 and S9: AC-101-32 … AC-101-35 at 60, 120 and 240 Hz touch rates, with ±1 pt sample jitter,
    /// curved swipes, a thumb that rests before swiping, and the one-angle swipe/steer rule with hysteresis.
    /// </summary>
    public sealed class GestureRobustnessTests
    {
        private const double Sensitivity = 0.04;

        private sealed class Finger
        {
            private readonly GestureRecognizer _g;
            private readonly double _dt;
            private readonly float _noise;
            private readonly Pcg32Random _random;

            public Finger(GestureRecognizer g, int hz, float noise, ulong seed = 7UL)
            {
                _g = g;
                _dt = 1.0 / hz;
                _noise = noise;
                _random = new Pcg32Random(seed);
            }

            public int Id { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public double T { get; private set; }

            /// <summary>Samples that produced a command so far, by sample count since Down.</summary>
            public int Samples { get; private set; }

            public void Down(int id, float x, float y, double t)
            {
                Id = id;
                X = x;
                Y = y;
                T = t;
                Samples = 0;
                _g.Process(new TouchSample(id, TouchPhaseKind.Began, x, y, t));
            }

            public void Rest(double seconds)
            {
                T += seconds; // iOS sends no samples while the finger is still
            }

            /// <summary>Straight motion by (dx, dy) over the duration at the finger's rate, each sample jittered.</summary>
            public void Line(float dx, float dy, double duration, Action<int> afterSample = null)
            {
                Curve((k) => (dx * k, dy * k), duration, afterSample);
            }

            /// <summary>Motion along an arbitrary path p(k), k in (0, 1], relative to the current point.</summary>
            public void Curve(Func<float, (float, float)> path, double duration, Action<int> afterSample = null)
            {
                int n = Math.Max(1, (int)Math.Round(duration / _dt));
                float x0 = X;
                float y0 = Y;
                double t0 = T;
                for (int i = 1; i <= n; i++)
                {
                    (float px, float py) = path((float)i / n);
                    X = x0 + px;
                    Y = y0 + py;
                    T = t0 + (i * duration / n);
                    Samples++;
                    _g.Process(new TouchSample(Id, TouchPhaseKind.Moved, X + Jitter(), Y + Jitter(), T));
                    afterSample?.Invoke(Samples);
                }
            }

            public void Up()
            {
                T += _dt;
                _g.Process(new TouchSample(Id, TouchPhaseKind.Ended, X, Y, T));
            }

            private float Jitter()
            {
                return _noise > 0f ? _random.NextFloat(-_noise, _noise) : 0f;
            }
        }

        private static int Count(GestureRecognizer g, InputCommand command)
        {
            int n = 0;
            for (int i = 0; i < g.Commands.Count; i++)
            {
                if (g.Commands.Peek(i) == command)
                {
                    n++;
                }
            }

            return n;
        }

        private static float Tan(double degrees)
        {
            return (float)Math.Tan(degrees * Math.PI / 180.0);
        }

        // ---- B1: a swipe after the thumb has rested ----

        [TestCase(60)]
        [TestCase(120)]
        [TestCase(240)]
        public void B1_SwipeAfterRestingThumbJumpsOnTheCrossingSample(int hz)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Rest(0.5);
            int firedAt = -1;
            int crossing = -1;
            float y0 = f.Y;
            f.Line(0f, 30f, 0.067, i =>
            {
                if (crossing < 0 && f.Y - y0 >= 24f)
                {
                    crossing = i;
                }

                if (firedAt < 0 && g.Commands.Count > 0)
                {
                    firedAt = i;
                }
            });
            Assert.AreEqual(1, Count(g, InputCommand.Jump), "30 pt in 67 ms after a 0.5 s rest is a jump");
            Assert.AreEqual(crossing, firedAt, "fires on the sample that crosses 24 pt from the resting point");
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-6);
        }

        [TestCase(60)]
        [TestCase(120)]
        public void B1_RestDuringADragThenSwipeDownSlides(int hz)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(50f, 0f, 0.4);
            f.Rest(0.8);
            f.Line(0f, -26f, 0.06);
            Assert.AreEqual(1, Count(g, InputCommand.Slide));
            Assert.AreEqual(50.0 * Sensitivity, g.ConsumeLateralM(), 0.01, "the drag steered, the swipe did not");
        }

        [Test]
        public void B1_RestThenSlowRiseIsStillNoSwipe()
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, 60, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Rest(0.5);
            f.Line(0f, 30f, 0.3);
            Assert.AreEqual(0, g.Commands.Count, "30 pt in 0.3 s is under 200 pt/s even after a rest");
        }

        // ---- AC-101-32 … 35 at higher rates and with jitter ----

        [TestCase(60, 0f)]
        [TestCase(120, 0f)]
        [TestCase(240, 0f)]
        [TestCase(60, 1f)]
        [TestCase(120, 1f)]
        [TestCase(240, 1f)]
        public void AC32_ThresholdsHoldAtEveryRateAndWithJitter(int hz, float noise)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(0f, 27f, 0.1);
            Assert.AreEqual(1, Count(g, InputCommand.Jump), "27 pt in 100 ms");

            g = new GestureRecognizer(new GestureConfig());
            f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(0f, 21f, 0.1);
            f.Up();
            Assert.AreEqual(0, g.Commands.Count, "21 pt is nothing");

            g = new GestureRecognizer(new GestureConfig());
            f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(40f * Tan(42.0), 40f, 0.1);
            Assert.AreEqual(0, Count(g, InputCommand.Jump), "42° from vertical is no swipe");
        }

        [TestCase(60, 0f, 25.0)]
        [TestCase(120, 1f, 25.0)]
        [TestCase(240, 1f, 25.0)]
        [TestCase(60, 0f, 33.0)]
        [TestCase(120, 0f, 33.0)]
        [TestCase(240, 0f, 33.0)]
        [TestCase(120, 1f, 30.0)]
        [TestCase(240, 1f, 30.0)]
        public void AC33_DiagonalSwipeIsASwipeAndNeverSteers(int hz, float noise, double degrees)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(30f * Tan(degrees), 30f, 0.09);
            f.Line(4f * Tan(degrees), 4f, 0.02);
            f.Up();
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.Less(Math.Abs(g.ConsumeLateralM()), 0.05, "a swipe never steers (" + degrees + "°)");
            Assert.AreEqual(0, Count(g, InputCommand.DodgeRight));
        }

        [TestCase(60)]
        [TestCase(120)]
        [TestCase(240)]
        public void CurvedSwipeThatStartsSidewaysDoesNotLeakSteering(int hz)
        {
            // A real thumb swipe often starts with a sideways wobble (here 50° from vertical for the first ~8 pt).
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, 1f);
            f.Down(1, 100f, 100f, 0.0);
            f.Curve(k => (8f * (float)Math.Sin(Math.Min(1f, k * 3f) * Math.PI * 0.5), 34f * k * k), 0.1);
            f.Up();
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.Less(Math.Abs(g.ConsumeLateralM()), 0.05);
        }

        [TestCase(60, 0f)]
        [TestCase(120, 1f)]
        [TestCase(240, 1f)]
        public void AC34_FlickAndSlowDragAtEveryRate(int hz, float noise)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(40f, 0f, 0.15);
            f.Up();
            Assert.AreEqual(1, Count(g, InputCommand.DodgeRight), "40 pt in 150 ms dodges");

            g = new GestureRecognizer(new GestureConfig());
            f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(-40f, 0f, 0.3);
            f.Up();
            Assert.AreEqual(0, g.Commands.Count, "slow drag: steering only");
            Assert.AreEqual(-40.0 * Sensitivity, g.ConsumeLateralM(), 0.05);
        }

        [TestCase(60, 0f)]
        [TestCase(120, 1f)]
        [TestCase(240, 1f)]
        public void AC35_DragSwipeDragAtEveryRate(int hz, float noise)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, noise);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(60f, 0f, 0.5);
            f.Line(0f, 30f, 0.08);
            f.Line(60f, 0f, 0.5);
            f.Rest(0.1);
            f.Up();
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.AreEqual(0, Count(g, InputCommand.DodgeLeft) + Count(g, InputCommand.DodgeRight));
            // Turning from the swipe back into the drag costs at most one direction segment (6 pt = 0.24 m) of drag:
            // the corner segment is still mostly vertical. Clean 60 Hz input loses nothing (AC35 in GestureRecognizerTests).
            Assert.AreEqual(120.0 * Sensitivity, g.ConsumeLateralM(), 0.24, "steering continued around the swipe");
        }

        [TestCase(60)]
        [TestCase(240)]
        public void NoisySteeringNeverFiresASwipe(int hz)
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, hz, 1f, 99UL);
            f.Down(1, 100f, 100f, 0.0);
            for (int i = 0; i < 6; i++)
            {
                f.Line(i % 2 == 0 ? 70f : -70f, i % 3 == 0 ? 8f : -8f, 0.35);
            }

            f.Rest(0.3);
            f.Up();
            Assert.AreEqual(0, g.Commands.Count);
        }

        // ---- S2: one angle with hysteresis ----

        [Test]
        public void SwipeBetween30And35DegreesDoesNotAlsoSteer()
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, 60, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(24f * Tan(33.0), 24f, 0.05);
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.Less(Math.Abs(g.ConsumeLateralM()), 0.05, "review S2: was 0.62 m");
        }

        [Test]
        public void AfterASwipeADiagonalWobbleDoesNotSteerButAClearDragDoes()
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, 120, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(0f, 30f, 0.08);
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            g.ConsumeLateralM();
            f.Line(10f, 10f, 0.15); // 45°: inside the hysteresis band, stays "vertical"
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-6, "no steering inside the band after a swipe");
            f.Line(40f, 0f, 0.3);
            // The unlatching segment brings its own travel along (here up to the 45° wobble's last partial segment).
            Assert.AreEqual(40.0 * Sensitivity, g.ConsumeLateralM(), 0.06, "a clear horizontal drag steers with no lost travel");
        }

        [Test]
        public void SteeringDragKeepsSteeringInsideTheBand()
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, 60, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(30f, 0f, 0.3);
            f.Line(20f, 20f, 0.3); // 45°: steering continues (hysteresis keeps the current mode)
            Assert.AreEqual(50.0 * Sensitivity, g.ConsumeLateralM(), 1e-3);
            Assert.AreEqual(0, g.Commands.Count);
        }

        [Test]
        public void SlowVerticalDriftDoesNotSteer()
        {
            var g = new GestureRecognizer(new GestureConfig());
            var f = new Finger(g, 60, 0f);
            f.Down(1, 100f, 100f, 0.0);
            f.Line(8f, 40f, 0.6); // 11° from vertical, too slow for a swipe
            Assert.AreEqual(0, g.Commands.Count);
            Assert.Less(Math.Abs(g.ConsumeLateralM()), 0.06, "at most the first direction segment's sideways share");
        }
    }
}
