using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Immutable runtime <c>ObstacleKit</c> (spec 002 sections 3.2 and 5).</summary>
    public sealed class ObstacleKitConfig
    {
        private readonly float[] _gapLengthsM;

        private ObstacleKitConfig(ObstacleKitDesignValues v)
        {
            LowBarrier = v.LowBarrier;
            HighBarrier = v.HighBarrier;
            FullBlock = v.FullBlock;
            Mover = v.Mover;
            _gapLengthsM = (float[])v.GapLengthsM.Clone();
            GapMinWindowS = v.GapMinWindowS;
            GapRunAcrossMarginM = v.GapRunAcrossMarginM;
            GapLandingClearM = v.GapLandingClearM;
            MoverLateralSpeedMps = v.MoverLateralSpeedMps;
            MoverTriggerLeadS = v.MoverTriggerLeadS;
            MoverMinSettleS = v.MoverMinSettleS;
            MoverMaxLaneShift = v.MoverMaxLaneShift;
        }

        public ObstacleShape LowBarrier { get; }

        public ObstacleShape HighBarrier { get; }

        public ObstacleShape FullBlock { get; }

        public ObstacleShape Mover { get; }

        public int GapLengthCount => _gapLengthsM.Length;

        public float GapMinWindowS { get; }

        public float GapRunAcrossMarginM { get; }

        public float GapLandingClearM { get; }

        public float MoverLateralSpeedMps { get; }

        public float MoverTriggerLeadS { get; }

        public float MoverMinSettleS { get; }

        public int MoverMaxLaneShift { get; }

        public float GetGapLength(int index)
        {
            return _gapLengthsM[index];
        }

        /// <summary>True if <paramref name="lengthM"/> is one of the allowed gap lengths (± 1 mm).</summary>
        public bool IsAllowedGapLength(float lengthM)
        {
            for (int i = 0; i < _gapLengthsM.Length; i++)
            {
                if (Math.Abs(_gapLengthsM[i] - lengthM) <= 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Hitbox of an archetype. <see cref="ObstacleArchetype.Gap"/> and <c>None</c> have no box.</summary>
        public ObstacleShape GetShape(ObstacleArchetype archetype)
        {
            switch (archetype)
            {
                case ObstacleArchetype.LowBarrier:
                    return LowBarrier;
                case ObstacleArchetype.HighBarrier:
                    return HighBarrier;
                case ObstacleArchetype.FullBlock:
                    return FullBlock;
                case ObstacleArchetype.Mover:
                    return Mover;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "This archetype has no box.");
            }
        }

        public static ObstacleKitConfig FromDesignValues(ObstacleKitDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid obstacle kit: " + string.Join(" ", errors), nameof(values));
            }

            return new ObstacleKitConfig(values);
        }

        /// <summary>
        /// Test hook: converts without the range checks, so fixtures can use values outside the design ranges
        /// (for example a late mover trigger that pushes a mover into HERO's side, AC-209).
        /// </summary>
        internal static ObstacleKitConfig FromDesignValuesUnchecked(ObstacleKitDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            return new ObstacleKitConfig(values);
        }

        public static ObstacleKitConfig CreateDefault()
        {
            return FromDesignValues(ObstacleKitDesignValues.CreateDefault());
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = MixShape(h, LowBarrier);
            h = MixShape(h, HighBarrier);
            h = MixShape(h, FullBlock);
            h = MixShape(h, Mover);
            for (int i = 0; i < _gapLengthsM.Length; i++)
            {
                h = StableHash.Mix(h, _gapLengthsM[i]);
            }

            h = StableHash.Mix(h, GapMinWindowS);
            h = StableHash.Mix(h, GapRunAcrossMarginM);
            h = StableHash.Mix(h, GapLandingClearM);
            h = StableHash.Mix(h, MoverLateralSpeedMps);
            h = StableHash.Mix(h, MoverTriggerLeadS);
            h = StableHash.Mix(h, MoverMinSettleS);
            h = StableHash.Mix(h, MoverMaxLaneShift);
            return h;
        }

        private static ulong MixShape(ulong h, ObstacleShape s)
        {
            h = StableHash.Mix(h, s.WidthM);
            h = StableHash.Mix(h, s.DepthM);
            h = StableHash.Mix(h, s.BottomM);
            return StableHash.Mix(h, s.TopM);
        }
    }
}
