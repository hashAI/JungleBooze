using System;
using System.IO;
using System.Text;
using JungleBooze.Core.Perf;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Benchmark files in <c>&lt;persistentDataPath&gt;/&lt;folder&gt;</c> (the app's Documents folder on iOS, visible in
    /// Finder / Files and through <c>xcrun devicectl device copy from</c>): <c>bench_&lt;stamp&gt;.csv</c> (one row per
    /// log interval) and <c>bench_&lt;stamp&gt;_summary.txt</c> (one block per phase). Rows are written from a reused
    /// char buffer, so logging does not allocate.
    /// </summary>
    public sealed class DeviceBenchLog : IDisposable
    {
        public const string CsvHeader = "t_s,phase,measuring,fps,frame_avg_ms,p50_ms,p95_ms,p99_ms,max_ms,missed,cpu_main_ms,cpu_render_ms,gpu_ms," +
                                        "thermal,footprint_mb,available_mb,battery_pct,low_power,draws,setpass,batches,tris,gc_frames,gc_max_b,render_w,render_h";

        private readonly StreamWriter _csv;
        private readonly StreamWriter _summary;

        public DeviceBenchLog(string folder, string stamp, string headerComment)
        {
            Directory.CreateDirectory(folder);
            CsvPath = Path.Combine(folder, "bench_" + stamp + ".csv");
            SummaryPath = Path.Combine(folder, "bench_" + stamp + "_summary.txt");
            _csv = new StreamWriter(CsvPath, false, new UTF8Encoding(false), 1 << 16);
            _summary = new StreamWriter(SummaryPath, false, new UTF8Encoding(false), 1 << 12);
            _csv.Write("# ");
            _csv.Write(headerComment);
            _csv.Write('\n');
            _csv.Write(CsvHeader);
            _csv.Write('\n');
            _summary.Write(headerComment);
            _summary.Write('\n');
            Flush();
        }

        public string CsvPath { get; }

        public string SummaryPath { get; }

        public void WriteRow(CharLine line)
        {
            _csv.Write(line.Buffer, 0, line.Length);
            _csv.Write('\n');
        }

        public void WriteSummary(string text)
        {
            _summary.Write(text);
            _summary.Write('\n');
            _summary.Flush();
        }

        public void Flush()
        {
            _csv.Flush();
            _summary.Flush();
        }

        public void Dispose()
        {
            _csv.Dispose();
            _summary.Dispose();
        }
    }
}
