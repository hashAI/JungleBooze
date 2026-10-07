using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;
using JungleBooze.Gameplay.Views;
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
