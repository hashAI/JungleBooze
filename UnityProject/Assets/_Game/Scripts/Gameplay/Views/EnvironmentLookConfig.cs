using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Look tuning for the run's environment (ground, jungle walls, sky, light), plain C#. Field defaults are the
    /// look-pass start values; <see cref="JungleBooze.Gameplay.Config.EnvironmentLookConfigAsset"/> copies its serialized
    /// fields into one. tools/blender/run_mock.py mirrors these defaults for preview renders; keep them in sync.
    /// </summary>
    public sealed class EnvironmentLookConfig
    {
        // Ground: one trail strip (textured across its width, repeating along the run) and two jungle-floor strips.

        /// <summary>Half width of the trail mesh in m (the 4.2 m path plus a painted grassy edge).</summary>
        public float TrailHalfWidthM = 5.4f;

        /// <summary>Length in m of one texture repeat along the run (trail and floor); the strips move in these steps.</summary>
        public float GroundTileM = 6f;

        /// <summary>How far behind HERO the ground strips start (m).</summary>
        public float GroundBehindM = 18f;

        /// <summary>Ground drawn past the view distance (m), so the far end is always inside the fog.</summary>
        public float GroundExtraAheadM = 24f;

        /// <summary>Jungle floor strips span this distance from the track centre (inner edge tucked under the trail).</summary>
        public float FloorInnerM = 4.6f;

        public float FloorOuterM = 44f;

        /// <summary>The floor sits this far below the trail so the trail edge always draws on top.</summary>
        public float FloorDropM = 0.02f;

        /// <summary>Anisotropic filtering for the ground textures (sharp at grazing angles).</summary>
        public int GroundAnisoLevel = 4;

        // Jungle walls: pre-merged 12 m segments (Jungle_WallA/B/C), one per side per segment slot.

        public float WallSegmentM = 12f;

        /// <summary>Wall origin offset from the path edge (m, positive = away from the path).</summary>
        public float WallInsetM = 0f;

        public float WallBehindM = 12f;

        /// <summary>Per-segment height scale = min + hash share of span (variety without a random source).</summary>
        public float WallHeightScaleMin = 0.92f;

        public float WallHeightScaleSpan = 0.22f;

        /// <summary>Mirror every other segment along the run (by hash) for more variety.</summary>
        public bool WallMirror = true;

        // Sky (SkyView): far canopy ring colors = shadow tint mixed toward fog by these amounts.

        public float FarRingHaze = 0.72f;
        public float NearRingHaze = 0.55f;

        // Light: key light rotation (Jungle; other worlds keep their style guide offsets) and trilight ambient.

        public float KeyPitchDeg = 38f;
        public float KeyYawDeg = -35f;
        public float KeyIntensity = 1.0f;

        /// <summary>Ambient from above = world shadow tint mixed toward white (cool fill against the warm key).</summary>
        public float AmbientSkyWhiteMix = 0.6f;

        /// <summary>Ambient at the horizon = shadow tint mixed toward the sky horizon color.</summary>
        public float AmbientEquatorMix = 0.5f;

        /// <summary>Ambient from below = shadow tint mixed toward ink.</summary>
        public float AmbientGroundInkMix = 0f;

        public static EnvironmentLookConfig CreateDefault()
        {
            return new EnvironmentLookConfig();
        }

        /// <summary>Range checks. Appends one message per problem; returns true when there are none. Allocates.</summary>
        public bool Validate(List<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            int before = errors.Count;
            CheckRange(errors, "trailHalfWidthM", TrailHalfWidthM, 1f, 20f);
            CheckRange(errors, "groundTileM", GroundTileM, 1f, 50f);
            CheckRange(errors, "groundBehindM", GroundBehindM, 0f, 100f);
            CheckRange(errors, "groundExtraAheadM", GroundExtraAheadM, 0f, 200f);
            CheckRange(errors, "floorInnerM", FloorInnerM, 0f, 50f);
            CheckRange(errors, "floorOuterM", FloorOuterM, FloorInnerM + 1f, 500f);
            CheckRange(errors, "floorDropM", FloorDropM, 0f, 1f);
            CheckRange(errors, "groundAnisoLevel", GroundAnisoLevel, 0f, 16f);
            CheckRange(errors, "wallSegmentM", WallSegmentM, 2f, 100f);
            CheckRange(errors, "wallInsetM", WallInsetM, -2f, 20f);
            CheckRange(errors, "wallBehindM", WallBehindM, 0f, 100f);
            CheckRange(errors, "wallHeightScaleMin", WallHeightScaleMin, 0.1f, 5f);
            CheckRange(errors, "wallHeightScaleSpan", WallHeightScaleSpan, 0f, 5f);
            CheckRange(errors, "farRingHaze", FarRingHaze, 0f, 1f);
            CheckRange(errors, "nearRingHaze", NearRingHaze, 0f, 1f);
            CheckRange(errors, "keyPitchDeg", KeyPitchDeg, -90f, 90f);
            CheckRange(errors, "keyYawDeg", KeyYawDeg, -360f, 360f);
            CheckRange(errors, "keyIntensity", KeyIntensity, 0f, 8f);
            CheckRange(errors, "ambientSkyWhiteMix", AmbientSkyWhiteMix, 0f, 1f);
            CheckRange(errors, "ambientEquatorMix", AmbientEquatorMix, 0f, 1f);
            CheckRange(errors, "ambientGroundInkMix", AmbientGroundInkMix, 0f, 1f);
            return errors.Count == before;
        }

        private static void CheckRange(List<string> errors, string name, float value, float min, float max)
        {
            if (!(value >= min && value <= max))
            {
                errors.Add(name + " must be in " + min + ".." + max + " (is " + value + ").");
            }
        }
    }
}
