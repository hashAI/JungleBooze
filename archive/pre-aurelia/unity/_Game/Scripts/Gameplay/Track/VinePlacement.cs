using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// One vine in a vine-section chunk (GDD 7.2). Chunk-relative; <see cref="Zc"/> is the grab point (centre of
    /// the grab zone). Rows chain: row 0 is grabbed with a jump, rows 1 and 2 can be reached from the previous
    /// row's release. Serializable so chunk assets can hold it.
    /// </summary>
    [Serializable]
    public struct VinePlacement
    {
        public int Lane;

        public float Zc;

        /// <summary>0, 1 or 2.</summary>
        public int Row;

        /// <summary>A chasm (a gap placed by the same chunk) lies below: missing the vine is a death.</summary>
        public bool OverChasm;

        public static VinePlacement Safe(int lane, float zc, int row = 0)
        {
            return new VinePlacement { Lane = lane, Zc = zc, Row = row, OverChasm = false };
        }

        public static VinePlacement Chasm(int lane, float zc, int row = 0)
        {
            return new VinePlacement { Lane = lane, Zc = zc, Row = row, OverChasm = true };
        }

        /// <summary>Spec 002 section 4.2 mirroring: lane l → 2 − l.</summary>
        public VinePlacement Mirrored()
        {
            VinePlacement p = this;
            p.Lane = LaneMasks.MirrorLane(Lane);
            return p;
        }

        public override string ToString()
        {
            return (OverChasm ? "ChasmVine[" : "Vine[") + Lane + "]@" + Zc + " row " + Row;
        }
    }
}
