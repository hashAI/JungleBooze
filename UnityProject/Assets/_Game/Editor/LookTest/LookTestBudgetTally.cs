using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JungleBooze.App.LookTest;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Adds up draws and triangles of one view per budget layer (renderer names start with the layer code, see
    /// <see cref="LookTestBatchSet.RendererName"/>) for the main view and the shadow pass, and compares them with the
    /// budget in <see cref="LookTestConfigAsset"/> (ENVIRONMENT_STRATEGY 4.2, ADR 0004 Decision 7). Pure; unit-tested.
    /// </summary>
    public sealed class LookTestBudgetTally
    {
        private readonly SortedDictionary<string, long[]> _main = new SortedDictionary<string, long[]>();

        public int MainDraws { get; private set; }

        public long MainTriangles { get; private set; }

        public int ShadowDraws { get; private set; }

        public long ShadowTriangles { get; private set; }

        public void AddMain(string rendererName, int submeshes, long triangles)
        {
            string layer = LookTestBatchSet.LayerOf(rendererName);
            if (!_main.TryGetValue(layer, out long[] value))
            {
                value = new long[2];
                _main.Add(layer, value);
            }

            value[0] += submeshes;
            value[1] += triangles;
            MainDraws += submeshes;
            MainTriangles += triangles;
        }

        public void AddShadow(int submeshes, long triangles)
        {
            ShadowDraws += submeshes;
            ShadowTriangles += triangles;
        }

        /// <summary>Draws and triangles of one layer in the main view (0 if none).</summary>
        public long Draws(string layer)
        {
            return _main.TryGetValue(layer, out long[] v) ? v[0] : 0L;
        }

        public long Triangles(string layer)
        {
            return _main.TryGetValue(layer, out long[] v) ? v[1] : 0L;
        }

        /// <summary>True if the view is inside every total budget.</summary>
        public bool WithinTotals(LookTestConfigAsset config)
        {
            return MainDraws <= config.MainDrawBudget && MainTriangles <= config.MainTriangleBudget &&
                   ShadowDraws <= config.ShadowDrawBudget && ShadowTriangles <= config.ShadowTriangleBudget;
        }

        /// <summary>Text table: per layer measured / budget, then totals; "OVER" marks a missed budget.</summary>
        public string Format(LookTestConfigAsset config, int extraDraws)
        {
            var text = new StringBuilder();
            var seen = new HashSet<string>();
            LookTestBudgetLine[] lines = config.BudgetLines ?? new LookTestBudgetLine[0];
            for (int i = 0; i < lines.Length; i++)
            {
                LookTestBudgetLine line = lines[i];
                seen.Add(line.Layer);
                long draws = Draws(line.Layer);
                long tris = Triangles(line.Layer);
                bool over = draws > line.Draws || tris > line.Triangles;
                text.AppendFormat(CultureInfo.InvariantCulture, "  {0,-3} {1,-32} draws {2,4} / {3,-4} tris {4,9:N0} / {5,-9:N0}{6}\n",
                    line.Layer, line.Description, draws, line.Draws, tris, line.Triangles, over ? "  OVER" : string.Empty);
            }

            foreach (KeyValuePair<string, long[]> pair in _main)
            {
                if (!seen.Contains(pair.Key))
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "  {0,-3} {1,-32} draws {2,4}        tris {3,9:N0}  (no budget line)\n", pair.Key, "unlisted", pair.Value[0], pair.Value[1]);
                }
            }

            int main = MainDraws + extraDraws;
            text.AppendFormat(CultureInfo.InvariantCulture, "  Main view: {0} draws (incl. {1} sky/post) / {2}, {3:N0} tris / {4:N0}{5}\n",
                main, extraDraws, config.MainDrawBudget, MainTriangles, config.MainTriangleBudget,
                main > config.MainDrawBudget || MainTriangles > config.MainTriangleBudget ? "  OVER" : "  ok");
            text.AppendFormat(CultureInfo.InvariantCulture, "  Shadow pass (estimate): {0} draws / {1}, {2:N0} tris / {3:N0}{4}\n",
                ShadowDraws, config.ShadowDrawBudget, ShadowTriangles, config.ShadowTriangleBudget,
                ShadowDraws > config.ShadowDrawBudget || ShadowTriangles > config.ShadowTriangleBudget ? "  OVER" : "  ok");
            return text.ToString();
        }
    }
}
