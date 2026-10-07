using System;
using System.Collections.Generic;
using System.IO;
using JungleBooze.Gameplay.Path;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>Spec 003 T1: AC-301 (isolation) and AC-302 (straight route is the identity mapping).</summary>
    public sealed class PathFrameTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void AC301_SimulationSourceNeverReferencesThePathNamespace()
        {
            string gameplay = System.IO.Path.Combine(Application.dataPath, "_Game", "Scripts", "Gameplay");
            string core = System.IO.Path.Combine(Application.dataPath, "_Game", "Scripts", "Core");

            // Simulation folders (spec 003 section 3.1 isolation rule). Views and the Path folder itself are allowed.
            string[] folders =
            {
                core,
                System.IO.Path.Combine(gameplay, "Runner"),
                System.IO.Path.Combine(gameplay, "Track"),
                System.IO.Path.Combine(gameplay, "Vine"),
                System.IO.Path.Combine(gameplay, "PowerUps"),
                System.IO.Path.Combine(gameplay, "Hazards"),
                System.IO.Path.Combine(gameplay, "Companion"),
                System.IO.Path.Combine(gameplay, "Session"),
            };

            var files = new List<string>();
            foreach (string folder in folders)
            {
                Assert.IsTrue(Directory.Exists(folder), "simulation folder not found: " + folder);
                files.AddRange(Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));
            }

            Assert.Greater(files.Count, 20, "simulation sources not found");

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i];
                    int comment = code.IndexOf("//", StringComparison.Ordinal);
                    if (comment >= 0)
                    {
                        code = code.Substring(0, comment);
                    }

                    Assert.IsFalse(
                        code.Contains("Gameplay.Path"),
                        System.IO.Path.GetFileName(file) + " line " + (i + 1) + " references the Path namespace.");
                }
            }
        }

        [Test]
        public void AC302_StraightRouteToWorldIsIdentity()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var frame = new PathFrame(tuning, new StraightRouteSource());
            frame.BeginRun(12345UL, -tuning.BehindM);

            var rng = new System.Random(7);
            for (int step = 0; step < 20; step++)
            {
                double hero = step * 150.0;
                frame.Extend(hero + tuning.AheadM);

                for (int k = 0; k < 50; k++)
                {
                    double s = hero - 30.0 + rng.NextDouble() * 220.0;
                    float x = (float)(rng.NextDouble() * 7.2 - 3.6);
                    float y = (float)(rng.NextDouble() * 4.0);

                    Vector3 world = frame.ToWorld(s, x, y);
                    Assert.AreEqual(x, world.x, Tolerance, "x at s=" + s);
                    Assert.AreEqual(y, world.y, Tolerance, "y at s=" + s);
                    Assert.AreEqual((float)s, world.z, Tolerance, "z at s=" + s);

                    Quaternion rotation = frame.RotationAt(s);
                    Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(rotation, Quaternion.identity)), Tolerance, "rotation at s=" + s);
                    Assert.AreEqual(0f, frame.GroundHeightAt(s, x), Tolerance);
                }
            }

            // Beyond the built range the straight route continues as the identity.
            Assert.AreEqual(5000f, frame.ToWorld(5000.0, 0f, 0f).z, Tolerance);
            Assert.AreEqual(0f, frame.ComputeRebase(new Vector3(0f, 0f, 5000f)).z, Tolerance, "floating origin is off by default");
        }
    }
}
