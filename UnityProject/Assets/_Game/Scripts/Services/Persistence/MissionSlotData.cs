using System;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// One mission of the current set as stored in the save. The public lower-case field names are the JSON format:
    /// never rename them. The target and reward are copied from the config when the set is made, so a later config
    /// change does not move a mission the player is working on.
    /// </summary>
    [Serializable]
    public sealed class MissionSlotData
    {
        /// <summary><c>MissionKind</c> as a number.</summary>
        public int kind;

        public bool perRun;

        public int target = 1;

        public int progress;

        public bool completed;

        /// <summary>Coins paid when the mission completes.</summary>
        public int reward;
    }
}
