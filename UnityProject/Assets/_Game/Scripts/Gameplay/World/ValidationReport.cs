using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JungleBooze.Gameplay.World
{
    /// <summary>Validator findings for one chunk variant (rule id, chunk-local s, message).</summary>
    public sealed class ValidationReport
    {
        public ValidationReport(string chunkId, string variant)
        {
            ChunkId = chunkId;
            Variant = variant;
        }

        public string ChunkId { get; }

        public string Variant { get; }

        public List<string> Issues { get; } = new List<string>();

        public bool Passed => Issues.Count == 0;

        public int BotRuns { get; set; }

        public long ProbeSteps { get; set; }

        public void Add(string rule, float s, string message)
        {
            string line = rule + " @" + s.ToString("0.#", CultureInfo.InvariantCulture) + ": " + message;
            if (!Issues.Contains(line))
            {
                Issues.Add(line);
            }
        }

        public override string ToString()
        {
            var b = new StringBuilder();
            b.Append(ChunkId).Append(" / ").Append(Variant).Append(Passed ? ": PASS" : ": FAIL");
            b.Append(" (bot runs ").Append(BotRuns).Append(')');
            for (int i = 0; i < Issues.Count; i++)
            {
                b.Append("\n  ").Append(Issues[i]);
            }

            return b.ToString();
        }
    }
}
