namespace JungleBooze.Gameplay.Views
{
    /// <summary>Kind of a context piece (spec 005 3.3): a thing beside the path that explains where an obstacle came from or what holds it.</summary>
    public enum ContextKind : byte
    {
        None = 0,

        /// <summary>Upturned root disc at the end of a fallen log.</summary>
        RootPlate = 1,

        /// <summary>Stump the log fell from.</summary>
        Stump = 2,

        /// <summary>Fallen crown (leaf mass) of the log.</summary>
        Crown = 3,

        /// <summary>Trunk holding the support limb of a hanging mat.</summary>
        SupportTrunk = 4,

        /// <summary>Verge trunk a wedged slab leans against.</summary>
        VergeTrunk = 5,

        /// <summary>Rock ledge and scree the boulder rolled from.</summary>
        ScreeBank = 6,

        /// <summary>Woody root crown the thorn thicket grows from.</summary>
        RootCrown = 7,
    }
}
