using System;

namespace JungleBooze.Core
{
    /// <summary>Plays back an <see cref="InputRecording"/>. Allocation-free per tick.</summary>
    public sealed class ReplayInputProvider : IInputProvider
    {
        private readonly InputRecording _recording;
        private int _cursor;

        public ReplayInputProvider(InputRecording recording)
        {
            _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        }

        /// <summary>True once every recorded frame has been returned.</summary>
        public bool IsFinished => _cursor >= _recording.Count;

        public InputFrame ReadInput(long tick)
        {
            while (_cursor < _recording.Count && _recording[_cursor].Tick < tick)
            {
                _cursor++;
            }

            if (_cursor < _recording.Count && _recording[_cursor].Tick == tick)
            {
                InputFrame frame = _recording[_cursor].Frame;
                _cursor++;
                return frame;
            }

            return InputFrame.Empty;
        }
    }
}
