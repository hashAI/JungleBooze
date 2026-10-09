using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// One painted backdrop card of the hero basin (Art/Environment/Backdrops): a cylinder strip around the camera at
    /// <see cref="DistanceM"/>, centred on <see cref="AzimuthDeg"/> (0 = straight ahead, + = right), spanning
    /// <see cref="WidthDeg"/>, its bottom edge at <see cref="BaseElevationDeg"/>. The height follows the texture's
    /// aspect ratio. Layers draw farthest first.
    /// </summary>
    [Serializable]
    public struct HeroBackdropLayer
    {
        public string Texture;
        public float DistanceM;
        public float AzimuthDeg;
        public float WidthDeg;
        public float BaseElevationDeg;
        [Tooltip("Mirror the painting horizontally (reuse a layer on the other side).")]
        public bool Mirror;
        [Range(0, 1)] public float FogShare;
        [Range(0, 2)] public float Exposure;

        public HeroBackdropLayer(string texture, float distanceM, float azimuthDeg, float widthDeg, float baseElevationDeg, bool mirror, float fogShare, float exposure)
        {
            Texture = texture;
            DistanceM = distanceM;
            AzimuthDeg = azimuthDeg;
            WidthDeg = widthDeg;
            BaseElevationDeg = baseElevationDeg;
            Mirror = mirror;
            FogShare = fogShare;
            Exposure = exposure;
        }
    }
}
