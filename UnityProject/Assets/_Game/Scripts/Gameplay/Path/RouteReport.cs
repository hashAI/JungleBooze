using System.Collections.Generic;
using System.Text;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>Result of <see cref="RouteValidator.Validate"/>: one counter per rule of spec 003 sections 4.3, 6.1 and 9.</summary>
    public sealed class RouteReport
    {
        /// <summary>Most messages kept (the counters keep counting).</summary>
        public const int MaxMessages = 32;

        public int SamplesChecked;

        /// <summary>Sustained radius below the world's limit (or below the calm limit at the start).</summary>
        public int RadiusViolations;

        /// <summary>Change of curvature per metre above the jerk limit (the emergency limit on emergency samples).</summary>
        public int JerkViolations;

        /// <summary>Yaw rate at the boost speed above the hard cap.</summary>
        public int YawRateViolations;

        /// <summary>Heading change over the turn window above the limit.</summary>
        public int TurnWindowViolations;

        /// <summary>Low-passed heading further than the restoring limit from the forward axis.</summary>
        public int RestoringViolations;

        public int GradeViolations;

        /// <summary>Crest or sag tighter than the vertical radius limits.</summary>
        public int VerticalCurveViolations;

        public int BankViolations;

        /// <summary>Not straight (R at least 250 m, grade at most 4 percent, gateway level) from 30 m before a vine or gateway chunk.</summary>
        public int ZoneViolations;

        /// <summary>A beat kind drawn more than twice in a row.</summary>
        public int RepeatViolations;

        /// <summary>More than the clearing distance between two breaths (clearing, landing glade, gateway).</summary>
        public int ClearingViolations;

        /// <summary>Pairs of samples closer than the minimum distance outside the s-window (AC-306).</summary>
        public int SelfIntersections;

        /// <summary>Points of the next-40 m rule outside the viewport margin (AC-305).</summary>
        public int VisibilityViolations;

        /// <summary>Samples shaped by the emergency ease (AC-312). Reported, not counted as a violation.</summary>
        public int EmergencySamples;

        /// <summary>Largest ratio of a probed point to the viewport half width (1 = at the edge, limit 0.94).</summary>
        public double WorstVisibilityRatio;

        /// <summary>Number of vine and gateway zones found.</summary>
        public int Zones;

        public List<string> Messages { get; } = new List<string>();

        public int TotalViolations =>
            RadiusViolations + JerkViolations + YawRateViolations + TurnWindowViolations + RestoringViolations
            + GradeViolations + VerticalCurveViolations + BankViolations + ZoneViolations + RepeatViolations
            + ClearingViolations + SelfIntersections + VisibilityViolations;

        public bool IsValid => TotalViolations == 0;

        public void Note(string message)
        {
            if (Messages.Count < MaxMessages)
            {
                Messages.Add(message);
            }
        }

        public override string ToString()
        {
            var b = new StringBuilder();
            b.Append("samples ").Append(SamplesChecked).Append(", violations ").Append(TotalViolations);
            b.Append(" (radius ").Append(RadiusViolations).Append(", jerk ").Append(JerkViolations);
            b.Append(", yaw rate ").Append(YawRateViolations).Append(", turn window ").Append(TurnWindowViolations);
            b.Append(", restoring ").Append(RestoringViolations).Append(", grade ").Append(GradeViolations);
            b.Append(", vertical ").Append(VerticalCurveViolations).Append(", bank ").Append(BankViolations);
            b.Append(", zone ").Append(ZoneViolations).Append(", repeat ").Append(RepeatViolations);
            b.Append(", clearing ").Append(ClearingViolations).Append(", self ").Append(SelfIntersections);
            b.Append(", visibility ").Append(VisibilityViolations).Append("), emergency ").Append(EmergencySamples);
            for (int i = 0; i < Messages.Count; i++)
            {
                b.Append('\n').Append(Messages[i]);
            }

            return b.ToString();
        }
    }
}
