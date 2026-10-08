using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// <c>ObstacleKit</c> in designer units (spec 002 section 3.2). Hitboxes never differ between worlds.
    /// Field defaults are the spec start values.
    /// </summary>
    [Serializable]
    public sealed class ObstacleKitDesignValues
    {
        public ObstacleShape LowBarrier = new ObstacleShape(2.04f, 0.6f, 0.0f, 0.8f);
        public ObstacleShape HighBarrier = new ObstacleShape(2.04f, 0.5f, 1.1f, 3.0f);
        public ObstacleShape FullBlock = new ObstacleShape(2.04f, 1.0f, 0.0f, 3.0f);
        public ObstacleShape Mover = new ObstacleShape(1.90f, 1.6f, 0.0f, 1.9f);

        /// <summary>
        /// Signature lane denial (GDD 8.3, thorn patch), per covered lane: deeper than a full block and taller than
        /// the jump apex (1.5 m), so the only answer is the free lane [ASSUMED size].
        /// </summary>
        public ObstacleShape LaneDenial = new ObstacleShape(2.04f, 4.0f, 0.0f, 2.2f);

        /// <summary>Signature lane strike (GDD 8.3), box while the strike is active [ASSUMED size].</summary>
        public ObstacleShape LaneStrike = new ObstacleShape(2.04f, 2.0f, 0.0f, 3.0f);

        public float[] GapLengthsM = { 3.0f, 4.0f };
        public float GapMinWindowS = 0.25f;
        public float GapRunAcrossMarginM = 0.25f;
        public float GapLandingClearM = 1.0f;
        public float MoverLateralSpeedMps = 4.8f;
        public float MoverTriggerLeadS = 1.4f;
        public float MoverMinSettleS = 0.5f;
        public int MoverMaxLaneShift = 1;

        public static ObstacleKitDesignValues CreateDefault()
        {
            return new ObstacleKitDesignValues();
        }

        public ObstacleKitDesignValues Clone()
        {
            var copy = (ObstacleKitDesignValues)MemberwiseClone();
            copy.GapLengthsM = GapLengthsM == null ? null : (float[])GapLengthsM.Clone();
            return copy;
        }

        /// <summary>Range checks of spec 002 section 3.2. Appends one message per problem.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            CheckShape(errors, "lowBarrier", LowBarrier);
            CheckShape(errors, "highBarrier", HighBarrier);
            CheckShape(errors, "fullBlock", FullBlock);
            CheckShape(errors, "mover", Mover);
            CheckShape(errors, "laneDenial", LaneDenial);
            CheckShape(errors, "laneStrike", LaneStrike);

            if (GapLengthsM == null || GapLengthsM.Length == 0)
            {
                errors.Add("gapLengthsM needs at least one length.");
            }
            else
            {
                for (int i = 0; i < GapLengthsM.Length; i++)
                {
                    ConfigChecks.Range(errors, "gapLengthsM[" + i + "]", GapLengthsM[i], 2.0, 6.0);
                }
            }

            ConfigChecks.Range(errors, "gapMinWindowS", GapMinWindowS, 0.15, 0.40);
            ConfigChecks.Range(errors, "gapRunAcrossMarginM", GapRunAcrossMarginM, 0.1, 0.5);
            ConfigChecks.Range(errors, "gapLandingClearM", GapLandingClearM, 0.5, 3.0);
            ConfigChecks.Range(errors, "moverLateralSpeedMps", MoverLateralSpeedMps, 3.0, 8.0);
            ConfigChecks.Range(errors, "moverTriggerLeadS", MoverTriggerLeadS, 1.2, 2.5);
            ConfigChecks.Range(errors, "moverMinSettleS", MoverMinSettleS, 0.3, 1.0);
            if (MoverMaxLaneShift != 1)
            {
                errors.Add("moverMaxLaneShift must be 1 in FP1.");
            }

            return errors.Count == before;
        }

        private static void CheckShape(List<string> errors, string name, ObstacleShape s)
        {
            ConfigChecks.Positive(errors, name + ".boxWidthM", s.WidthM);
            ConfigChecks.Positive(errors, name + ".boxDepthM", s.DepthM);
            if (!(s.BottomM >= 0f))
            {
                errors.Add(name + ".boxBottomM must not be negative.");
            }

            if (!(s.TopM > s.BottomM))
            {
                errors.Add(name + ".boxTopM must be above boxBottomM.");
            }
        }
    }
}
