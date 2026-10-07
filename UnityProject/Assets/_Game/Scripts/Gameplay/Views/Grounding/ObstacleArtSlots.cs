using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Names, instance counts and a cached existence check for the wave 1 obstacle art slots (spec 005 15.2).
    /// <para>Authoring convention (differs from the old unit-cube <c>Obstacle_*</c> models): real size in meters, Y up,
    /// the model's origin on the ground at the middle of its footprint (x lateral, z along the path, front toward
    /// -z); the lowest point sits 0.03 to 0.12 m below y = 0. Bodies fill the hitbox exactly (no scaling by the view,
    /// the F7 grounding audit shows any mismatch). Lengths: log trunks lie along x and are 2.04 / 4.44 / 6.84 m
    /// (A, B, C for one, two, three lanes). <c>Obst_HangMat</c> (A, B, C) hangs from y = 3.05 down to 1.1 and is
    /// 2.04 x 0.5 m; <c>Obst_Limb</c> is 1 m along +x from its trunk end (the view stretches x to the span);
    /// <c>Obst_LianaTie</c> is 1 m tall hanging from y = 0 (scaled in y); <c>Obst_Wallow</c>, <c>Obst_SandBar</c> are
    /// flat decal meshes 2.4 x 2.2 m; <c>Obst_Furrow</c> is a 1 m long strip along x (stretched to the lane distance);
    /// <c>Obst_BarrelBoulder</c> is axis along z, radius 0.95 m, length 1.6 m, centered (it spins about z).
    /// Ochre marks are baked into the body models.</para>
    /// Setup-time lookups only; existence is cached so a missing model costs one Resources lookup per run.
    /// </summary>
    public static class ObstacleArtSlots
    {
        private static readonly string[] Names =
        {
            "Obst_Log_Trunk", "Obst_Log_TrunkB", "Obst_Log_TrunkC",
            "Obst_RootPlate", "Obst_RootPlateB", "Obst_Stump", "Obst_RockHump",
            "Obst_HangMat", "Obst_HangMatB", "Obst_HangMatC", "Obst_Limb", "Obst_LianaTie",
            "Obst_ButtressFin", "Obst_StandingStone", "Obst_WedgedSlab", "Obst_RootWeb",
            "Obst_BarrelBoulder", "Obst_Wallow", "Obst_Chock", "Obst_Furrow", "Obst_SandBar",
            "Obst_ThornCage", "Obst_CaneWall", "Obst_RootCrown", "Obst_ThornLitter",
            EnvironmentArt.LowBarrier, EnvironmentArt.HighBarrier, EnvironmentArt.FullBlock, EnvironmentArt.ThornPatch,
        };

        // How many instances of a slot one row can need at once (lanes, ties, variants).
        private static readonly int[] Instances =
        {
            3, 1, 1,
            1, 1, 1, 2,
            3, 3, 3, 1, 6,
            3, 3, 1, 3,
            1, 1, 1, 1, 1,
            2, 2, 1, 2,
            3, 3, 3, 2,
        };

        // 0 = not looked up yet, 1 = exists, 2 = missing.
        private static readonly byte[] State = new byte[Names.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < State.Length; i++)
            {
                State[i] = 0;
            }
        }

        /// <summary>Number of slots.</summary>
        public static int Count => Names.Length;

        public static string NameOf(ObstacleArtSlot slot)
        {
            return Names[(int)slot];
        }

        /// <summary>Instances of a slot that one rig may use at once.</summary>
        public static int InstancesOf(ObstacleArtSlot slot)
        {
            return Instances[(int)slot];
        }

        /// <summary>True when the model exists (looked up once per process and cached).</summary>
        public static bool Exists(ObstacleArtSlot slot)
        {
            int i = (int)slot;
            if (State[i] == 0)
            {
                State[i] = EnvironmentArt.Exists(Names[i]) ? (byte)1 : (byte)2;
            }

            return State[i] == 1;
        }

        /// <summary>Every slot name that exists in the project, for the start-of-run report. Allocates.</summary>
        public static List<string> FoundNames()
        {
            var found = new List<string>();
            for (int i = 0; i < Names.Length; i++)
            {
                if (Exists((ObstacleArtSlot)i))
                {
                    found.Add(Names[i]);
                }
            }

            return found;
        }
    }
}
