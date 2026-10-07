namespace JungleBooze.Services.Meta
{
    /// <summary>What one run counted, handed to the missions when the run is banked.</summary>
    public struct RunStats
    {
        public int Coins;
        public int DistanceM;
        public int Vines;
        public int PerfectReleases;
        public int Slides;
        public int Shields;
        public int NearMisses;

        public int ValueOf(MissionKind kind)
        {
            switch (kind)
            {
                case MissionKind.Coins:
                    return Coins;
                case MissionKind.Distance:
                    return DistanceM;
                case MissionKind.Vines:
                    return Vines;
                case MissionKind.PerfectReleases:
                    return PerfectReleases;
                case MissionKind.Slides:
                    return Slides;
                case MissionKind.Shields:
                    return Shields;
                case MissionKind.NearMisses:
                    return NearMisses;
                default:
                    return 0;
            }
        }
    }
}
