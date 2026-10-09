using System;

namespace JungleBooze.Core.Save
{
    /// <summary>One discovered journal entry (GDD §15): id, how often it was seen, and the run it was first found in.</summary>
    [Serializable]
    public sealed class JournalRecord
    {
        public string id;
        public int sightings;
        public int firstRun;

        public JournalRecord Clone()
        {
            return (JournalRecord)MemberwiseClone();
        }
    }
}
