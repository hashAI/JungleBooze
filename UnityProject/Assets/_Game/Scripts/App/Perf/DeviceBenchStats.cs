using JungleBooze.Core.Perf;

namespace JungleBooze.App.Perf
{
    /// <summary>Running statistics over one interval (a log row or a whole phase). Allocation-free after construction.</summary>
    public sealed class DeviceBenchStats
    {
        public FrameTimeHistogram Frames { get; } = new FrameTimeHistogram(0.1f, 250f);

        public double CpuMainSum { get; private set; }
        public double CpuRenderSum { get; private set; }
        public double GpuSum { get; private set; }
        public int TimingSamples { get; private set; }
        public long Missed { get; private set; }
        public int ThermalMax { get; private set; } = -1;
        public long FootprintMaxBytes { get; private set; } = -1;
        public long AvailableMinBytes { get; private set; } = -1;
        public long GcFrames { get; private set; }
        public long GcMaxBytes { get; private set; }
        public long GcTotalBytes { get; private set; }
        public double Seconds { get; private set; }

        public double CpuMainMs => TimingSamples > 0 ? CpuMainSum / TimingSamples : -1.0;
        public double CpuRenderMs => TimingSamples > 0 ? CpuRenderSum / TimingSamples : -1.0;
        public double GpuMs => TimingSamples > 0 ? GpuSum / TimingSamples : -1.0;
        public double Fps => Seconds > 0.0 ? Frames.Count / Seconds : 0.0;

        public void Clear()
        {
            Frames.Clear();
            CpuMainSum = 0.0;
            CpuRenderSum = 0.0;
            GpuSum = 0.0;
            TimingSamples = 0;
            Missed = 0;
            ThermalMax = -1;
            FootprintMaxBytes = -1;
            AvailableMinBytes = -1;
            GcFrames = 0;
            GcMaxBytes = 0;
            GcTotalBytes = 0;
            Seconds = 0.0;
        }

        public void AddFrame(float ms, float missMs)
        {
            Frames.Add(ms);
            Seconds += ms * 0.001;
            if (ms > missMs)
            {
                Missed++;
            }
        }

        public void AddTiming(double cpuMain, double cpuRender, double gpu)
        {
            CpuMainSum += cpuMain;
            CpuRenderSum += cpuRender;
            GpuSum += gpu;
            TimingSamples++;
        }

        public void AddGc(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            GcFrames++;
            GcTotalBytes += bytes;
            if (bytes > GcMaxBytes)
            {
                GcMaxBytes = bytes;
            }
        }

        public void AddHealth(int thermal, long footprint, long available)
        {
            if (thermal > ThermalMax)
            {
                ThermalMax = thermal;
            }

            if (footprint > FootprintMaxBytes)
            {
                FootprintMaxBytes = footprint;
            }

            if (available >= 0 && (AvailableMinBytes < 0 || available < AvailableMinBytes))
            {
                AvailableMinBytes = available;
            }
        }
    }
}
