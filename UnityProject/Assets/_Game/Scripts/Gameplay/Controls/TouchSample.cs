namespace JungleBooze.Gameplay.Controls
{
    /// <summary>One touch sample in iOS points (y up, Unity screen convention) with a timestamp in seconds.</summary>
    public readonly struct TouchSample
    {
        public TouchSample(int touchId, TouchPhaseKind phase, float xPt, float yPt, double time)
        {
            TouchId = touchId;
            Phase = phase;
            XPt = xPt;
            YPt = yPt;
            Time = time;
        }

        public int TouchId { get; }

        public TouchPhaseKind Phase { get; }

        public float XPt { get; }

        public float YPt { get; }

        public double Time { get; }
    }
}
