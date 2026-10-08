using System;
using System.IO;
using System.Text;

namespace JungleBooze.Core
{
    /// <summary>
    /// Binary replay file (ADR 0006). Little-endian layout:
    /// magic "JBRP" (4 bytes), int32 format version, uint64 seed, uint64 config hash, string build version
    /// (length-prefixed UTF-8), int32 frame count, then per frame: int64 tick, uint8 commands, int16 lateral mm.
    /// Reading a different format version throws <see cref="NotSupportedException"/>: replays are never migrated.
    /// Allocates; call at run end or load time only.
    /// </summary>
    public static class InputRecordingSerializer
    {
        private static readonly byte[] Magic = { (byte)'J', (byte)'B', (byte)'R', (byte)'P' };

        public static void Write(InputRecording recording, Stream stream)
        {
            if (recording == null)
            {
                throw new ArgumentNullException(nameof(recording));
            }

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(InputRecording.CurrentFormatVersion);
                writer.Write(recording.Seed);
                writer.Write(recording.ConfigHash);
                writer.Write(recording.BuildVersion);
                writer.Write(recording.Count);
                for (int i = 0; i < recording.Count; i++)
                {
                    RecordedInput input = recording[i];
                    writer.Write(input.Tick);
                    writer.Write((byte)input.Frame.Commands);
                    writer.Write(input.Frame.LateralDeltaMm);
                }
            }
        }

        public static InputRecording Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                byte[] magic = reader.ReadBytes(Magic.Length);
                for (int i = 0; i < Magic.Length; i++)
                {
                    if (magic.Length != Magic.Length || magic[i] != Magic[i])
                    {
                        throw new InvalidDataException("Not a replay file (bad magic).");
                    }
                }

                int version = reader.ReadInt32();
                if (version != InputRecording.CurrentFormatVersion)
                {
                    throw new NotSupportedException(
                        "Replay format " + version + " is not supported (this build reads format " +
                        InputRecording.CurrentFormatVersion + ").");
                }

                ulong seed = reader.ReadUInt64();
                ulong configHash = reader.ReadUInt64();
                string build = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0)
                {
                    throw new InvalidDataException("Negative frame count.");
                }

                var recording = new InputRecording(seed, configHash, build, Math.Max(16, count));
                for (int i = 0; i < count; i++)
                {
                    long tick = reader.ReadInt64();
                    var commands = (InputCommand)reader.ReadByte();
                    short lateral = reader.ReadInt16();
                    recording.Add(tick, new InputFrame(commands, lateral));
                }

                return recording;
            }
        }
    }
}
