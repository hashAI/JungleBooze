using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Painterly style values (ADR 0009, design/aurelia/ART_DIRECTION_PAINTERLY.md s4-s9) the hero basin builder writes
    /// into the _PAINTERLY shader variants. Colours are sRGB, as picked from the art direction's palette. Only read
    /// when the config's style is <see cref="HeroBasinStyle.Painterly"/>.
    /// </summary>
    [Serializable]
    public sealed class HeroPainterlyLook
    {
        [Header("Shared light (s4)")]
        public Color ShadowTint = new Color(0.243f, 0.361f, 0.400f, 0.45f);
        public Color Terminator = new Color(0.878f, 0.541f, 0.227f, 0.15f);
        public Color AOTint = new Color(0.302f, 0.227f, 0.133f, 1f);
        public float Wrap = 0.3f;
        public float RampSoftness = 0.22f;
        public float Ramp = 0.65f;
        public Color Rim = new Color(1f, 0.84f, 0.54f, 0.35f);

        [Header("Stone, ground, bark")]
        [Tooltip("Mip bias on the albedo: removes photo micro-detail (s3 noise budget).")]
        public float StoneBlur = 1.3f;
        public float StoneFlatten = 0.55f;
        public float StoneSaturation = 1.1f;
        public float StoneNormal = 0.4f;
        public float StoneSpecular = 0.12f;
        [Tooltip("Albedo multiplier toward warm sandstone (the realistic textures are grey bark / limestone).")]
        public Color StoneTint = new Color(1.0f, 0.88f, 0.70f, 1f);
        public Color GroundTint = new Color(1.05f, 1.0f, 0.82f, 1f);
        public Color MossColor = new Color(0.36f, 0.42f, 0.10f, 1f);
        [Tooltip("Normal strength on hand-painted (\"_P\") sets: their normals carry only broad forms already.")]
        public float PaintedNormal = 0.8f;

        [Header("Foliage (s5)")]
        public float LeafBlur = 0.5f;
        public float LeafFlatten = 0.6f;
        public float LeafSaturation = 1.2f;
        public float LeafNormal = 0.4f;
        public float LeafWrap = 0.55f;
        public Color LeafCore = new Color(0.45f, 0.55f, 0.40f, 0.55f);
        public Color LeafTip = new Color(1.2f, 1.12f, 0.7f, 0.6f);
        public Color LeafBackLight = new Color(0.79f, 0.82f, 0.25f, 0.5f);
        public Color LeafTint = Color.white;

        [Header("Water (s6)")]
        public Color WaterShallow = new Color(0.38f, 0.86f, 0.66f, 1f);
        public Color WaterMid = new Color(0.16f, 0.66f, 0.54f, 1f);
        public Color WaterDeep = new Color(0.06f, 0.42f, 0.36f, 1f);
        public Color WaterSky = new Color(0.745f, 0.890f, 0.910f, 1f);
        public Color Foam = new Color(0.988f, 0.941f, 0.796f, 1f);
        public float WaterDepthGain = 0.8f;
        public float WaterReflection = 0.35f;
        public float WaterLight = 0.55f;
        public float FoamScale = 0.35f;
        public float Sparkle = 2f;

        [Header("Falls (s6)")]
        public Color FallLitWhite = new Color(0.937f, 0.831f, 0.690f, 1f);
        public Color FallShadeWhite = new Color(0.741f, 0.682f, 0.600f, 1f);
        public float FallWhiteCut = 0.42f;
        public float FallEdge = 0.06f;
        public float FallStreakShade = 0.35f;

        [Header("Backdrop layers (s7)")]
        public float BackdropBlur = 0.3f;
        public float BackdropFlatten = 0f;
        public float BackdropSaturation = 1.05f;
        [Tooltip("Warm golden haze on the painted layers: rgb, a = amount on the farthest layer (fades to 0 on the nearest).")]
        public Color BackdropHaze = new Color(0.839f, 0.698f, 0.451f, 0.1f);

        [Header("Pista (s9)")]
        public float PistaNormalBlur = 2.5f;
        public float PistaNormalStrength = 0.45f;
        public float PistaWrap = 0.5f;
        public Color PistaSkin = new Color(0.91f, 0.627f, 0.478f, 0.5f);
        public float PistaRim = 0.6f;
        public float PistaSaturation = 1.1f;
    }
}
