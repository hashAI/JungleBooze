using System;
using System.Collections.Generic;
using System.IO;
using JungleBooze.Gameplay.Analytics;
using UnityEngine;

namespace JungleBooze.App.Expedition
{
    /// <summary>
    /// On-device analytics log (GDD §22, spec 103 AC-103-50): JSON lines appended to
    /// <c>persistentDataPath/analytics/events.jsonl</c>, or kept in memory for tools and tests. Nothing is sent over
    /// the network; a TestFlight-only upload is a later, owner-approved step (APP_STORE_CHECKLIST).
    /// </summary>
    public sealed class LocalAnalyticsLog : IAnalyticsSink
    {
        private readonly string _file;

        public LocalAnalyticsLog(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                _file = Path.Combine(folder, "analytics", "events.jsonl");
            }
        }

        /// <summary>Lines written this session (memory mode keeps all of them; file mode keeps the last 64).</summary>
        public List<string> Lines { get; } = new List<string>();

        public string FilePath => _file;

        public void Write(string jsonLine)
        {
            Lines.Add(jsonLine);
            if (_file == null)
            {
                return;
            }

            if (Lines.Count > 64)
            {
                Lines.RemoveAt(0);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file));
                File.AppendAllText(_file, jsonLine + "\n");
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[JungleBooze] Analytics log not written: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("[JungleBooze] Analytics log not written: " + exception.Message);
            }
        }
    }
}
