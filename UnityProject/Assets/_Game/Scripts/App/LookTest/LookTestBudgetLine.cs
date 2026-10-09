using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// One row of the per-view render budget (ENVIRONMENT_STRATEGY 4.2 layer table). Renderers built by the scene
    /// builder are named "&lt;Layer&gt; ..." (for example "L1 Walls (Leaves)"), so the screenshot tool can add up
    /// draws and triangles per layer and compare them with this row.
    /// </summary>
    [Serializable]
    public struct LookTestBudgetLine
    {
        [Tooltip("Layer code at the start of the renderer name: L0..L6, W (water and atmosphere), FX.")]
        public string Layer;
        public string Description;
        [Tooltip("Main-view draw calls, worst view.")]
        public int Draws;
        [Tooltip("Main-view triangles, worst view.")]
        public int Triangles;

        public LookTestBudgetLine(string layer, string description, int draws, int triangles)
        {
            Layer = layer;
            Description = description;
            Draws = draws;
            Triangles = triangles;
        }
    }
}
