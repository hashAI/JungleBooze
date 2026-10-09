using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// Basic dynamic difficulty (spec 102 §6.4): a per-run skill score, the slow update of the player's skill
    /// estimate S, and the run-start effects of S. Never touches speed, movement numbers or best-distance logic.
    /// </summary>
    public static class DifficultyModel
    {
        /// <summary>
        /// S_run = 0.5·distScore + 0.3·hitScore + 0.2·travScore. With no traversal attempts the traversal term is 0
        /// [ASSUMED 2026-10-09: Part A has no vine/swim yet, so a missing measurement must not raise S].
        /// </summary>
        public static float RunScore(WorldDirectorConfig cfg, float distance, int hits, int traversalAttempts, int traversalSuccesses)
        {
            float d = Math.Max(1f, distance);
            float distScore = Clamp((float)(Math.Log(d / cfg.DistanceReference) / Math.Log(cfg.DistanceLogBase)), -1f, 1f);
            float km = Math.Max(0.001f, distance / 1000f);
            float hitScore = Clamp(1f - (hits / km), -1f, 1f);
            float travScore = 0f;
            if (traversalAttempts > 0)
            {
                float rate = traversalSuccesses / (float)traversalAttempts;
                travScore = Clamp((rate - cfg.TraversalTarget) / cfg.TraversalScale, -1f, 1f);
            }

            return (0.5f * distScore) + (0.3f * hitScore) + (0.2f * travScore);
        }

        /// <summary>S ← S + clamp(rate·(S_run − S), −maxStep, +maxStep).</summary>
        public static float UpdateSkill(WorldDirectorConfig cfg, float skill, float runScore)
        {
            float delta = Clamp(cfg.SkillRate * (runScore - skill), -cfg.SkillMaxStep, cfg.SkillMaxStep);
            return Clamp(skill + delta, -1f, 1f);
        }

        public static int BandShift(WorldDirectorConfig cfg, float skill)
        {
            return skill < cfg.SkillLow ? -1 : skill > cfg.SkillHigh ? 1 : 0;
        }

        public static int RecoveryEvery(WorldDirectorConfig cfg, float skill, int phaseValue)
        {
            if (skill < cfg.SkillLow)
            {
                return Math.Max(3, phaseValue - 1);
            }

            return skill > cfg.SkillHigh ? phaseValue + 1 : phaseValue;
        }

        public static float CueIntensity(WorldDirectorConfig cfg, float skill)
        {
            return skill < cfg.SkillLow ? cfg.CueIntensityLowSkill : skill > cfg.SkillHigh ? cfg.CueIntensityHighSkill : 1f;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }
}
