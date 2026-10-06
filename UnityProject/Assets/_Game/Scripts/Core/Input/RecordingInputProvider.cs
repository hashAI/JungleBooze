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

        public InputCommand ReadCommands(long tick)
        {
            InputCommand commands = _inner.ReadCommands(tick);
            Recording.Add(tick, commands);
            return commands;
        }
    }
}
