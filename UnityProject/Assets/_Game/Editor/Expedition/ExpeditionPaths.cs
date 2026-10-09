namespace JungleBooze.Editor.Expedition
{
    /// <summary>Asset locations of the vertical slice (spec 103 §14).</summary>
    public static class ExpeditionPaths
    {
        public const string ConfigRoot = "Assets/_Game/Config";
        public const string ChunkFolder = ConfigRoot + "/Chunks";
        public const string WorldFolder = ConfigRoot + "/World";
        public const string RewardsFolder = ConfigRoot + "/Rewards";
        public const string DiscoveryFolder = ConfigRoot + "/Discovery";
        public const string ProgressionFolder = ConfigRoot + "/Progression";
        public const string UiFolder = ConfigRoot + "/UI";
        public const string Catalog = WorldFolder + "/ChunkCatalog.asset";
        public const string Script = WorldFolder + "/ExpeditionScript.asset";
        public const string Director = WorldFolder + "/WorldDirectorConfig.asset";
        public const string Content = WorldFolder + "/ExpeditionContent.asset";
        public const string Pickups = RewardsFolder + "/PickupConfig.asset";
        public const string Results = UiFolder + "/ResultsConfig.asset";
        public const string MaterialFolder = "Assets/_Game/Art/Expedition";
        public const string Palette = MaterialFolder + "/WorldPalette.asset";
        public const string Scene = "Assets/_Game/Scenes/Expedition.unity";
        public const string ValidationReport = "Assets/_Game/Config/World/ChunkValidation.txt";

        public static string Chunk(string id)
        {
            return ChunkFolder + "/" + id + ".asset";
        }

        public static string Discovery(string id)
        {
            return DiscoveryFolder + "/" + id + ".asset";
        }

        public static string Ability(string name)
        {
            return ProgressionFolder + "/Ability_" + name.Replace(" ", string.Empty) + ".asset";
        }
    }
}
