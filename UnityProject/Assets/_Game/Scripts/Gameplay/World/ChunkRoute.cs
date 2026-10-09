using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A route of a Branch chunk (spec 102 §3): its type and the divider sides that lead into it. A route with no
    /// steps and <see cref="Locked"/> is a visible teaser only (no path; V12: physically closed and harmless).
    /// </summary>
    [Serializable]
    public sealed class ChunkRoute
    {
        public string Name = "Route";
        public RouteType Type = RouteType.Safe;
        public List<RouteStep> Steps = new List<RouteStep>();

        /// <summary>Ability needed to use this route (spec 102 §9). Locked routes stay visible but closed.</summary>
        public AbilityFlags RequiredAbility = AbilityFlags.None;

        /// <summary>No walkable path in this build (teaser for a later phase).</summary>
        public bool Locked;

        /// <summary>Authoring note (what the lock or teaser is).</summary>
        public string Note = string.Empty;

        public ChunkRoute Clone()
        {
            var copy = (ChunkRoute)MemberwiseClone();
            copy.Steps = new List<RouteStep>(Steps);
            return copy;
        }
    }
}
