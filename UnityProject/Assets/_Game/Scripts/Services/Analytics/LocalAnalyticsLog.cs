using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleBooze.Core.Analytics;
using UnityEngine;

namespace JungleBooze.Services.Analytics
{
    /// <summary>
    /// On-device analytics log (GDD §22, spec 103 AC-103-50): JSON lines in <c>folder/analytics/events.jsonl</c>, or
    /// kept in memory for tools and tests. The game passes <c>Application.temporaryCachePath</c> (iOS
    /// <c>Library/Caches</c>: not backed up to iCloud, re-creatable data, review S7). Lines are buffered and appended
    /// once per <see cref="Commit"/>; past <see cref="MaxBytes"/> the file rolls to <c>events.1.jsonl</c> (one old
    /// file kept), so the log never exceeds ~2 × MaxBytes. Nothing is sent over the network; a TestFlight-only upload
    /// is a later, owner-approved step (APP_STORE_CHECKLIST).
    /// </summary>
    public sealed class LocalAnalyticsLog : IAnalyticsSink
    {
        /// <summary>Roll the log over at this size, bytes.</summary>
        public const long MaxBytes = 256 * 1024;

        private const int KeepLines = 64;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _file;
        private readonly string _rolled;
        private readonly StringBuilder _batch = new StringBuilder(4096);

        public LocalAnalyticsLog(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                string dir = Path.Combine(folder, "analytics");
                _file = Path.Combine(dir, "events.jsonl");
                _rolled = Path.Combine(dir, "events.1.jsonl");
            }
        }

        /// <summary>Lines written this session (memory mode keeps all of them; file mode keeps the last 64).</summary>
        public List<string> Lines { get; } = new List<string>();

        public string FilePath => _file;

        public string RolledPath => _rolled;

        /// <summary>File writes so far (one per commit with lines).</summary>
        public int Commits { get; private set; }

        public void Write(string jsonLine)
        {
            Lines.Add(jsonLine);
            if (_file == null)
            {
                return;
            }

            if (Lines.Count > KeepLines)
            {
                Lines.RemoveAt(0);
            }

            _batch.Append(jsonLine).Append('\n');
        }

        public void Commit()
        {
            if (_file == null || _batch.Length == 0)
            {
                return;
            }

            string text = _batch.ToString();
            _batch.Length = 0;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file));
                var info = new FileInfo(_file);
                if (info.Exists && info.Length + Utf8NoBom.GetByteCount(text) > MaxBytes)
                {
                    if (File.Exists(_rolled))
                    {
                        File.Delete(_rolled);
                    }

                    File.Move(_file, _rolled);
                }

                File.AppendAllText(_file, text, Utf8NoBom);
                Commits++;
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
