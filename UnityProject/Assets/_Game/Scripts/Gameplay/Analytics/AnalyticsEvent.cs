namespace JungleBooze.Gameplay.Analytics
{
    /// <summary>
    /// One analytics event (GDD §22), plain struct with typed slots so recording never allocates: string slots hold
    /// references to existing strings (ids, labels). <see cref="AnalyticsRecorder.ToJson"/> names the properties.
    /// </summary>
    public struct AnalyticsEvent
    {
        public AnalyticsEventType Type;

        /// <summary>Run time, ms.</summary>
        public long TimeMs;

        public int RunId;

        /// <summary>type / entry_id / power-up id / ability id / cause / route.</summary>
        public string Text;

        /// <summary>chunk_id / power-up action / obstacle label.</summary>
        public string Text2;

        /// <summary>obstacle_id / cost / index / run_index.</summary>
        public int Int;

        /// <summary>success / first_time / clean.</summary>
        public bool Flag;

        /// <summary>distance_m and similar.</summary>
        public float Value;
    }
}
