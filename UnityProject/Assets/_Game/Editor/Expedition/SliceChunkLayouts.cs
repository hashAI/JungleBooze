using System.Collections.Generic;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Editor.Expedition
{
    /// <summary>
    /// The 15 MVP chunks (spec 102 §9) with the vertical-slice layouts of spec 103 §3, the Expedition 1 script
    /// (§2, §10.1), journal entries D-01…D-04 (§7.3) and the seven abilities (GDD §13), with Part B traversal:
    /// the swim pool (C6: water, currents, water obstacles, the Sunken Arch deep dive), the vine/canopy span (C8:
    /// vines V1/V2 with perfect columns, beams A/B/C) and sailback spawns (C5 ambient, C8 flock, C9 and the grotto).
    /// View-side bends (spec 102 §2.1) on straight chunks.
    /// Layouts not given by spec 103 (pool variants of C3/C4/C9, F_Recovery_Riverbank_01, F_Branch_Ravine_01,
    /// D_Discovery_Grotto_01) are [ASSUMED 2026-10-09, gameplay-engineer] and follow the same rules.
    /// </summary>
    internal static class SliceChunkLayouts
    {
        public const string Start = "F_Start_RootGate_01";
        public const string Glade = "F_Straight_Glade_01";
        public const string Roots = "F_Straight_Roots_01";
        public const string Meadow = "F_Recovery_Meadow_01";
        public const string Riverbank = "F_Recovery_Riverbank_01";
        public const string StiltRoots = "F_Branch_StiltRoots_01";
        public const string Ravine = "F_Branch_Ravine_01";
        public const string FallenGiants = "F_Challenge_FallenGiants_01";
        public const string ThornRun = "F_Challenge_ThornRun_01";
        public const string Ford = "R_Transition_Ford_01";
        public const string Swim = "R_Swim_Pool_01";
        public const string Waterfall = "R_Branch_Waterfall_01";
        public const string Canopy = "C_Canopy_VineSpan_01";
        public const string Grotto = "D_Discovery_Grotto_01";
        public const string Overlook = "D_Discovery_Overlook_01";

        public static List<ChunkDefinition> CreateChunks()
        {
            return new List<ChunkDefinition>
            {
                StartChunk(),
                GladeChunk(),
                RootsChunk(),
                MeadowChunk(),
                RiverbankChunk(),
                StiltRootsChunk(),
                RavineChunk(),
                FallenGiantsChunk(),
                ThornRunChunk(),
                FordChunk(),
                SwimChunk(),
                WaterfallChunk(),
                CanopyChunk(),
                GrottoChunk(),
                OverlookChunk(),
            };
        }

        public static ExpeditionScript CreateScript()
        {
            var script = new ExpeditionScript { Seed = 1UL, CueIntensity = 1f, NoGapSeconds = 60f };
            Add(script, Start, "Full", DifficultyPhase.Learning, "C1 Forest, steering");
            Add(script, Roots, "Learn", DifficultyPhase.Learning, "C2 Obstacles: jump, slide, dodge");
            Add(script, StiltRoots, "Slice", DifficultyPhase.Learning, "C3 First route choice");
            Add(script, Glade, "Slice", DifficultyPhase.Rhythm, "C4 Rhythm, first gap");
            Add(script, Ford, "Default", DifficultyPhase.Rhythm, "C5 River (ford)");
            Add(script, Swim, "Default", DifficultyPhase.Rhythm, "C6 Swimming");
            Add(script, Overlook, "Default", DifficultyPhase.Rhythm, "C7 Waterfall vista");
            Add(script, Canopy, "Default", DifficultyPhase.Rhythm, "C8 Vine + canopy + creature");
            Add(script, Waterfall, "Slice", DifficultyPhase.Decision, "C9 Secret behind the falls");
            Add(script, Roots, "B", DifficultyPhase.Decision, "C10 Rhythm");
            script.Entries.Add(new ExpeditionScriptEntry { ChunkId = Meadow, Variant = "Default", RulesPhase = DifficultyPhase.Decision, PowerUpSlot = 0, PowerUp = PowerUpKind.Shield, Beat = "C11 Breather + Shield" });
            Add(script, FallenGiants, "Default", DifficultyPhase.Challenge, "C12 Difficult section, part 1");
            Add(script, ThornRun, "Default", DifficultyPhase.Challenge, "C13 Difficult section, part 2");
            return script;
        }

        public static List<DiscoveryEntry> CreateDiscoveries()
        {
            return new List<DiscoveryEntry>
            {
                new DiscoveryEntry { Id = "D-01", Name = "Falls Basin", Category = DiscoveryCategory.Location, Rarity = "common", ToastText = "Falls Basin · Added to Journal" },
                new DiscoveryEntry { Id = "D-02", Name = "Sailback", Category = DiscoveryCategory.Creature, Rarity = "uncommon", ToastText = "Unknown Species · Added to Journal", MissedHint = "1 unknown creature near the falls" },
                new DiscoveryEntry { Id = "D-03", Name = "Veil Grotto", Category = DiscoveryCategory.Location, Rarity = "rare", Secret = true, ToastText = "Veil Grotto · Added to Journal", MissedHint = "Something glinted behind Veil Falls" },
                new DiscoveryEntry { Id = "D-04", Name = "Sunken Arch", Category = DiscoveryCategory.Location, Rarity = "rare", Secret = true, ToastText = "Sunken Arch · Added to Journal", MissedHint = "A glinting passage lies under the river" },
            };
        }

        public static List<AbilityDefinition> CreateAbilities()
        {
            return new List<AbilityDefinition>
            {
                new AbilityDefinition { Ability = AbilityFlags.DeepBreath, Name = "Deep Breath", Order = 1, CostCoins = 150, CostCrystals = 0, Implemented = true, Line = "Dive deep at shimmering water to reach sunken passages", ObjectiveLine = "Dive into the Sunken Arch" },
                Later(AbilityFlags.VineGrip, "Vine Grip", 2, 600, 2, "Reach the high vine line in the canopy"),
                Later(AbilityFlags.TrailSense, "Trail Sense", 3, 1200, 4, "Secret cues pulse when you are close"),
                Later(AbilityFlags.RootVault, "Root Vault", 4, 2000, 6, "Vault onto raised roots to reach high ledges"),
                Later(AbilityFlags.CreatureTracking, "Creature Tracking", 5, 3000, 8, "Follow the tracks and calls of nearby creatures"),
                Later(AbilityFlags.ShoulderCharge, "Shoulder Charge", 6, 4500, 12, "Burst through brittle root walls while sliding"),
                Later(AbilityFlags.DoubleJump, "Double Jump", 7, 7000, 20, "A second jump in the air"),
            };
        }

        private static AbilityDefinition Later(AbilityFlags ability, string name, int order, int coins, int crystals, string line)
        {
            return new AbilityDefinition { Ability = ability, Name = name, Order = order, CostCoins = coins, CostCrystals = crystals, Implemented = false, Line = line, ObjectiveLine = line, Showcase = true };
        }

        private static void Add(ExpeditionScript script, string id, string variant, DifficultyPhase rules, string beat)
        {
            script.Entries.Add(new ExpeditionScriptEntry { ChunkId = id, Variant = variant, RulesPhase = rules, PowerUpSlot = -1, PowerUp = PowerUpKind.None, Beat = beat });
        }

        private static ChunkDefinition Def(string id, string label, ChunkCategory category, EnvironmentSet env, float length, int rating, int min, int max, DifficultyPhase pMin, DifficultyPhase pMax, ChunkDimensions dims)
        {
            return new ChunkDefinition
            {
                Id = id,
                Label = label,
                Category = category,
                Environment = env,
                Length = length,
                Rating = rating,
                RatingMin = min,
                RatingMax = max,
                PhaseMin = pMin,
                PhaseMax = pMax,
                Dimensions = dims,
            };
        }

        // ---- C1 · spec 103 §3.1 ----
        private static ChunkDefinition StartChunk()
        {
            ChunkDefinition d = Def(Start, "C1", ChunkCategory.Straight, EnvironmentSet.Forest, 160f, 1, 1, 1, DifficultyPhase.Learning, DifficultyPhase.Rhythm, default);
            d.RunOpener = true;
            var full = new ChunkLayoutBuilder("Full", 160f);
            full.Width(6f, -3.5f, 3.5f).Width(25f, -4f, 4f).Width(148f, -4f, 4f).Width(154f, -3.5f, 3.5f)
                .Line(20f, 58f, 0f, 4f)
                .Weave(60f, 130f, 2.5f, 35f)
                .Blk(140f, 1.4f, 0f, "Mossy boulder")
                .Help(HelpMove.Steer, 140f, -0.7f, 0.7f)
                .Line(134f, 146f, -2f, 3f)
                // The curving trail of keyframe F1 (view-side only).
                .Bend(20f, 0f).Bend(35f, -1f / 120f).Bend(70f, -1f / 120f).Bend(85f, 0f).Bend(95f, 1f / 150f).Bend(125f, 1f / 150f).Bend(138f, 0f);
            d.Variants.Add(full.Variant);

            // Run 2+: 60 m, the arch and a coin line, no blocker (spec 103 §3.1, §10.2).
            var shortStart = new ChunkLayoutBuilder("Short", 160f, false, 60f);
            shortStart.Width(6f, -3.5f, 3.5f).Width(25f, -4f, 4f).Width(44f, -4f, 4f).Width(54f, -3.5f, 3.5f)
                .Line(12f, 52f, 0f, 4f)
                .Bend(12f, 0f).Bend(22f, -1f / 150f).Bend(42f, -1f / 150f).Bend(52f, 0f);
            d.Variants.Add(shortStart.Variant);
            return d;
        }

        // ---- C2 / C10 · spec 103 §3.2, §3.10 ----
        private static ChunkDefinition RootsChunk()
        {
            ChunkDefinition d = Def(Roots, "C2", ChunkCategory.Straight, EnvironmentSet.Forest, 220f, 3, 2, 5, DifficultyPhase.Rhythm, DifficultyPhase.Mastery, new ChunkDimensions(2, 1, 0, 1, 1));

            var learn = new ChunkLayoutBuilder("Learn", 220f, true);
            learn.Low(20f, 0.5f, label: "Root").Help(HelpMove.Jump, 20f).Arc(20f)
                .Line(27f, 39f, 0f, 3f)
                .Low(45f, 0.6f, label: "Root").Arc(45f)
                .Line(52f, 64f, 0f, 3f)
                .Low(70f, 0.9f, walk: true, label: "Fallen log").Line(70f, 72f, 0f, 1f)
                .Line(80f, 94f, 0f, 3.5f)
                .High(100f, label: "Hanging branch").Help(HelpMove.Slide, 100f).Under(100f)
                .Line(108f, 120f, 0f, 3f)
                .High(125f, label: "Hanging branch").Under(125f)
                .BlkSpan(160f, -3.5f, 0.5f, "Root wall left").Help(HelpMove.Dodge, 160f, -3.5f, 0.5f).Line(150f, 175f, 2f, 3f)
                .BlkSpan(180f, -0.5f, 3.5f, "Root wall right").Line(176f, 190f, -2f, 2.8f)
                .Low(200f, 0.5f, -3.5f, 0f, label: "Root (left)").Arc(200f, -1.8f);
            d.Variants.Add(learn.Variant);

            var b = new ChunkLayoutBuilder("B", 220f, false, 200f);
            b.Low(20f, 0.6f).Arc(20f)
                .High(40f).Under(40f)
                .Blk(60f, 1.2f, 1.2f).Line(50f, 70f, -1.5f, 2.5f)
                .Low(80f, 0.6f).Arc(80f)
                .High(92f).Under(92f)
                .Gap(115f, 118.5f).Arc(116.75f)
                .Blk(135f, 1.4f, -1.5f).Blk(150f, 1.4f, 1.5f).Blk(165f, 1.4f, -1f).Weave(128f, 172f, 1.6f, 30f, 0.2f)
                .Low(185f, 0.5f).Arc(185f)
                .PowerUp(100f, 0f, 1.0f, false)
                .Bend(25f, 0f).Bend(40f, 1f / 200f).Bend(100f, 1f / 200f).Bend(112f, 0f);
            d.Variants.Add(b.Variant);
            return d;
        }

        // ---- C3 · spec 103 §3.3 ----
        private static ChunkDefinition StiltRootsChunk()
        {
            ChunkDefinition d = Def(StiltRoots, "C3", ChunkCategory.Branch, EnvironmentSet.Forest, 220f, 3, 3, 6, DifficultyPhase.Rhythm, DifficultyPhase.Mastery, new ChunkDimensions(1, 2, 1, 2, 1));
            d.ShowcaseAbilities = AbilityFlags.None;
            d.Variants.Add(StiltRootsVariant("Slice", true, false).Variant);
            d.Variants.Add(StiltRootsVariant("A", false, true).Variant);
            return d;
        }

        private static ChunkLayoutBuilder StiltRootsVariant(string name, bool script, bool swapRisky)
        {
            const float ridge = -2.8f;
            var v = new ChunkLayoutBuilder(name, 220f, script);
            v.Width(20f, -3.5f, 3.5f).Width(50f, -4.5f, 4.5f).Width(60f, -4.5f, 4.5f).Width(80f, -4f, 4.5f)
                .Width(160f, -4f, 4.5f).Width(170f, -4.5f, 4.5f).Width(200f, -3.5f, 3.5f)
                .Divider("D1 stiltwood root wall", 60f, 170f, -1.6f, -0.4f, 1)
                .Route("Risky ridge", RouteType.Risky, ChunkLayoutBuilder.Step(0, -1))
                .Route("Safe", RouteType.Safe, ChunkLayoutBuilder.Step(0, 1))
                .LockedRoute("Secret (Trail Sense)", RouteType.Secret, AbilityFlags.TrailSense, "Veilmoss curtain over a sealed rootstone crack at s 100–130, x > +4.5 (Phase 3)")
                // Risky: raised root ridge +1.2 m (12%).
                .Floor(62f, 72f, 0f, 1.2f, -4.6f, -1.6f).Floor(72f, 160f, 1.2f, 1.2f, -4.6f, -1.6f).Floor(160f, 170f, 1.2f, 0f, -4.6f, -1.6f);
            if (!swapRisky)
            {
                v.Low(95f, 0.5f, -4.5f, -1.6f).High(120f, 1.0f, -4.5f, -1.6f).Low(148f, 0.6f, -4.5f, -1.6f);
            }
            else
            {
                v.High(95f, 1.0f, -4.5f, -1.6f).Low(120f, 0.6f, -4.5f, -1.6f).Low(148f, 0.5f, -4.5f, -1.6f);
            }

            v.Crystal(148.3f, ridge, 1.6f, script)
                .Line(75f, 158f, ridge, 1.5f)
                // Safe: flat, one partial root (steer left or jump).
                .Low(115f, 0.4f, swapRisky ? -0.4f : 0.5f, swapRisky ? 3.0f : 4.5f)
                .Line(65f, 165f, 1.5f, 4f)
                .Line(178f, 196f, 0f, 3f);
            if (!script)
            {
                v.PowerUp(132f, ridge, 0.9f, true);
            }

            return v;
        }

        // ---- C4 · spec 103 §3.4 ----
        private static ChunkDefinition GladeChunk()
        {
            ChunkDefinition d = Def(Glade, "C4", ChunkCategory.Straight, EnvironmentSet.Forest, 200f, 3, 1, 3, DifficultyPhase.Learning, DifficultyPhase.Mastery, new ChunkDimensions(2, 0, 1, 1, 1));
            d.Variants.Add(GladeVariant("Slice", true, 82f).Variant);

            // Pool: the High→Low combo spaced for Learning (1.2 s at 10.9 m/s).
            d.Variants.Add(GladeVariant("A", false, 85f).PowerUp(160f, 1.5f, 1.0f, false).Variant);
            return d;
        }

        private static ChunkLayoutBuilder GladeVariant(string name, bool script, float lowAfterHigh)
        {
            var v = new ChunkLayoutBuilder(name, 200f, script);
            v.Line(8f, 18f, 0f, 2.5f)
                .Low(25f, 0.6f).Arc(25f)
                .Gap(45f, 47.5f).Arc(46.25f, 0f, 4)
                .High(70f).Under(70f)
                .Low(lowAfterHigh, 0.6f).Arc(lowAfterHigh)
                .Blk(105f, 1.2f, 1.0f).Blk(120f, 1.2f, -1.5f).Weave(95f, 130f, -1.8f, 30f)
                .Thorns(145f, 150f, -3.5f, -1.0f).Line(140f, 165f, 1.5f, 2.5f)
                .Gap(170f, 173f).Arc(171.5f)
                .Bend(55f, 0f).Bend(62f, -1f / 180f).Bend(95f, -1f / 180f).Bend(104f, 0f);
            if (script)
            {
                v.Help(HelpMove.Gap, 45f);
            }

            return v;
        }

        // ---- C5 · spec 103 §3.5 ----
        private static ChunkDefinition FordChunk()
        {
            ChunkDefinition d = Def(Ford, "C5", ChunkCategory.Transition, EnvironmentSet.River, 220f, 2, 2, 4, DifficultyPhase.Learning, DifficultyPhase.Mastery, new ChunkDimensions(1, 1, 0, 0, 0));
            var v = new ChunkLayoutBuilder("Default", 220f);
            v.Zone(TraversalMode.ShallowWater, 60f, 180f, -3.5f, 3.5f, 0f, false, "Ankle-deep ford: ground with splash footsteps, no speed change. The 1.0 m descent to river level (6–60) is visual only")
                .Blk(80f, 1.4f, -1.5f, "Boulder").Blk(100f, 1.4f, 1.8f, "Boulder").Blk(120f, 1.6f, -0.5f, "Boulder")
                .Weave(70f, 135f, 2.0f, 40f)
                .Low(140f, 0.5f, walk: true, label: "Driftwood").Line(140f, 142f, 0f, 1f)
                .Creature("D-02", 150f, -22f, 8f, 2, 2.5f, 15f, 26f, 9f, 52f, true, "Ambient sailback crossing 45–60 m ahead at y ≥ 8 m (foreshadowing; outside the discovery volume)")
                .Line(185f, 205f, 0f, 5f)
                .Bend(10f, 0f).Bend(25f, 1f / 140f).Bend(55f, 1f / 140f).Bend(70f, 0f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C6 · spec 103 §3.6 ----
        private static ChunkDefinition SwimChunk()
        {
            ChunkDefinition d = Def(Swim, "C6", ChunkCategory.Straight, EnvironmentSet.River, 320f, 3, 3, 6, DifficultyPhase.Learning, DifficultyPhase.Mastery, new ChunkDimensions(1, 1, 3, 1, 1));
            d.ShowcaseAbilities = AbilityFlags.DeepBreath;
            var v = new ChunkLayoutBuilder("Default", 320f);
            v.Width(6f, -3.5f, 3.5f).Width(30f, -4.5f, 4.5f).Width(290f, -4.5f, 4.5f).Width(314f, -3.5f, 3.5f)
                // Riverbed (the depth profile): bank 6–30 to −0.9 (swim from 30), deep −1.8 from 45, rises 290–306 (wade out ≈ 300).
                .Floor(6f, 30f, 0f, -0.9f).Floor(30f, 45f, -0.9f, -1.8f).Floor(45f, 290f, -1.8f, -1.8f).Floor(290f, 306f, -1.8f, 0f)
                .Water(6f, 312f, -4.5f, 4.5f)
                .Current(40f, 120f, 1.5f, 0f)
                .Current(140f, 200f, 0f, 3.0f)
                .Line(45f, 115f, -2.5f, 2f, 0.4f)
                .FloatingLog(60f, label: "FloatingLog").Help(HelpMove.Dive, 60f)
                .FloatingLog(90f, -4.5f, 0.5f, "Debris mat")
                .Snag(115f, label: "Snag").Help(HelpMove.Leap, 115f)
                .Rock(150f, 1.4f, 1.5f).Rock(165f, 1.4f, -1.5f).Rock(180f, 1.4f, 0.5f)
                .Weave(140f, 200f, -1.6f, 30f, 0f, 2.5f)
                .Line(230f, 240f, 0f, 1.5f, -1.1f)
                .DeepDive(214f, 222f, -3.5f, -0.5f, 262f, -2.0f, "Sunken Arch: 2.5 m down under the rootstone arch; surfaces at s 262, x −2.0")
                .Crystal(235f, -2.0f, -1.9f, true).Crystal(248f, -2.0f, -1.75f, true)
                .LockedRoute("Sunken Arch (Deep Breath)", RouteType.Secret, AbilityFlags.DeepBreath, "Deep dive at 214–222, x −3.5…−0.5 (spec 103 §4.5)")
                .Discovery("D-04", 255f, -3.5f, -0.5f, false, AbilityFlags.DeepBreath, "Crossed during the Deep Breath dive (spec 103 §3.6)", deepDiveOnly: true)
                .LowBranch(255f, -1.0f, 4.5f, "LowBranch")
                .FloatingLog(270f, label: "FloatingLog")
                .Line(276f, 286f, 0f, 2.5f, 0.4f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C7 · spec 103 §3.7 ----
        private static ChunkDefinition OverlookChunk()
        {
            ChunkDefinition d = Def(Overlook, "C7", ChunkCategory.Discovery, EnvironmentSet.Waterfall, 180f, 1, 1, 3, DifficultyPhase.Learning, DifficultyPhase.Mastery, new ChunkDimensions(0, 0, 0, 0, 0));
            var v = new ChunkLayoutBuilder("Default", 180f);
            v.Line(10f, 60f, 0f, 3f)
                .Discovery("D-01", 90f, vista: true)
                .Line(70f, 120f, 0f, 6f)
                .Low(130f, 0.5f, label: "Wet rock").Arc(130f)
                // 150–174: the path turns toward the giant stiltwood beside the falls (view-side).
                .Bend(140f, 0f).Bend(150f, 1f / 80f).Bend(166f, 1f / 80f).Bend(173f, 0f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C8 · spec 103 §3.8 ----
        private static ChunkDefinition CanopyChunk()
        {
            ChunkDefinition d = Def(Canopy, "C8", ChunkCategory.Branch, EnvironmentSet.Canopy, 300f, 4, 4, 7, DifficultyPhase.Rhythm, DifficultyPhase.Mastery, new ChunkDimensions(1, 1, 3, 2, 2));
            d.SetPiece = true;
            const float top = 9f;
            var v = new ChunkLayoutBuilder("Default", 300f);

            // [ASSUMED 2026-10-09] Beam C centred at x −0.2 (spec §3.8 says −0.6): B (+0.8) → C must be ≤ maxBeamOffset 1.0 (V14).
            v.Width(20f, -3.5f, 3.5f).Width(60f, -1.5f, 1.5f).Width(70f, -1.2f, 1.2f)
                .Width(174.99f, -1.2f, 1.2f).Width(175f, -1.2f, 1.8f).Width(178.49f, -1.2f, 1.8f).Width(178.5f, -0.2f, 1.8f)
                .Width(204.99f, -0.2f, 1.8f).Width(205f, -1.4f, 1.8f).Width(207.99f, -1.4f, 1.8f).Width(208f, -1.4f, 1.0f)
                .Width(240f, -1.4f, 1.0f).Width(290f, -3.5f, 3.5f)
                // Floors: root ramp up, open air from 70 to 240, then the takeoff funnel, landing branch and beams A/B/C.
                .Floor(20f, 70f, 0f, top).Gap(70f, 240f)
                .Floor(70f, 96f, top, top, -1.2f, 1.2f)
                .Floor(104f, 140f, top, top, -1.2f, 1.2f)
                .Floor(148f, 175f, top, top, -1.2f, 1.2f)
                .Floor(178.5f, 205f, top, top, -0.2f, 1.8f)
                .Floor(208f, 240f, top, top, -1.4f, 1.0f)
                .Floor(240f, 290f, top, 0f)
                .Zone(TraversalMode.Canopy, 148f, 240f, -1.4f, 1.8f, 0.25f, false, "Canopy beams A/B/C: landing assist 0.25 m; falls hold the camera 0.8 s")
                .Vine(99f, 0f, 96f, 104f, 108.6f, 109.6f, true)
                .Vine(143f, 0f, 140f, 148f, 152f, 153f, false)
                .LockedRoute("Upper vine line (Vine Grip)", RouteType.Risky, AbilityFlags.VineGrip, "Gold-lit vines 3 m above reach on the left, s 140–235 (Phase 3)")
                .Line(25f, 65f, 0f, 4f)
                .Line(72f, 92f, 0f, 4f)
                .Point(108.8f, 0f, 2.6f).Point(108.8f, 0f, 3.4f).Point(108.8f, 0f, 4.2f)
                .Line(112f, 136f, 0f, 2f)
                .Point(152.6f, 0f, 2.6f).Point(152.6f, 0f, 4.2f).Crystal(152.6f, 0f, 3.4f, true)
                .Line(156f, 157.5f, 0f, 1.5f)
                .High(160f, 1.0f, label: "Hanging moss").Under(160f)
                .Line(164f, 172f, 0f, 2f)
                .Arc(176.75f, 0.8f, 3)
                .Line(181f, 189f, 0.8f, 2f)
                .Low(192f, 0.5f, walk: true, label: "Branch knot").Line(192f, 194f, 0.8f, 1f)
                .Line(197f, 202f, 0.8f, 2.5f)
                .Arc(206.5f, -0.2f, 3)
                .Line(211f, 218f, -0.2f, 3.5f)
                .High(222f, 1.0f, label: "Hanging moss").Under(222f, 3, -0.2f)
                .Line(228f, 238f, -0.2f, 2.5f)
                .Line(250f, 285f, 0f, 5f)
                .Creature("D-02", 172f, 2.8f, 1.5f, 3, 4.5f, 243f, 4.8f, -6.0f, 0f, false, "Sailback perch on a side branch; the flock glides ahead and right, into Veil Falls (C9 s 119)")
                // The descent spirals down the trunk (view-side bend).
                .Bend(240f, 0f).Bend(250f, 1f / 60f).Bend(280f, 1f / 60f).Bend(290f, 0f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C9 · spec 103 §3.9 ----
        private static ChunkDefinition WaterfallChunk()
        {
            ChunkDefinition d = Def(Waterfall, "C9", ChunkCategory.Branch, EnvironmentSet.Waterfall, 240f, 4, 4, 8, DifficultyPhase.Rhythm, DifficultyPhase.Mastery, new ChunkDimensions(1, 2, 1, 2, 2));
            d.Variants.Add(WaterfallVariant("Slice", true).Variant);
            d.Variants.Add(WaterfallVariant("A", false).PowerUp(136f, -3.45f, 0.9f, true).Variant);
            return d;
        }

        private static ChunkLayoutBuilder WaterfallVariant(string name, bool script)
        {
            const float risky = -3.45f;
            var v = new ChunkLayoutBuilder(name, 240f, script);
            v.Width(40f, -3.5f, 3.5f).Width(60f, -4.5f, 4.5f).Width(90f, -4.5f, 4.5f).Width(103f, -4.5f, 5.8f)
                .Width(165f, -4.5f, 5.8f).Width(180f, -4.5f, 4.5f).Width(200f, -3.5f, 3.5f)
                .Divider("D1 waterfall pillar", 70f, 180f, -2.4f, -1.2f, 1)
                .Divider("D2 rock pillar", 110f, 165f, 2.6f, 3.8f, -1)
                .Route("Risky stepping rocks", RouteType.Risky, ChunkLayoutBuilder.Step(0, -1))
                .Route("Safe pool edge", RouteType.Safe, ChunkLayoutBuilder.Step(0, 1), ChunkLayoutBuilder.Step(1, -1))
                .Route("Veil Grotto", RouteType.Secret, ChunkLayoutBuilder.Step(0, 1), ChunkLayoutBuilder.Step(1, 1))
                // Risky: wet stepping rocks across the plunge pool.
                .Gap(100f, 103f, -4.6f, -2.4f).Low(112f, 0.6f, -4.5f, -2.4f).Gap(125f, 128.5f, -4.6f, -2.4f).High(145f, 1.0f, -4.5f, -2.4f)
                .Crystal(155f, risky, 1.0f, script)
                .Line(75f, 98f, risky, 1.5f).Arc(101.5f, risky).Line(104f, 123f, risky, 1.5f).Arc(126.75f, risky).Line(130f, 168f, risky, 1.5f)
                // Safe: along the pool's edge.
                .Low(130f, 0.5f, -1.2f, 1.0f).Line(75f, 165f, 1.8f, 3f)
                // Secret: Veil Grotto behind the water curtain (observation only).
                .Zone(TraversalMode.Curtain, 118f, 121f, 3.8f, 5.8f, 0f, false, "Water curtain: no collision, splash burst, 0.25 s droplet overlay")
                .Creature("D-02", 108f, 3.2f, 3.0f, 1, 0f, 11.5f, 4.8f, 2.7f, 25f, false, "C9's own sailback perched on D2 launches at 25 m and glides into the curtain (cue 1)")
                .Creature("D-02", 143f, 5.3f, 2.8f, 2, 4f, 0f, 5.3f, 2.8f, -1f, false, "Roosting sailbacks inside the grotto (seen-again flavour)")
                .Crystal(135f, 4.8f, 1.0f, true).Crystal(150f, 4.8f, 1.0f, true)
                .Discovery("D-03", 140f, 3.8f, 5.8f)
                .Line(125f, 160f, 4.8f, 2f)
                .Line(184f, 200f, 0f, 4f);
            return v;
        }

        // ---- C11 · spec 103 §3.11 ----
        private static ChunkDefinition MeadowChunk()
        {
            ChunkDefinition d = Def(Meadow, "C11", ChunkCategory.Recovery, EnvironmentSet.Forest, 120f, 1, 1, 1, DifficultyPhase.Learning, DifficultyPhase.Mastery, default);
            var v = new ChunkLayoutBuilder("Default", 120f);
            v.Weave(10f, 110f, 1.5f, 40f, 0f, 4f)
                .PowerUp(61f, 2.5f, 2.3f, false);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- Riverbank (not scripted; second Recovery chunk) [ASSUMED layout] ----
        private static ChunkDefinition RiverbankChunk()
        {
            ChunkDefinition d = Def(Riverbank, "R1", ChunkCategory.Recovery, EnvironmentSet.River, 120f, 1, 1, 2, DifficultyPhase.Learning, DifficultyPhase.Mastery, default);
            var v = new ChunkLayoutBuilder("Default", 120f);
            v.Zone(TraversalMode.ShallowWater, 30f, 90f, 1.5f, 3.5f, 0f, false, "Shallow edge on the right")
                .Weave(10f, 110f, 2.0f, 50f, 0f, 4f)
                .PowerUp(70f, -2f, 1.0f, false);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C12 · spec 103 §3.12 ----
        private static ChunkDefinition FallenGiantsChunk()
        {
            ChunkDefinition d = Def(FallenGiants, "C12", ChunkCategory.Challenge, EnvironmentSet.Forest, 240f, 6, 5, 8, DifficultyPhase.Challenge, DifficultyPhase.Mastery, new ChunkDimensions(3, 1, 1, 3, 2));
            var v = new ChunkLayoutBuilder("Default", 240f);
            v.Low(15f, 0.9f, walk: true, depth: 5f, label: "Fallen trunk").Line(15f, 20f, 0f, 1.25f)
                .High(28f, label: "Trunk arch").Under(28f)
                .Low(38f, 0.6f).Arc(38f)
                .Gap(52f, 56f).Arc(54f)
                .Blk(66f, 2.0f, -1.0f, "Root pillar").Blk(66f, 2.0f, 2.5f, "Root pillar").Line(62f, 70f, -2.75f, 2f).Line(62f, 70f, 0.75f, 2f)
                .High(78f).Under(78f)
                .Thorns(90f, 90.6f, -3.5f, 0f).Low(90f, 0.5f, 0f, 3.5f).Arc(90f, 1.75f)
                .Gap(104f, 108f, -3.6f, 1.0f).Line(100f, 112f, 2.25f, 2f)
                .Low(118f, 0.6f).Arc(118f)
                .High(129f).Under(129f)
                .Blk(142f, 1.6f, 0f).Line(136f, 148f, -2.2f, 3f).Line(136f, 148f, 2.2f, 3f)
                .Gap(156f, 160.5f).Arc(158.25f)
                .High(168f).Under(168f)
                .Low(178f, 0.6f).Arc(178f)
                .BlkSpan(192f, -3.5f, 0.5f, "Root wall left").Line(186f, 196f, 2.0f, 2.5f)
                .BlkSpan(204f, -0.5f, 3.5f, "Root wall right").Line(198f, 208f, -2.0f, 2.5f)
                .Gap(218f, 221.5f).Arc(219.75f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- C13 · spec 103 §3.13 ----
        private static ChunkDefinition ThornRunChunk()
        {
            ChunkDefinition d = Def(ThornRun, "C13", ChunkCategory.Challenge, EnvironmentSet.Forest, 200f, 6, 6, 9, DifficultyPhase.Challenge, DifficultyPhase.Mastery, new ChunkDimensions(3, 2, 0, 3, 2));
            var v = new ChunkLayoutBuilder("Default", 200f);
            v.Width(15f, -3.5f, 3.5f).Width(35f, -1.5f, 1.5f).Width(85f, -1.5f, 1.5f).Width(100f, -3.5f, 3.5f)
                .Thorns(25f, 26.5f, -1.5f, 0f).Thorns(35f, 36.5f, 0f, 1.5f).Thorns(45f, 46.5f, -1.5f, 0f).Weave(20f, 50f, 0.8f, 20f, 0f, 2f)
                .High(60f).Under(60f)
                .Gap(75f, 78f).Arc(76.5f)
                .Blk(92f, 1.4f, -2.0f).Blk(101f, 1.4f, 1.0f).Blk(110f, 1.4f, -1.0f)
                .Line(90f, 94f, 0.5f, 2f).Line(99f, 103f, -1.5f, 2f).Line(108f, 112f, 1.0f, 2f)
                .Low(125f, 0.8f, walk: true, label: "Thorny log").Line(125f, 127f, 0f, 1f)
                .High(137f).Under(137f)
                .Gap(150f, 154.5f).Arc(152.25f)
                .Thorns(165f, 165.6f, height: 0.6f).Arc(165f)
                .High(178f).Under(178f)
                .Line(182f, 192f, 0f, 2.5f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- #6 Ravine (Phase 3; authored, not in the Phase 2 pool) [ASSUMED layout] ----
        private static ChunkDefinition RavineChunk()
        {
            ChunkDefinition d = Def(Ravine, "R6", ChunkCategory.Branch, EnvironmentSet.Forest, 220f, 5, 4, 7, DifficultyPhase.Decision, DifficultyPhase.Mastery, new ChunkDimensions(2, 1, 1, 2, 2));
            d.PoolEnabled = false;
            var v = new ChunkLayoutBuilder("Default", 220f);
            v.LockedRoute("High ledge (Root Vault)", RouteType.Risky, AbilityFlags.RootVault, "Raised root ledge ≤ 1.6 m on the right (Phase 3)")
                .Low(30f, 0.6f).Arc(30f)
                .Blk(55f, 1.4f, 1.5f).Blk(70f, 1.4f, -1.5f).Weave(48f, 78f, -1.5f, 30f)
                .Gap(95f, 98.5f).Arc(96.75f)
                .High(118f).Under(118f)
                .Low(140f, 0.6f).Arc(140f)
                .Thorns(160f, 165f, 0.5f, 3.5f).Line(155f, 170f, -2f, 2.5f)
                .Gap(185f, 188f).Arc(186.5f);
            d.Variants.Add(v.Variant);
            return d;
        }

        // ---- #13 Grotto (Phase 3; authored, not in the Phase 2 pool) [ASSUMED layout] ----
        private static ChunkDefinition GrottoChunk()
        {
            ChunkDefinition d = Def(Grotto, "D13", ChunkCategory.Discovery, EnvironmentSet.Forest, 200f, 3, 2, 5, DifficultyPhase.Rhythm, DifficultyPhase.Challenge, new ChunkDimensions(1, 1, 0, 1, 1));
            d.PoolEnabled = false;
            var v = new ChunkLayoutBuilder("Default", 200f);
            v.LockedRoute("Secret chamber (Shoulder Charge)", RouteType.Secret, AbilityFlags.ShoulderCharge, "Brittle root wall on the left at s 90 (Phase 3)")
                .Line(10f, 60f, 0f, 3f)
                .Low(70f, 0.5f).Arc(70f)
                .High(110f).Under(110f)
                .Blk(140f, 1.4f, 0f).Line(134f, 146f, -2f, 3f)
                .Line(155f, 190f, 0f, 3.5f);
            d.Variants.Add(v.Variant);
            return d;
        }
    }
}
