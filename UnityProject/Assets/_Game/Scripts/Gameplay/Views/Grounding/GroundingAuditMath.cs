using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Pure math of the obstacle grounding audit (spec 005 3.2, G1, G6): compares a hitbox with the bounds of what is
    /// drawn for it, both in the rig's local frame (x lateral, y up, z along the path). The ghost distances are
    /// bounds-based, so they under-report holes inside a model; they find missing or misplaced models and boxes that
    /// do not reach the hitbox.
    /// </summary>
    public static class GroundingAuditMath
    {
        /// <summary>A hitbox whose bottom is within this of the ground counts as ground-level.</summary>
        public const float GroundLevelM = 0.01f;

        public static GroundingAuditResult Evaluate(
            Vector3 hitMin, Vector3 hitMax, Vector3 visibleMin, Vector3 visibleMax, bool hasVisible,
            float ghostMaxM, float embedMinM, float maxFloatM)
        {
            var r = new GroundingAuditResult();
            if (!hasVisible)
            {
                r.Failures = GroundingFailures.NoVisible;
                r.GhostFrontM = 0.5f;
                return r;
            }

            r.GhostFrontM = Mathf.Max(0f, visibleMin.z - hitMin.z);
            r.ProudFrontM = Mathf.Max(0f, hitMin.z - visibleMin.z);
            r.GhostTopM = Mathf.Max(0f, hitMax.y - visibleMax.y);
            r.GhostLeftM = Mathf.Max(0f, visibleMin.x - hitMin.x);
            r.GhostRightM = Mathf.Max(0f, hitMax.x - visibleMax.x);

            bool ground = hitMin.y <= GroundLevelM;
            if (ground)
            {
                r.EmbedM = Mathf.Max(0f, -visibleMin.y);
                r.FloatM = Mathf.Max(0f, visibleMin.y);
            }
            else
            {
                r.GhostBottomM = Mathf.Max(0f, visibleMin.y - hitMin.y);
            }

            GroundingFailures f = GroundingFailures.None;
            if (r.GhostFrontM > ghostMaxM)
            {
                f |= GroundingFailures.GhostFront;
            }

            if (r.GhostTopM > ghostMaxM)
            {
                f |= GroundingFailures.GhostTop;
            }

            if (r.GhostLeftM > ghostMaxM || r.GhostRightM > ghostMaxM)
            {
                f |= GroundingFailures.GhostSide;
            }

            if (r.GhostBottomM > ghostMaxM)
            {
                f |= GroundingFailures.GhostBottom;
            }

            if (ground)
            {
                if (r.FloatM > maxFloatM)
                {
                    f |= GroundingFailures.Floating;
                }

                // A tolerance of 5 mm: bounds come from float transforms.
                if (r.EmbedM + 0.005f < embedMinM)
                {
                    f |= GroundingFailures.EmbedTooShallow;
                }
            }

            r.Failures = f;
            return r;
        }
    }
}
