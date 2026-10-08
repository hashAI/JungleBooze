using System;

namespace JungleBooze.Core
{
    /// <summary>Decorator that passes input through unchanged and records it.</summary>
    public sealed class RecordingInputProvider : IInputProvider
    {
        private readonly IInputProvider _inner;

        public RecordingInputProvider(IInputProvider inner, InputRecording recording)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Recording = recording ?? throw new ArgumentNullException(nameof(recording));
        }

        public InputRecording Recording { get; }

        public InputFrame ReadInput(long tick)
        {
            InputFrame frame = _inner.ReadInput(tick);
            Recording.Add(tick, frame);
            return frame;
        }
    }
}
