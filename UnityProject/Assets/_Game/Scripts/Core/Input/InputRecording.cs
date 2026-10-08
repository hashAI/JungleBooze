using System;
using System.Collections.Generic;

namespace JungleBooze.Core
{
    /// <summary>
    /// Everything needed to reproduce a run: header (format version, seed, config hash, build version) plus every
    /// non-empty input frame keyed by tick. Ticks without input are not stored. Binary form:
    /// <see cref="InputRecordingSerializer"/>. Format history: 1 = lane-era byte commands (retired);
    /// 2 = <see cref="InputFrame"/> commands + lateral drag in mm (ADR 0006).
    /// </summary>
    public sealed class InputRecording
    {
        /// <summary>Current replay format. Bump on any change to <see cref="InputFrame"/> or the binary layout.</summary>
        public const int CurrentFormatVersion = 2;

        private readonly List<RecordedInput> _frames;

        public InputRecording(ulong seed, int initialCapacity = 1024)
            : this(seed, 0UL, string.Empty, initialCapacity)
        {
        }

        public InputRecording(ulong seed, ulong configHash, string buildVersion, int initialCapacity = 1024)
        {
            Seed = seed;
            ConfigHash = configHash;
            BuildVersion = buildVersion ?? string.Empty;
            _frames = new List<RecordedInput>(initialCapacity);
        }

        /// <summary>Seed of the run's root <see cref="IRandom"/>.</summary>
        public ulong Seed { get; }

        /// <summary>Stable hash of the tuning the run used (0 = unknown). A replay is valid only for the same config.</summary>
        public ulong ConfigHash { get; }

        /// <summary>Build that recorded it. A replay is guaranteed only for the same build and platform (ADR 0002).</summary>
        public string BuildVersion { get; }

        public int FormatVersion => CurrentFormatVersion;

        public int Count => _frames.Count;

        public RecordedInput this[int index] => _frames[index];

        /// <summary>Appends a frame. Ticks must be strictly increasing. Empty frames are ignored.</summary>
        public void Add(long tick, InputFrame frame)
        {
            if (frame.IsEmpty)
            {
                return;
            }

            if (_frames.Count > 0 && tick <= _frames[_frames.Count - 1].Tick)
            {
                throw new ArgumentException("Ticks must be strictly increasing.", nameof(tick));
            }

            _frames.Add(new RecordedInput(tick, frame));
        }

        /// <summary>Removes all frames (keeps the header), for reuse across runs with the same seed.</summary>
        public void Clear()
        {
            _frames.Clear();
        }
    }
}
