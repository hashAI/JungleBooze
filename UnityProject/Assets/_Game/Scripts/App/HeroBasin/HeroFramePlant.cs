using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// One hero framing plant of a dressing pass: a kit plant clump (name fragment, e.g. "FrameLeft_P") stood where the
    /// pass camera's ray through (u, v) lands, <see cref="SizeU"/> of the frame width wide, turned
    /// <see cref="YawDeg"/> from facing the camera. With <see cref="DepthM"/> above 0 the plant stands that far along
    /// the ray instead (its foot below the frame edge): for lens-near framing where the ray finds no ground.
    /// </summary>
    [Serializable]
    public struct HeroFramePlant
    {
        public string Piece;
        public Vector2 Uv;
        public float SizeU;
        public float YawDeg;
        public float DepthM;

        public HeroFramePlant(string piece, float u, float v, float sizeU, float yawDeg, float depthM = 0f)
        {
            Piece = piece;
            Uv = new Vector2(u, v);
            SizeU = sizeU;
            YawDeg = yawDeg;
            DepthM = depthM;
        }
    }
}
