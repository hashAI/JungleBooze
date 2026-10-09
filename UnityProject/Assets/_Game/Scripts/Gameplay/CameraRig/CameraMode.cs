namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>Which camera modifier applies (spec 103 §11). Run = the plain spec 101 profile.</summary>
    public enum CameraMode : byte
    {
        Run = 0,
        Swim,
        DeepDive,
        Swing,
        Canopy,
    }
}
