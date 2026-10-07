using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Hazards;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Vine;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Meta;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Loads the run configs. [ASSUMED] Each config is read from an asset in a <c>Resources</c> folder
    /// (<c>Assets/_Game/Config/Resources/</c>, created by the menu JungleBooze > Setup > Create Default Config Assets)
    /// if it exists and is valid; otherwise the spec 001 start values built into the code are used
    /// (<see cref="RunnerDesignValues"/> and friends). An invalid asset is reported as an error and skipped.
    /// Setup-time only (allocates).
    /// </summary>
    public static class RunConfigLoader
    {
        public const string RunnerTuningName = "RunnerTuning";
        public const string SpeedCurveName = "SpeedCurve";
        public const string InputTuningName = "InputTuning";
        public const string PresentationTuningName = "RunnerPresentationTuning";
        public const string VineTuningName = "VineTuning";
        public const string PowerUpTuningName = "PowerUpTuning";
        public const string HazardTuningName = "HazardTuning";
        public const string WorldScheduleName = "WorldSchedule";
        public const string CompanionTuningName = "CompanionTuning";
        public const string EconomyConfigName = "EconomyConfig";
        public const string MetaConfigName = "MetaConfig";
        public const string EnvironmentLookName = "EnvironmentLook";

        public static RunConfigSet Load()
        {
            var sources = new List<string>(4);

            RunnerConfig runner = LoadOrDefault<RunnerConfigAsset, RunnerConfig>(
                RunnerTuningName, a => a.ToConfig(), RunnerConfig.CreateDefault, sources);
            SpeedCurve curve = LoadOrDefault<SpeedCurveAsset, SpeedCurve>(
                SpeedCurveName, a => a.ToSpeedCurve(), SpeedCurve.CreateDefault, sources);
            InputConfig input = LoadOrDefault<InputConfigAsset, InputConfig>(
                InputTuningName, a => ValidOrThrow(a.ToConfig()), InputConfig.CreateDefault, sources);
            RunnerPresentationConfig presentation = LoadOrDefault<RunnerPresentationConfigAsset, RunnerPresentationConfig>(
                PresentationTuningName, a => ValidOrThrow(a.ToConfig()), RunnerPresentationConfig.CreateDefault, sources);

            return new RunConfigSet(runner, curve, input, presentation, string.Join(", ", sources));
        }

        /// <summary>
        /// Vine tuning (GDD 7.5) from <c>Resources/VineTuning.asset</c> if present and valid, else the built-in
        /// defaults. Kept out of <see cref="RunConfigSet"/> so the run config set stays unchanged.
        /// </summary>
        public static VineConfig LoadVines()
        {
            var sources = new List<string>(1);
            VineConfig vines = LoadOrDefault<VineConfigAsset, VineConfig>(
                VineTuningName, a => a.ToConfig(), VineConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return vines;
        }

        /// <summary>Power-up tuning (GDD 10) from <c>Resources/PowerUpTuning.asset</c> if present and valid, else defaults.</summary>
        public static PowerUpConfig LoadPowerUps()
        {
            var sources = new List<string>(1);
            PowerUpConfig config = LoadOrDefault<PowerUpConfigAsset, PowerUpConfig>(
                PowerUpTuningName, a => a.ToConfig(), PowerUpConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return config;
        }

        /// <summary>Signature hazard tuning (GDD 8.3) from <c>Resources/HazardTuning.asset</c> if present and valid, else defaults.</summary>
        public static HazardConfig LoadHazards()
        {
            var sources = new List<string>(1);
            HazardConfig config = LoadOrDefault<HazardConfigAsset, HazardConfig>(
                HazardTuningName, a => a.ToConfig(), HazardConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return config;
        }

        /// <summary>World order and lengths (GDD 9) from <c>Resources/WorldSchedule.asset</c> if present and valid, else defaults.</summary>
        public static WorldScheduleConfig LoadWorlds()
        {
            var sources = new List<string>(1);
            WorldScheduleConfig config = LoadOrDefault<WorldScheduleConfigAsset, WorldScheduleConfig>(
                WorldScheduleName, a => a.ToConfig(), WorldScheduleConfig.CreateDefault, sources);
            Debug.Log("[JungleBooze] " + sources[0] + ". " + config.Count + " worlds, first lap "
                + config.PeriodM.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " m; world starts: "
                + config.SegmentStartZ(1).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + config.SegmentStartZ(2).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + ", "
                + config.SegmentStartZ(3).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " m.");

            return config;
        }

        /// <summary>Companion tuning (GDD 15.2) from <c>Resources/CompanionTuning.asset</c> if present and valid, else defaults.</summary>
        public static CompanionConfig LoadCompanion()
        {
            var sources = new List<string>(1);
            CompanionConfig config = LoadOrDefault<CompanionConfigAsset, CompanionConfig>(
                CompanionTuningName, a => a.ToConfig(), CompanionConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return config;
        }

        /// <summary>Continue rules (GDD 14.4) from <c>Resources/EconomyConfig.asset</c> if present and valid, else defaults.</summary>
        public static ContinueRules LoadContinueRules()
        {
            var sources = new List<string>(1);
            ContinueRules rules = LoadOrDefault<EconomyConfigAsset, ContinueRules>(
                EconomyConfigName, a => a.ToContinueRules(), ContinueRules.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return rules;
        }

        /// <summary>Missions, daily reward and shop tuning (GDD 13) from <c>Resources/MetaConfig.asset</c> if present and valid, else defaults.</summary>
        public static MetaConfig LoadMeta()
        {
            var sources = new List<string>(1);
            MetaConfig config = LoadOrDefault<MetaConfigAsset, MetaConfig>(
                MetaConfigName, a => a.ToConfig(), MetaConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return config;
        }

        /// <summary>
        /// Environment look (ground, jungle walls, sky haze, key light, ambient) from <c>Resources/EnvironmentLook.asset</c>
        /// if present and valid, else the built-in defaults.
        /// </summary>
        public static EnvironmentLookConfig LoadEnvironmentLook()
        {
            var sources = new List<string>(1);
            EnvironmentLookConfig config = LoadOrDefault<EnvironmentLookConfigAsset, EnvironmentLookConfig>(
                EnvironmentLookName, a => ValidOrThrow(a.ToConfig()), EnvironmentLookConfig.CreateDefault, sources);
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] " + sources[0] + ".");
            }

            return config;
        }

        private static TConfig LoadOrDefault<TAsset, TConfig>(
            string resourceName, Func<TAsset, TConfig> convert, Func<TConfig> fallback, List<string> sources)
            where TAsset : ScriptableObject
        {
            TAsset asset = Resources.Load<TAsset>(resourceName);
            if (asset == null)
            {
                sources.Add(resourceName + ": defaults");
                return fallback();
            }

            try
            {
                TConfig config = convert(asset);
                sources.Add(resourceName + ": asset");
                return config;
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[JungleBooze] " + resourceName + " asset is invalid, using defaults: " + e.Message, asset);
                sources.Add(resourceName + ": defaults (asset invalid)");
                return fallback();
            }
        }

        private static InputConfig ValidOrThrow(InputConfig config)
        {
            var errors = new List<string>();
            if (!config.Validate(errors))
            {
                throw new ArgumentException(string.Join(" ", errors));
            }

            return config;
        }

        private static EnvironmentLookConfig ValidOrThrow(EnvironmentLookConfig config)
        {
            var errors = new List<string>();
            if (!config.Validate(errors))
            {
                throw new ArgumentException(string.Join(" ", errors));
            }

            return config;
        }

        private static RunnerPresentationConfig ValidOrThrow(RunnerPresentationConfig config)
        {
            var errors = new List<string>();
            if (!config.Validate(errors))
            {
                throw new ArgumentException(string.Join(" ", errors));
            }

            return config;
        }
    }
}
