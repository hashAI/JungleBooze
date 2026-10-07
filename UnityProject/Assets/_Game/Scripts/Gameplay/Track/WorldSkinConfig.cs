using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// FP1 subset of a world skin (spec 002 section 3.8): per-archetype display names for the Game Over cause text
    /// (section 12.4). Cause strings are built once in the constructor, so <see cref="GetCauseText"/> never allocates.
    /// </summary>
    public sealed class WorldSkinConfig
    {
        private const int ArchetypeSlots = 6;

        private readonly string[] _names = new string[ArchetypeSlots];
        private readonly string[] _hitTexts = new string[ArchetypeSlots];
        private readonly string[] _trippedTexts = new string[ArchetypeSlots];

        public WorldSkinConfig(
            string worldName,
            string lowBarrier,
            string highBarrier,
            string fullBlock,
            string mover,
            string gap,
            string fellText)
        {
            WorldName = worldName ?? string.Empty;
            _names[(int)ObstacleArchetype.None] = "obstacle";
            _names[(int)ObstacleArchetype.LowBarrier] = Require(lowBarrier, nameof(lowBarrier));
            _names[(int)ObstacleArchetype.HighBarrier] = Require(highBarrier, nameof(highBarrier));
            _names[(int)ObstacleArchetype.FullBlock] = Require(fullBlock, nameof(fullBlock));
            _names[(int)ObstacleArchetype.Mover] = Require(mover, nameof(mover));
            _names[(int)ObstacleArchetype.Gap] = Require(gap, nameof(gap));
            FellText = Require(fellText, nameof(fellText));
            for (int i = 0; i < ArchetypeSlots; i++)
            {
                _hitTexts[i] = "Hit: " + _names[i];
                _trippedTexts[i] = "Tripped twice: " + _names[i];
            }
        }

        public string WorldName { get; }

        /// <summary>Cause text for <see cref="DeathCause.Fell"/> ("Fell into a ravine" [ASSUMED wording]).</summary>
        public string FellText { get; }

        /// <summary>Jungle display names from spec 002 section 3.8.</summary>
        public static WorldSkinConfig CreateJungle()
        {
            return new WorldSkinConfig(
                "Jungle",
                "Fallen log",
                "Low branch",
                "Giant tree trunk",
                "Rolling boulder",
                "Ravine",
                "Fell into a ravine");
        }

        public string GetDisplayName(ObstacleArchetype archetype)
        {
            return _names[Slot(archetype)];
        }

        /// <summary>
        /// Spec 002 section 12.4: <c>Hit</c> → "Hit: Giant tree trunk"; <c>Hit</c> after a stumble →
        /// "Tripped twice: Fallen log"; <c>Fell</c> → <see cref="FellText"/>. Empty for <c>None</c>.
        /// </summary>
        public string GetCauseText(DeathCause cause, ObstacleArchetype archetype, bool afterStumble)
        {
            switch (cause)
            {
                case DeathCause.Fell:
                    return FellText;
                case DeathCause.Hit:
                    return afterStumble ? _trippedTexts[Slot(archetype)] : _hitTexts[Slot(archetype)];
                default:
                    return string.Empty;
            }
        }

        public string GetCauseText(in DeathInfo death)
        {
            return GetCauseText(death.Cause, death.Archetype, death.AfterStumble);
        }

        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            for (int i = 1; i < ArchetypeSlots; i++)
            {
                if (string.IsNullOrEmpty(_names[i]))
                {
                    errors.Add("displayName for " + (ObstacleArchetype)i + " is empty.");
                }
            }

            return errors.Count == before;
        }

        private static int Slot(ObstacleArchetype archetype)
        {
            int slot = (int)archetype;
            return slot >= 0 && slot < ArchetypeSlots ? slot : 0;
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A world skin display name is empty.", name);
            }

            return value;
        }
    }
}
