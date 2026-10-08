using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Hazards
{
    /// <summary>
    /// <c>HazardTuning</c> in designer units (GDD 8.3 and 16): timing of the telegraphed lane strike. One set of
    /// values for every world's skin in the gray-box build [ASSUMED]; per-skin rows come with the world skins.
    /// Hitboxes of both signature behaviors live in the obstacle kit (shared by all worlds).
    /// </summary>
    [Serializable]
    public sealed class HazardDesignValues
    {
        /// <summary>Warning shown in the lane before the strike (GDD 8.2: falling rocks 1.3 s, darts 1.2 s).</summary>
        public float LaneStrikeWarningS = 1.3f;

        /// <summary>How long the strike is in the lane [ASSUMED].</summary>
        public float LaneStrikeActiveS = 0.6f;

        /// <summary>One full cycle warning + active + rest (the visible rhythm, GDD 8.2 water spout 1.5 s rest) [ASSUMED].</summary>
        public float LaneStrikeCycleS = 3.4f;

        /// <summary>
        /// Where in the active time HERO would arrive if he stayed in the lane at constant speed (0 = at the start,
        /// 1 = at the end). Sets the trigger lead: warning + active × this [ASSUMED 0.5: arrive mid-strike].
        /// </summary>
        public float LaneStrikeArrivalFraction = 0.5f;

        public static HazardDesignValues CreateDefault()
        {
            return new HazardDesignValues();
        }

        public HazardDesignValues Clone()
        {
            return (HazardDesignValues)MemberwiseClone();
        }

        /// <summary>Sanity ranges. GDD 8.3: the answer is visible at least 1.2 s before impact.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "laneStrikeWarningS", LaneStrikeWarningS, 1.2, 4);
            ConfigChecks.Range(errors, "laneStrikeActiveS", LaneStrikeActiveS, 0.2, 3);
            ConfigChecks.Range(errors, "laneStrikeCycleS", LaneStrikeCycleS, LaneStrikeWarningS + LaneStrikeActiveS, 20);
            ConfigChecks.Range(errors, "laneStrikeArrivalFraction", LaneStrikeArrivalFraction, 0, 1);
            return errors.Count == before;
        }
    }
}
