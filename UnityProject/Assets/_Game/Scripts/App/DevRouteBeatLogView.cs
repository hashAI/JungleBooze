using System.Globalization;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Dev aid (development builds and the editor; the bootstrap does not create it in release builds): logs the kind
    /// and start metre of the first <see cref="MaxBeats"/> beats of the run's route, for example
    /// "[JungleBooze] Route beat 3: Bend from 215 m". It reads the route only through <see cref="PathFrame.Sample(double, out PathPose)"/>,
    /// as the route is built ahead of HERO. Zone chunks (swing zone, gateway) are logged too but do not count as beats.
    /// Two beats of the same kind in a row show as one line. After a dev start distance the log begins where the route
    /// is still built (about 30 m behind the start point).
    /// </summary>
    public sealed class DevRouteBeatLogView : IRunView
    {
        public const int MaxBeats = 8;
        private const int MaxLines = 16;

        private readonly PathFrame _frame;
        private double _scanS;
        private RouteBeatKind _last;
        private bool _hasLast;
        private int _beats;
        private int _lines;

        public DevRouteBeatLogView(PathFrame frame)
        {
            _frame = frame;
        }

        public void BeginRun(GameSession session)
        {
            _scanS = _frame.BuiltStartS;
            _hasLast = false;
            _beats = 0;
            _lines = 0;
            if (Debug.isDebugBuild)
            {
                Debug.Log("[JungleBooze] Route beats of run " + session.RunSeed + " (first " + MaxBeats + "), metres along the track:");
            }

            Scan();
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            Scan();
        }

        private void Scan()
        {
            if (!Debug.isDebugBuild || _beats >= MaxBeats || _lines >= MaxLines)
            {
                return;
            }

            double step = _frame.Tuning.SampleSpacingM > 0.25f ? _frame.Tuning.SampleSpacingM : 0.25;
            double end = _frame.BuiltEndS;
            while (_scanS <= end && _beats < MaxBeats && _lines < MaxLines)
            {
                _frame.Sample(_scanS, out PathPose pose);
                if (!_hasLast || pose.Beat != _last)
                {
                    _hasLast = true;
                    _last = pose.Beat;
                    bool zone = pose.Beat == RouteBeatKind.SwingZone || pose.Beat == RouteBeatKind.Gateway;
                    if (!zone)
                    {
                        _beats++;
                    }

                    _lines++;
                    Debug.Log("[JungleBooze] Route " + (zone ? "zone: " : "beat " + _beats + ": ") + pose.Beat + " from "
                        + (_scanS < 0.0 ? 0.0 : _scanS).ToString("F0", CultureInfo.InvariantCulture) + " m.");
                }

                _scanS += step;
            }
        }
    }
}
