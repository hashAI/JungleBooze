using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>Small shared math helpers for the look-test builder.</summary>
    public static class LookTestMath
    {
        /// <summary>Hermite smoothstep from edge0 to edge1 (either order).</summary>
        public static float Smooth(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
