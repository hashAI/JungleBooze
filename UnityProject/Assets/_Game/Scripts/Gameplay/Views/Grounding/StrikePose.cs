using JungleBooze.Gameplay.Hazards;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Pose of the lane-strike column as a pure function of (phase, ticks in the phase, alpha) (spec 005 10.1 S5), so
    /// EditMode tests exercise it without Unity objects. This is the CURRENT gray-box behavior, moved here unchanged:
    /// the column exists only while the strike is Active and drops the last <see cref="DropHeightM"/> meters over the
    /// first <see cref="DropTicks"/> Active ticks. The physical log-drop of spec 005 10.2 (wave 2) replaces
    /// <see cref="Evaluate"/> behind the same signature.
    /// </summary>
    public static class StrikePose
    {
        /// <summary>The column starts this high above its resting place (m).</summary>
        public const float DropHeightM = 1f;

        /// <summary>Ticks the drop takes.</summary>
        public const int DropTicks = 3;

        /// <summary>
        /// Whether the column is shown and the height of its bottom above the ground. Outside Active the column is
        /// hidden (bottom 0); at Active tick 0 it is <see cref="DropHeightM"/> up and settles to 0 after
        /// <see cref="DropTicks"/> ticks.
        /// </summary>
        public static void Evaluate(LaneStrikePhase phase, int phaseTicks, float alpha, out bool columnVisible, out float bottomM)
        {
            if (phase != LaneStrikePhase.Active)
            {
                columnVisible = false;
                bottomM = 0f;
                return;
            }

            columnVisible = true;
            float drop = Mathf.Clamp01((phaseTicks + alpha) / DropTicks);
            bottomM = (1f - drop) * DropHeightM;
        }
    }
}
