using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Bots
{
    /// <summary>Which side of a route split a bot steers to (route choice by steering, spec 102 §3.3).</summary>
    public interface IForkPreference
    {
        /// <summary>−1 left, +1 right, 0 = no opinion (the bot's default).</summary>
        int PreferredSide(in ForkPoint fork);
    }
}
